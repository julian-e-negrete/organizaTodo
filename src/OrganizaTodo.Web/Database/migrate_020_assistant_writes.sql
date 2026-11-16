-- ============================================================
-- MIGRATION 020 — Assistant write entry points
--
-- Narrow, SECURITY DEFINER functions so an assistant role (jarvis_pay) can
-- add records without INSERT/UPDATE rights on any table:
--   sp_assistant_card_charge_add  one credit card charge (CreditCardPurchases)
--   sp_assistant_bill_add         a new monthly service bill (HousingServices)
--   sp_assistant_bill_rename      rename an existing bill (nothing else changes)
--   sp_assistant_usd_buy          a USD purchase exactly as the app records it:
--                                 AssetPurchases + OtherExpenses (ARS cost) +
--                                 Savings + LedgerTransactions, then a ledger
--                                 balance recompute
-- Every function checks p_user_id ownership, validates ranges and reuses the
-- app's own sp_* creators, so rows look identical to ones made in the app.
-- Idempotent: safe to run more than once. Depends on 019 only for the
-- organizatodo.change_source label (harmless without it).
-- ============================================================

CREATE OR REPLACE FUNCTION sp_assistant_card_charge_add(
    p_user_id INT, p_description VARCHAR(200), p_total_amount DECIMAL(18,2),
    p_installments INT, p_due_month INT, p_due_year INT
) RETURNS INT
LANGUAGE plpgsql SECURITY DEFINER SET search_path = public AS $$
DECLARE
    v_id INT;
BEGIN
    IF p_description IS NULL OR btrim(p_description) = '' THEN
        RAISE EXCEPTION 'Description is required';
    END IF;
    IF p_total_amount IS NULL OR p_total_amount <= 0 OR p_total_amount >= 1000000000 THEN
        RAISE EXCEPTION 'Amount must be greater than 0 and less than 1,000,000,000';
    END IF;
    IF p_installments IS NULL OR p_installments < 1 OR p_installments > 60 THEN
        RAISE EXCEPTION 'Installments must be between 1 and 60';
    END IF;
    IF p_due_month NOT BETWEEN 1 AND 12 OR p_due_year NOT BETWEEN 2000 AND 2100 THEN
        RAISE EXCEPTION 'Invalid due month/year';
    END IF;
    PERFORM sp_credit_card_create(p_user_id, btrim(p_description), p_total_amount,
                                  p_installments, 0, NOW()::timestamp, p_due_month, p_due_year);
    SELECT c."Id" INTO v_id FROM "CreditCardPurchases" c
    WHERE c."UserId" = p_user_id ORDER BY c."Id" DESC LIMIT 1;
    RETURN v_id;
END;
$$;

CREATE OR REPLACE FUNCTION sp_assistant_bill_add(
    p_user_id INT, p_name VARCHAR(100), p_amount DECIMAL(18,2), p_due_day INT
) RETURNS INT
LANGUAGE plpgsql SECURITY DEFINER SET search_path = public AS $$
DECLARE
    v_id INT;
BEGIN
    IF p_name IS NULL OR btrim(p_name) = '' THEN
        RAISE EXCEPTION 'Name is required';
    END IF;
    IF p_amount IS NULL OR p_amount <= 0 OR p_amount >= 1000000000 THEN
        RAISE EXCEPTION 'Amount must be greater than 0 and less than 1,000,000,000';
    END IF;
    IF p_due_day IS NOT NULL AND p_due_day NOT BETWEEN 1 AND 31 THEN
        RAISE EXCEPTION 'Due day must be between 1 and 31';
    END IF;
    IF EXISTS (SELECT 1 FROM "HousingServices" h WHERE h."UserId" = p_user_id
               AND h."IsActive" AND lower(btrim(h."Name")) = lower(btrim(p_name))) THEN
        RAISE EXCEPTION 'An active bill named "%" already exists', btrim(p_name);
    END IF;
    PERFORM set_config('organizatodo.change_source', 'jarvis', true);
    PERFORM sp_housing_services_create(p_user_id, btrim(p_name), p_amount, p_due_day, 'MONTHLY');
    SELECT h."Id" INTO v_id FROM "HousingServices" h
    WHERE h."UserId" = p_user_id ORDER BY h."Id" DESC LIMIT 1;
    RETURN v_id;
END;
$$;

CREATE OR REPLACE FUNCTION sp_assistant_bill_rename(
    p_user_id INT, p_bill_type VARCHAR(10), p_bill_id INT, p_new_name VARCHAR(100)
) RETURNS VARCHAR
LANGUAGE plpgsql SECURITY DEFINER SET search_path = public AS $$
DECLARE
    v_old VARCHAR(100);
BEGIN
    IF p_new_name IS NULL OR btrim(p_new_name) = '' THEN
        RAISE EXCEPTION 'New name is required';
    END IF;
    IF p_bill_type = 'HOUSING' THEN
        UPDATE "HousingServices" h SET "Name" = btrim(p_new_name)
        FROM (SELECT "Id", "Name" FROM "HousingServices"
              WHERE "Id" = p_bill_id AND "UserId" = p_user_id AND "IsActive" FOR UPDATE) o
        WHERE h."Id" = o."Id" RETURNING o."Name" INTO v_old;
    ELSIF p_bill_type = 'FIXED' THEN
        UPDATE "FixedLiabilities" f SET "Name" = btrim(p_new_name)
        FROM (SELECT "Id", "Name" FROM "FixedLiabilities"
              WHERE "Id" = p_bill_id AND "UserId" = p_user_id AND "IsActive" FOR UPDATE) o
        WHERE f."Id" = o."Id" RETURNING o."Name" INTO v_old;
    ELSE
        RAISE EXCEPTION 'Bill type must be HOUSING or FIXED';
    END IF;
    IF v_old IS NULL THEN
        RAISE EXCEPTION 'Bill not found';
    END IF;
    RETURN v_old;
END;
$$;

CREATE OR REPLACE FUNCTION sp_assistant_usd_buy(
    p_user_id INT, p_quantity DECIMAL(18,6), p_unit_price_ars DECIMAL(18,2),
    p_exchange_type VARCHAR(20), p_purchase_date DATE, p_notes VARCHAR(200) DEFAULT NULL
) RETURNS INT
LANGUAGE plpgsql SECURITY DEFINER SET search_path = public AS $$
DECLARE
    v_total DECIMAL(18,2);
    v_month INT := EXTRACT(MONTH FROM p_purchase_date);
    v_year  INT := EXTRACT(YEAR FROM p_purchase_date);
    v_id    INT;
BEGIN
    IF p_quantity IS NULL OR p_quantity <= 0 OR p_quantity > 1000000 THEN
        RAISE EXCEPTION 'Quantity must be greater than 0';
    END IF;
    IF p_unit_price_ars IS NULL OR p_unit_price_ars <= 0 OR p_unit_price_ars > 1000000 THEN
        RAISE EXCEPTION 'Rate must be greater than 0';
    END IF;
    IF p_purchase_date IS NULL OR p_purchase_date < DATE '2000-01-01' OR p_purchase_date > DATE '2100-01-01' THEN
        RAISE EXCEPTION 'Invalid purchase date';
    END IF;
    v_total := round(p_quantity * p_unit_price_ars, 2);
    v_id := sp_asset_purchases_create(
        p_user_id, 'USD', p_quantity, p_unit_price_ars,
        COALESCE(NULLIF(btrim(p_exchange_type), ''), 'oficial'), v_total,
        NULLIF(btrim(COALESCE(p_notes, '')), ''), p_purchase_date,
        TRUE, 'Compra USD ' || trim(to_char(p_quantity, 'FM999999990.######')), v_month, v_year,
        TRUE, NULL, v_month, v_year
    );
    PERFORM sp_ledger_recompute_balances(p_user_id);
    RETURN v_id;
END;
$$;

REVOKE EXECUTE ON FUNCTION sp_assistant_card_charge_add(INT, VARCHAR, DECIMAL, INT, INT, INT) FROM PUBLIC;
REVOKE EXECUTE ON FUNCTION sp_assistant_bill_add(INT, VARCHAR, DECIMAL, INT) FROM PUBLIC;
REVOKE EXECUTE ON FUNCTION sp_assistant_bill_rename(INT, VARCHAR, INT, VARCHAR) FROM PUBLIC;
REVOKE EXECUTE ON FUNCTION sp_assistant_usd_buy(INT, DECIMAL, DECIMAL, VARCHAR, DATE, VARCHAR) FROM PUBLIC;

DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'jarvis_pay') THEN
        GRANT EXECUTE ON FUNCTION
            sp_assistant_card_charge_add(INT, VARCHAR, DECIMAL, INT, INT, INT),
            sp_assistant_bill_add(INT, VARCHAR, DECIMAL, INT),
            sp_assistant_bill_rename(INT, VARCHAR, INT, VARCHAR),
            sp_assistant_usd_buy(INT, DECIMAL, DECIMAL, VARCHAR, DATE, VARCHAR)
        TO jarvis_pay;
    END IF;
END $$;

-- ============================================================
-- MIGRATION 021 — Assistant edits of credit card charges
--
--   sp_assistant_card_charge_set_amount  change one charge's TotalAmount
--   sp_assistant_card_charge_remove      soft-delete one charge (IsActive = FALSE,
--                                        IsDeleted = TRUE so sync propagates it;
--                                        exactly what the app's delete does)
-- SECURITY DEFINER, ownership-checked, EXECUTE granted to jarvis_pay only.
-- Both return the charge's description and old amount. Idempotent.
-- ============================================================

CREATE OR REPLACE FUNCTION sp_assistant_card_charge_set_amount(
    p_user_id INT, p_id INT, p_total_amount DECIMAL(18,2)
) RETURNS TABLE("Description" VARCHAR, "OldAmount" DECIMAL, "NewAmount" DECIMAL)
LANGUAGE plpgsql SECURITY DEFINER SET search_path = public AS $$
DECLARE
    v_desc VARCHAR;
    v_old  DECIMAL(18,2);
BEGIN
    IF p_total_amount IS NULL OR p_total_amount <= 0 OR p_total_amount >= 1000000000 THEN
        RAISE EXCEPTION 'Amount must be greater than 0 and less than 1,000,000,000';
    END IF;
    SELECT c."Description", c."TotalAmount" INTO v_desc, v_old FROM "CreditCardPurchases" c
    WHERE c."Id" = p_id AND c."UserId" = p_user_id AND c."IsActive"
    FOR UPDATE;
    IF NOT FOUND THEN
        RAISE EXCEPTION 'Credit card charge not found';
    END IF;
    UPDATE "CreditCardPurchases" SET "TotalAmount" = p_total_amount WHERE "Id" = p_id;
    RETURN QUERY SELECT v_desc, v_old, p_total_amount;
END;
$$;

CREATE OR REPLACE FUNCTION sp_assistant_card_charge_remove(
    p_user_id INT, p_id INT
) RETURNS TABLE("Description" VARCHAR, "OldAmount" DECIMAL)
LANGUAGE plpgsql SECURITY DEFINER SET search_path = public AS $$
DECLARE
    v_desc VARCHAR;
    v_old  DECIMAL(18,2);
BEGIN
    SELECT c."Description", c."TotalAmount" INTO v_desc, v_old FROM "CreditCardPurchases" c
    WHERE c."Id" = p_id AND c."UserId" = p_user_id AND c."IsActive"
    FOR UPDATE;
    IF NOT FOUND THEN
        RAISE EXCEPTION 'Credit card charge not found';
    END IF;
    UPDATE "CreditCardPurchases" SET "IsActive" = FALSE, "IsDeleted" = TRUE WHERE "Id" = p_id;
    RETURN QUERY SELECT v_desc, v_old;
END;
$$;

REVOKE EXECUTE ON FUNCTION sp_assistant_card_charge_set_amount(INT, INT, DECIMAL) FROM PUBLIC;
REVOKE EXECUTE ON FUNCTION sp_assistant_card_charge_remove(INT, INT) FROM PUBLIC;

DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'jarvis_pay') THEN
        GRANT EXECUTE ON FUNCTION
            sp_assistant_card_charge_set_amount(INT, INT, DECIMAL),
            sp_assistant_card_charge_remove(INT, INT)
        TO jarvis_pay;
    END IF;
END $$;

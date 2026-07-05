-- ============================================================
-- Migration 014: Link Savings to LedgerTransactions + cascade delete
--
-- 1. Every Ahorro auto-creates/updates/deletes a matching Ledger
--    income transaction (Ahorros y Deudas stays in sync).
--
-- 2. Otros Egresos remain fully independent — no LedgerTransaction
--    is ever created for them.
--
-- 3. Cascade delete: removing any record in the linked trio
--    (Ahorro / Compra de Activo / Otro Egreso) deletes all three
--    plus the Saving's LedgerTransaction, in FK-safe order.
-- ============================================================

-- ============================================================
-- SCHEMA – link column only on Savings
-- ============================================================

ALTER TABLE "Savings"
    ADD COLUMN IF NOT EXISTS "LedgerTransactionId" INT
        REFERENCES "LedgerTransactions"("Id") ON DELETE SET NULL;

-- ============================================================
-- DROP functions whose return type or parameter names change.
-- PostgreSQL forbids both via CREATE OR REPLACE.
-- ============================================================

DROP FUNCTION IF EXISTS sp_savings_create(integer, numeric, integer, integer, character varying, boolean);
DROP FUNCTION IF EXISTS sp_savings_delete(integer, integer);
DROP FUNCTION IF EXISTS sp_other_expenses_create(integer, character varying, numeric, integer, integer, integer);
DROP FUNCTION IF EXISTS sp_other_expenses_delete(integer, integer);

-- ============================================================
-- sp_savings_create — returns INT, auto-creates Ledger income tx
-- ============================================================

CREATE OR REPLACE FUNCTION sp_savings_create(
    user_id            INT,
    amount             DECIMAL(18,2),
    month              INT,
    year               INT,
    notes              VARCHAR(200),
    is_initial_balance BOOLEAN
) RETURNS INT LANGUAGE plpgsql AS $$
DECLARE
    v_id            INT;
    v_ledger_id     INT;
    v_balance_after DECIMAL(18,2);
    v_pos_amount    DECIMAL(18,2) := ABS(amount);
    v_tx_date       TIMESTAMP     := MAKE_DATE(year, month, 1)::TIMESTAMP;
    v_description   VARCHAR(300)  := COALESCE(NULLIF(TRIM(notes), ''), 'Ahorro');
BEGIN
    INSERT INTO "Savings"("UserId","Amount","Month","Year","Notes","IsInitialBalance")
    VALUES (user_id, amount, month, year, COALESCE(notes, ''), is_initial_balance)
    RETURNING "Id" INTO v_id;

    -- IsInitialBalance rows are migration artefacts; skip Ledger.
    IF NOT is_initial_balance THEN
        v_balance_after := sp_ledger_balance_get_current(user_id) + v_pos_amount;
        INSERT INTO "LedgerTransactions"
            ("UserId","Amount","Type","Description","TransactionDate","BalanceAfter")
        VALUES
            (user_id, v_pos_amount, 'income', v_description, v_tx_date, v_balance_after)
        RETURNING "Id" INTO v_ledger_id;

        UPDATE "Savings" SET "LedgerTransactionId" = v_ledger_id WHERE "Id" = v_id;
        PERFORM sp_ledger_recompute_balances(user_id);
    END IF;

    RETURN v_id;
END;
$$;

-- ============================================================
-- sp_savings_update — also updates the linked Ledger tx
-- ============================================================

CREATE OR REPLACE FUNCTION sp_savings_update(id INT, user_id INT, amount DECIMAL(18,2), notes VARCHAR(200))
RETURNS VOID LANGUAGE plpgsql AS $$
DECLARE
    v_ledger_id INT;
BEGIN
    SELECT "LedgerTransactionId" INTO v_ledger_id
    FROM "Savings" WHERE "Id" = id AND "UserId" = user_id;

    UPDATE "Savings"
    SET "Amount" = amount,
        "Notes"  = COALESCE(notes, '')
    WHERE "Id" = id AND "UserId" = user_id;

    IF v_ledger_id IS NOT NULL THEN
        UPDATE "LedgerTransactions"
        SET "Amount"      = ABS(amount),
            "Description" = COALESCE(NULLIF(TRIM(notes), ''), 'Ahorro')
        WHERE "Id" = v_ledger_id;
        PERFORM sp_ledger_recompute_balances(user_id);
    END IF;
END;
$$;

-- ============================================================
-- sp_savings_delete — cascade: AP → OtherExpense → Saving → LedgerTx
-- ============================================================

CREATE OR REPLACE FUNCTION sp_savings_delete(p_id INT, p_user_id INT)
RETURNS VOID LANGUAGE plpgsql AS $$
DECLARE
    v_ledger_id     INT;
    v_ap_id         INT;
    v_ap_expense_id INT;
BEGIN
    -- Read all linked IDs before any deletions
    SELECT "LedgerTransactionId" INTO v_ledger_id
    FROM "Savings" WHERE "Id" = p_id AND "UserId" = p_user_id;

    SELECT "Id", "ExpenseId" INTO v_ap_id, v_ap_expense_id
    FROM "AssetPurchases"
    WHERE "SavingId" = p_id AND "UserId" = p_user_id
    LIMIT 1;

    -- Delete AP first (clears FK references to Saving and OtherExpense)
    IF v_ap_id IS NOT NULL THEN
        DELETE FROM "AssetPurchases" WHERE "Id" = v_ap_id;
    END IF;

    -- Delete linked OtherExpense (no LedgerTransaction on expense side)
    IF v_ap_expense_id IS NOT NULL THEN
        DELETE FROM "OtherExpenses" WHERE "Id" = v_ap_expense_id;
    END IF;

    -- Delete the Saving
    DELETE FROM "Savings" WHERE "Id" = p_id AND "UserId" = p_user_id;

    -- Delete the Saving's LedgerTransaction last
    IF v_ledger_id IS NOT NULL THEN
        DELETE FROM "LedgerTransactions" WHERE "Id" = v_ledger_id;
        PERFORM sp_ledger_recompute_balances(p_user_id);
    END IF;
END;
$$;

-- ============================================================
-- sp_other_expenses_create — returns INT (used by asset purchase SP),
-- no LedgerTransaction (Otros Egresos is independent of the Ledger)
-- ============================================================

CREATE OR REPLACE FUNCTION sp_other_expenses_create(
    p_user_id     INT,
    p_description VARCHAR(200),
    p_amount      DECIMAL(18,2),
    p_month       INT,
    p_year        INT,
    p_vehicle_id  INT
) RETURNS INT LANGUAGE plpgsql AS $$
DECLARE
    v_id INT;
BEGIN
    INSERT INTO "OtherExpenses"("UserId","Description","Amount","Month","Year","VehicleId")
    VALUES (p_user_id, p_description, p_amount, p_month, p_year, p_vehicle_id)
    RETURNING "Id" INTO v_id;
    RETURN v_id;
END;
$$;

-- ============================================================
-- sp_other_expenses_update — plain update, no Ledger side-effect
-- ============================================================

CREATE OR REPLACE FUNCTION sp_other_expenses_update(
    p_id          INT,
    p_user_id     INT,
    p_description VARCHAR(200),
    p_amount      DECIMAL(18,2),
    p_vehicle_id  INT
) RETURNS VOID LANGUAGE plpgsql AS $$
BEGIN
    UPDATE "OtherExpenses"
    SET "Description" = p_description,
        "Amount"      = p_amount,
        "VehicleId"   = p_vehicle_id
    WHERE "Id" = p_id AND "UserId" = p_user_id;
END;
$$;

-- ============================================================
-- sp_other_expenses_delete — cascade: AP → linked Saving + its LedgerTx → OtherExpense
-- ============================================================

CREATE OR REPLACE FUNCTION sp_other_expenses_delete(p_id INT, p_user_id INT)
RETURNS VOID LANGUAGE plpgsql AS $$
DECLARE
    v_ap_id            INT;
    v_ap_saving_id     INT;
    v_saving_ledger_id INT;
BEGIN
    -- Read all linked IDs before any deletions
    SELECT "Id", "SavingId" INTO v_ap_id, v_ap_saving_id
    FROM "AssetPurchases"
    WHERE "ExpenseId" = p_id AND "UserId" = p_user_id
    LIMIT 1;

    IF v_ap_saving_id IS NOT NULL THEN
        SELECT "LedgerTransactionId" INTO v_saving_ledger_id
        FROM "Savings" WHERE "Id" = v_ap_saving_id;
    END IF;

    -- Delete AP first (clears FK references to Saving and OtherExpense)
    IF v_ap_id IS NOT NULL THEN
        DELETE FROM "AssetPurchases" WHERE "Id" = v_ap_id;
    END IF;

    -- Delete linked Saving
    IF v_ap_saving_id IS NOT NULL THEN
        DELETE FROM "Savings" WHERE "Id" = v_ap_saving_id;
    END IF;

    -- Delete the OtherExpense
    DELETE FROM "OtherExpenses" WHERE "Id" = p_id AND "UserId" = p_user_id;

    -- Delete Saving's LedgerTransaction last (if it had one)
    IF v_saving_ledger_id IS NOT NULL THEN
        DELETE FROM "LedgerTransactions" WHERE "Id" = v_saving_ledger_id;
        PERFORM sp_ledger_recompute_balances(p_user_id);
    END IF;
END;
$$;

-- ============================================================
-- sp_asset_purchases_create — call updated SPs so the linked
-- Saving auto-creates its LedgerTransaction
-- ============================================================

CREATE OR REPLACE FUNCTION sp_asset_purchases_create(
    p_user_id           INT,
    p_asset_type        VARCHAR,
    p_quantity          NUMERIC,
    p_unit_price_ars    NUMERIC,
    p_exchange_type     VARCHAR,
    p_total_ars         NUMERIC,
    p_notes             VARCHAR,
    p_purchase_date     DATE,
    p_create_expense    BOOLEAN,
    p_expense_desc      VARCHAR,
    p_expense_month     INT,
    p_expense_year      INT,
    p_create_saving     BOOLEAN,
    p_saving_notes      VARCHAR,
    p_saving_month      INT,
    p_saving_year       INT
) RETURNS INT LANGUAGE plpgsql AS $$
DECLARE
    v_expense_id  INT := NULL;
    v_saving_id   INT := NULL;
    v_saving_amt  NUMERIC;
    v_saving_note VARCHAR;
    v_id          INT;
BEGIN
    IF p_create_expense THEN
        -- Returns the new expense ID; no LedgerTransaction created
        v_expense_id := sp_other_expenses_create(
            p_user_id, p_expense_desc, p_total_ars,
            p_expense_month, p_expense_year, NULL
        );
    END IF;

    IF p_create_saving THEN
        v_saving_amt := CASE
            WHEN p_quantity IS NOT NULL AND p_asset_type IN ('USD','ACCION','CRYPTO') THEN p_quantity
            ELSE p_total_ars
        END;

        v_saving_note := COALESCE(
            NULLIF(p_saving_notes, ''),
            CASE p_asset_type
                WHEN 'USD'    THEN 'Dólares'
                WHEN 'ACCION' THEN 'Acciones'
                WHEN 'CRYPTO' THEN 'Crypto'
                ELSE ''
            END
        );

        -- Returns the new saving ID; also creates its LedgerTransaction
        v_saving_id := sp_savings_create(
            p_user_id, v_saving_amt,
            p_saving_month, p_saving_year,
            v_saving_note, FALSE
        );
    END IF;

    INSERT INTO "AssetPurchases"(
        "UserId","ExpenseId","SavingId","AssetType","Quantity",
        "UnitPriceArs","ExchangeType","TotalArs","Notes","PurchaseDate"
    ) VALUES (
        p_user_id, v_expense_id, v_saving_id, p_asset_type, p_quantity,
        p_unit_price_ars, p_exchange_type, p_total_ars, p_notes, p_purchase_date
    ) RETURNING "Id" INTO v_id;

    RETURN v_id;
END;
$$;

-- ============================================================
-- sp_asset_purchases_delete — cascade: AP → OtherExpense → Saving → Saving's LedgerTx
-- ============================================================

CREATE OR REPLACE FUNCTION sp_asset_purchases_delete(p_id INT, p_user_id INT)
RETURNS VOID LANGUAGE plpgsql AS $$
DECLARE
    v_expense_id       INT;
    v_saving_id        INT;
    v_saving_ledger_id INT;
BEGIN
    -- Read all linked IDs before any deletions
    SELECT "ExpenseId", "SavingId" INTO v_expense_id, v_saving_id
    FROM "AssetPurchases" WHERE "Id" = p_id AND "UserId" = p_user_id;

    IF v_saving_id IS NOT NULL THEN
        SELECT "LedgerTransactionId" INTO v_saving_ledger_id
        FROM "Savings" WHERE "Id" = v_saving_id;
    END IF;

    -- Delete AP first (clears FK references to OtherExpense and Saving)
    DELETE FROM "AssetPurchases" WHERE "Id" = p_id AND "UserId" = p_user_id;

    -- Delete linked OtherExpense (no LedgerTransaction on its side)
    IF v_expense_id IS NOT NULL THEN
        DELETE FROM "OtherExpenses" WHERE "Id" = v_expense_id;
    END IF;

    -- Delete linked Saving
    IF v_saving_id IS NOT NULL THEN
        DELETE FROM "Savings" WHERE "Id" = v_saving_id;
    END IF;

    -- Delete Saving's LedgerTransaction last
    IF v_saving_ledger_id IS NOT NULL THEN
        DELETE FROM "LedgerTransactions" WHERE "Id" = v_saving_ledger_id;
        PERFORM sp_ledger_recompute_balances(p_user_id);
    END IF;
END;
$$;

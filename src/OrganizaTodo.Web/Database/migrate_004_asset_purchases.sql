-- ============================================================
-- TABLE – Asset Purchases (Compras de Activos)
-- ============================================================

CREATE TABLE IF NOT EXISTS "AssetPurchases" (
    "Id"            SERIAL PRIMARY KEY,
    "UserId"        INT NOT NULL REFERENCES "Users"("Id") ON DELETE CASCADE,
    "ExpenseId"     INT REFERENCES "OtherExpenses"("Id") ON DELETE SET NULL,
    "SavingId"      INT REFERENCES "Savings"("Id") ON DELETE SET NULL,
    "AssetType"     VARCHAR(20) NOT NULL,
    "Quantity"      NUMERIC(18,6),
    "UnitPriceArs"  NUMERIC(18,2),
    "ExchangeType"  VARCHAR(30),
    "TotalArs"      NUMERIC(18,2) NOT NULL,
    "Notes"         VARCHAR(500),
    "PurchaseDate"  DATE NOT NULL DEFAULT CURRENT_DATE,
    "CreatedAt"     TIMESTAMP NOT NULL DEFAULT NOW()
);

-- ============================================================
-- FUNCTIONS – Asset Purchases
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
        INSERT INTO "OtherExpenses"("UserId","Description","Amount","Month","Year")
        VALUES (p_user_id, p_expense_desc, p_total_ars, p_expense_month, p_expense_year)
        RETURNING "Id" INTO v_expense_id;
    END IF;

    IF p_create_saving THEN
        -- For USD/stocks/crypto with a known quantity, store the asset units (not the ARS cost)
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

        INSERT INTO "Savings"("UserId","Amount","Month","Year","Notes","IsInitialBalance")
        VALUES (p_user_id, v_saving_amt, p_saving_month, p_saving_year, v_saving_note, FALSE)
        RETURNING "Id" INTO v_saving_id;
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

CREATE OR REPLACE FUNCTION sp_asset_purchases_get_by_user_id(p_user_id INT)
RETURNS TABLE(
    "Id" INT, "UserId" INT, "ExpenseId" INT, "SavingId" INT,
    "AssetType" VARCHAR, "Quantity" NUMERIC, "UnitPriceArs" NUMERIC,
    "ExchangeType" VARCHAR, "TotalArs" NUMERIC, "Notes" VARCHAR,
    "PurchaseDate" DATE, "CreatedAt" TIMESTAMP,
    "ExpenseDescription" VARCHAR, "SavingNotes" VARCHAR
) LANGUAGE plpgsql AS $$
BEGIN
    RETURN QUERY
    SELECT ap."Id", ap."UserId", ap."ExpenseId", ap."SavingId",
           ap."AssetType", ap."Quantity", ap."UnitPriceArs",
           ap."ExchangeType", ap."TotalArs", ap."Notes",
           ap."PurchaseDate", ap."CreatedAt",
           oe."Description"::VARCHAR,
           s."Notes"::VARCHAR
    FROM "AssetPurchases" ap
    LEFT JOIN "OtherExpenses" oe ON oe."Id" = ap."ExpenseId"
    LEFT JOIN "Savings" s ON s."Id" = ap."SavingId"
    WHERE ap."UserId" = p_user_id
    ORDER BY ap."PurchaseDate" DESC, ap."CreatedAt" DESC;
END;
$$;

CREATE OR REPLACE FUNCTION sp_asset_purchases_get_by_id(p_id INT, p_user_id INT)
RETURNS TABLE(
    "Id" INT, "UserId" INT, "ExpenseId" INT, "SavingId" INT,
    "AssetType" VARCHAR, "Quantity" NUMERIC, "UnitPriceArs" NUMERIC,
    "ExchangeType" VARCHAR, "TotalArs" NUMERIC, "Notes" VARCHAR,
    "PurchaseDate" DATE, "CreatedAt" TIMESTAMP,
    "ExpenseDescription" VARCHAR, "SavingNotes" VARCHAR
) LANGUAGE plpgsql AS $$
BEGIN
    RETURN QUERY
    SELECT ap."Id", ap."UserId", ap."ExpenseId", ap."SavingId",
           ap."AssetType", ap."Quantity", ap."UnitPriceArs",
           ap."ExchangeType", ap."TotalArs", ap."Notes",
           ap."PurchaseDate", ap."CreatedAt",
           oe."Description"::VARCHAR,
           s."Notes"::VARCHAR
    FROM "AssetPurchases" ap
    LEFT JOIN "OtherExpenses" oe ON oe."Id" = ap."ExpenseId"
    LEFT JOIN "Savings" s ON s."Id" = ap."SavingId"
    WHERE ap."Id" = p_id AND ap."UserId" = p_user_id;
END;
$$;

CREATE OR REPLACE FUNCTION sp_asset_purchases_update(
    p_id            INT,
    p_user_id       INT,
    p_asset_type    VARCHAR,
    p_quantity      NUMERIC,
    p_unit_price    NUMERIC,
    p_exchange_type VARCHAR,
    p_total_ars     NUMERIC,
    p_notes         VARCHAR,
    p_purchase_date DATE
) RETURNS VOID LANGUAGE plpgsql AS $$
BEGIN
    UPDATE "AssetPurchases" SET
        "AssetType"    = p_asset_type,
        "Quantity"     = p_quantity,
        "UnitPriceArs" = p_unit_price,
        "ExchangeType" = p_exchange_type,
        "TotalArs"     = p_total_ars,
        "Notes"        = p_notes,
        "PurchaseDate" = p_purchase_date
    WHERE "Id" = p_id AND "UserId" = p_user_id;
END;
$$;

CREATE OR REPLACE FUNCTION sp_asset_purchases_delete(p_id INT, p_user_id INT)
RETURNS VOID LANGUAGE plpgsql AS $$
BEGIN
    DELETE FROM "AssetPurchases" WHERE "Id" = p_id AND "UserId" = p_user_id;
END;
$$;

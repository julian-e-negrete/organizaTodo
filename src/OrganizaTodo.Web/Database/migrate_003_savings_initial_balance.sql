-- Migration 003: Add IsInitialBalance flag to Savings

ALTER TABLE "Savings" ADD COLUMN IF NOT EXISTS "IsInitialBalance" BOOLEAN NOT NULL DEFAULT FALSE;

-- Exclude initial-balance entries from the monthly average
CREATE OR REPLACE FUNCTION sp_savings_get_monthly_average(user_id INT)
RETURNS DECIMAL(18,2) LANGUAGE plpgsql AS $$
DECLARE avg_amount DECIMAL(18,2);
BEGIN
    SELECT COALESCE(AVG("Amount"), 0) INTO avg_amount
    FROM "Savings"
    WHERE "UserId" = user_id AND "IsInitialBalance" = FALSE;
    RETURN avg_amount;
END;
$$;

-- Return new column in selects
DROP FUNCTION IF EXISTS sp_savings_get_by_user_id(INT);
CREATE OR REPLACE FUNCTION sp_savings_get_by_user_id(user_id INT)
RETURNS TABLE("Id" INT,"UserId" INT,"Amount" DECIMAL,"Month" INT,"Year" INT,"Notes" VARCHAR,"IsInitialBalance" BOOLEAN,"CreatedAt" TIMESTAMP)
LANGUAGE plpgsql AS $$
BEGIN
    RETURN QUERY
    SELECT s."Id",s."UserId",s."Amount",s."Month",s."Year",s."Notes",s."IsInitialBalance",s."CreatedAt"
    FROM "Savings" s WHERE s."UserId" = user_id
    ORDER BY s."IsInitialBalance" DESC, s."Year" DESC, s."Month" DESC;
END;
$$;

DROP FUNCTION IF EXISTS sp_savings_get_by_id(INT, INT);
CREATE OR REPLACE FUNCTION sp_savings_get_by_id(id INT, user_id INT)
RETURNS TABLE("Id" INT,"UserId" INT,"Amount" DECIMAL,"Month" INT,"Year" INT,"Notes" VARCHAR,"IsInitialBalance" BOOLEAN,"CreatedAt" TIMESTAMP)
LANGUAGE plpgsql AS $$
BEGIN
    RETURN QUERY
    SELECT s."Id",s."UserId",s."Amount",s."Month",s."Year",s."Notes",s."IsInitialBalance",s."CreatedAt"
    FROM "Savings" s WHERE s."Id" = id AND s."UserId" = user_id;
END;
$$;

DROP FUNCTION IF EXISTS sp_savings_create(INT, DECIMAL, INT, INT, VARCHAR);
CREATE OR REPLACE FUNCTION sp_savings_create(user_id INT, amount DECIMAL(18,2), month INT, year INT, notes VARCHAR(200), is_initial_balance BOOLEAN)
RETURNS VOID LANGUAGE plpgsql AS $$
BEGIN
    INSERT INTO "Savings"("UserId","Amount","Month","Year","Notes","IsInitialBalance")
    VALUES (user_id, amount, month, year, COALESCE(notes, ''), is_initial_balance);
END;
$$;

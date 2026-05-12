-- Migration 002: Savings table and functions

CREATE TABLE IF NOT EXISTS "Savings" (
    "Id"        SERIAL        PRIMARY KEY,
    "UserId"    INT           NOT NULL REFERENCES "Users"("Id") ON DELETE CASCADE,
    "Amount"    DECIMAL(18,2) NOT NULL,
    "Month"     INT           NOT NULL,
    "Year"      INT           NOT NULL,
    "Notes"     VARCHAR(200)  NOT NULL DEFAULT '',
    "CreatedAt" TIMESTAMP     NOT NULL DEFAULT NOW()
);

CREATE OR REPLACE FUNCTION sp_savings_get_by_user_id(user_id INT)
RETURNS TABLE("Id" INT,"UserId" INT,"Amount" DECIMAL,"Month" INT,"Year" INT,"Notes" VARCHAR,"CreatedAt" TIMESTAMP)
LANGUAGE plpgsql AS $$
BEGIN
    RETURN QUERY
    SELECT s."Id",s."UserId",s."Amount",s."Month",s."Year",s."Notes",s."CreatedAt"
    FROM "Savings" s
    WHERE s."UserId" = user_id
    ORDER BY s."Year" DESC, s."Month" DESC;
END;
$$;

CREATE OR REPLACE FUNCTION sp_savings_get_by_id(id INT, user_id INT)
RETURNS TABLE("Id" INT,"UserId" INT,"Amount" DECIMAL,"Month" INT,"Year" INT,"Notes" VARCHAR,"CreatedAt" TIMESTAMP)
LANGUAGE plpgsql AS $$
BEGIN
    RETURN QUERY
    SELECT s."Id",s."UserId",s."Amount",s."Month",s."Year",s."Notes",s."CreatedAt"
    FROM "Savings" s
    WHERE s."Id" = id AND s."UserId" = user_id;
END;
$$;

CREATE OR REPLACE FUNCTION sp_savings_create(user_id INT, amount DECIMAL(18,2), month INT, year INT, notes VARCHAR(200))
RETURNS VOID LANGUAGE plpgsql AS $$
BEGIN
    INSERT INTO "Savings"("UserId","Amount","Month","Year","Notes")
    VALUES (user_id, amount, month, year, COALESCE(notes, ''));
END;
$$;

CREATE OR REPLACE FUNCTION sp_savings_update(id INT, user_id INT, amount DECIMAL(18,2), notes VARCHAR(200))
RETURNS VOID LANGUAGE plpgsql AS $$
BEGIN
    UPDATE "Savings"
    SET "Amount"=amount, "Notes"=COALESCE(notes, '')
    WHERE "Id"=id AND "UserId"=user_id;
END;
$$;

CREATE OR REPLACE FUNCTION sp_savings_delete(id INT, user_id INT)
RETURNS VOID LANGUAGE plpgsql AS $$
BEGIN
    DELETE FROM "Savings" WHERE "Id"=id AND "UserId"=user_id;
END;
$$;

CREATE OR REPLACE FUNCTION sp_savings_get_cumulative_total(user_id INT)
RETURNS DECIMAL(18,2) LANGUAGE plpgsql AS $$
DECLARE total DECIMAL(18,2);
BEGIN
    SELECT COALESCE(SUM("Amount"), 0) INTO total
    FROM "Savings" WHERE "UserId" = user_id;
    RETURN total;
END;
$$;

CREATE OR REPLACE FUNCTION sp_savings_get_monthly_average(user_id INT)
RETURNS DECIMAL(18,2) LANGUAGE plpgsql AS $$
DECLARE avg_amount DECIMAL(18,2);
BEGIN
    SELECT COALESCE(AVG("Amount"), 0) INTO avg_amount
    FROM "Savings" WHERE "UserId" = user_id;
    RETURN avg_amount;
END;
$$;

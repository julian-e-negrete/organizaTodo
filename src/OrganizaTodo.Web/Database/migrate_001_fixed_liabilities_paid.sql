-- Migration 001 – Add IsPaid / PaidDate to FixedLiabilities
-- Run once against the organizaTodo database.

ALTER TABLE "FixedLiabilities"
    ADD COLUMN IF NOT EXISTS "IsPaid"   BOOLEAN   NOT NULL DEFAULT FALSE,
    ADD COLUMN IF NOT EXISTS "PaidDate" TIMESTAMP NULL;

DROP FUNCTION IF EXISTS sp_fixed_liabilities_get_by_user_id(INT);
DROP FUNCTION IF EXISTS sp_fixed_liabilities_get_by_id(INT, INT);

CREATE OR REPLACE FUNCTION sp_fixed_liabilities_get_by_user_id(user_id INT)
RETURNS TABLE(
    "Id" INT,"UserId" INT,"Name" VARCHAR,"MonthlyAmount" DECIMAL,
    "DueDay" INT,"IsActive" BOOLEAN,"IsPaid" BOOLEAN,"PaidDate" TIMESTAMP,"CreatedAt" TIMESTAMP
) LANGUAGE plpgsql AS $$
BEGIN
    RETURN QUERY
    SELECT f."Id",f."UserId",f."Name",f."MonthlyAmount",f."DueDay",f."IsActive",f."IsPaid",f."PaidDate",f."CreatedAt"
    FROM "FixedLiabilities" f WHERE f."UserId"=user_id AND f."IsActive"=TRUE ORDER BY f."Name";
END;
$$;

CREATE OR REPLACE FUNCTION sp_fixed_liabilities_get_by_id(id INT, user_id INT)
RETURNS TABLE(
    "Id" INT,"UserId" INT,"Name" VARCHAR,"MonthlyAmount" DECIMAL,
    "DueDay" INT,"IsActive" BOOLEAN,"IsPaid" BOOLEAN,"PaidDate" TIMESTAMP,"CreatedAt" TIMESTAMP
) LANGUAGE plpgsql AS $$
BEGIN
    RETURN QUERY
    SELECT f."Id",f."UserId",f."Name",f."MonthlyAmount",f."DueDay",f."IsActive",f."IsPaid",f."PaidDate",f."CreatedAt"
    FROM "FixedLiabilities" f WHERE f."Id"=id AND f."UserId"=user_id;
END;
$$;

CREATE OR REPLACE FUNCTION sp_fixed_liabilities_mark_paid(id INT, user_id INT, is_paid BOOLEAN)
RETURNS VOID LANGUAGE plpgsql AS $$
BEGIN
    UPDATE "FixedLiabilities"
    SET "IsPaid"=is_paid, "PaidDate"=CASE WHEN is_paid THEN NOW() ELSE NULL END
    WHERE "Id"=id AND "UserId"=user_id;
END;
$$;

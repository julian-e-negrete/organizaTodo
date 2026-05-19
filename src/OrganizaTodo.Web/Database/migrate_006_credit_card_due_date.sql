-- ============================================================
-- Add DueMonth / DueYear to CreditCardPurchases
-- ============================================================

ALTER TABLE "CreditCardPurchases"
    ADD COLUMN IF NOT EXISTS "DueMonth" INT NOT NULL DEFAULT 1,
    ADD COLUMN IF NOT EXISTS "DueYear"  INT NOT NULL DEFAULT 2025;

-- Backfill existing rows: due date = month after purchase date
UPDATE "CreditCardPurchases"
SET
    "DueMonth" = CASE WHEN EXTRACT(MONTH FROM "PurchaseDate") = 12 THEN 1
                      ELSE EXTRACT(MONTH FROM "PurchaseDate")::INT + 1 END,
    "DueYear"  = CASE WHEN EXTRACT(MONTH FROM "PurchaseDate") = 12
                      THEN EXTRACT(YEAR FROM "PurchaseDate")::INT + 1
                      ELSE EXTRACT(YEAR FROM "PurchaseDate")::INT END;

-- ============================================================
-- Drop old signatures before recreating
-- (return type changed or parameter list changed)
-- ============================================================

DROP FUNCTION IF EXISTS sp_credit_card_get_by_id(integer, integer);
DROP FUNCTION IF EXISTS sp_credit_card_get_by_user_id(integer);
DROP FUNCTION IF EXISTS sp_credit_card_get_monthly_total(integer);
DROP FUNCTION IF EXISTS sp_credit_card_update(integer, integer, character varying, numeric, integer, numeric);
DROP FUNCTION IF EXISTS sp_credit_card_create(integer, character varying, numeric, integer, numeric, timestamp without time zone);

-- ============================================================
-- Updated functions
-- ============================================================

CREATE OR REPLACE FUNCTION sp_credit_card_create(
    user_id INT, description VARCHAR(200), total_amount DECIMAL(18,2),
    installments INT, interest_rate DECIMAL(5,2), purchase_date TIMESTAMP,
    due_month INT, due_year INT
) RETURNS VOID LANGUAGE plpgsql AS $$
BEGIN
    INSERT INTO "CreditCardPurchases"(
        "UserId","Description","TotalAmount","Installments","CurrentInstallment",
        "InterestRate","PurchaseDate","DueMonth","DueYear"
    ) VALUES (user_id, description, total_amount, installments, 1, interest_rate, purchase_date, due_month, due_year);
END;
$$;

CREATE OR REPLACE FUNCTION sp_credit_card_get_by_user_id(user_id INT, month INT, year INT)
RETURNS TABLE(
    "Id" INT,"UserId" INT,"Description" VARCHAR,"TotalAmount" DECIMAL,"Installments" INT,
    "CurrentInstallment" INT,"InterestRate" DECIMAL,"PurchaseDate" TIMESTAMP,"IsActive" BOOLEAN,
    "DueMonth" INT,"DueYear" INT,"MonthlyInstallmentAmount" DECIMAL
) LANGUAGE plpgsql AS $$
BEGIN
    RETURN QUERY
    SELECT c."Id",c."UserId",c."Description",c."TotalAmount",c."Installments",
           c."CurrentInstallment",c."InterestRate",c."PurchaseDate",c."IsActive",
           c."DueMonth",c."DueYear",
           CAST((c."TotalAmount"*(1+c."InterestRate"/100.0))/NULLIF(c."Installments",0) AS DECIMAL(18,2))
    FROM "CreditCardPurchases" c
    WHERE c."UserId"=user_id AND c."IsActive"=TRUE
      AND c."DueMonth"=month AND c."DueYear"=year
    ORDER BY c."PurchaseDate" DESC;
END;
$$;

CREATE OR REPLACE FUNCTION sp_credit_card_get_by_id(id INT, user_id INT)
RETURNS TABLE(
    "Id" INT,"UserId" INT,"Description" VARCHAR,"TotalAmount" DECIMAL,"Installments" INT,
    "CurrentInstallment" INT,"InterestRate" DECIMAL,"PurchaseDate" TIMESTAMP,"IsActive" BOOLEAN,
    "DueMonth" INT,"DueYear" INT
) LANGUAGE plpgsql AS $$
BEGIN
    RETURN QUERY
    SELECT c."Id",c."UserId",c."Description",c."TotalAmount",c."Installments",
           c."CurrentInstallment",c."InterestRate",c."PurchaseDate",c."IsActive",
           c."DueMonth",c."DueYear"
    FROM "CreditCardPurchases" c WHERE c."Id"=id AND c."UserId"=user_id;
END;
$$;

CREATE OR REPLACE FUNCTION sp_credit_card_update(
    id INT, user_id INT, description VARCHAR(200),
    total_amount DECIMAL(18,2), installments INT, interest_rate DECIMAL(5,2),
    due_month INT, due_year INT
) RETURNS VOID LANGUAGE plpgsql AS $$
BEGIN
    UPDATE "CreditCardPurchases"
    SET "Description"=description,"TotalAmount"=total_amount,
        "Installments"=installments,"InterestRate"=interest_rate,
        "DueMonth"=due_month,"DueYear"=due_year
    WHERE "Id"=id AND "UserId"=user_id;
END;
$$;

CREATE OR REPLACE FUNCTION sp_credit_card_advance_installment(id INT, user_id INT)
RETURNS VOID LANGUAGE plpgsql AS $$
DECLARE
    v_due_month INT;
    v_due_year  INT;
BEGIN
    SELECT "DueMonth", "DueYear" INTO v_due_month, v_due_year
    FROM "CreditCardPurchases" WHERE "Id" = id AND "UserId" = user_id;

    UPDATE "CreditCardPurchases"
    SET
        "CurrentInstallment" = "CurrentInstallment" + 1,
        "DueMonth" = CASE WHEN v_due_month = 12 THEN 1 ELSE v_due_month + 1 END,
        "DueYear"  = CASE WHEN v_due_month = 12 THEN v_due_year + 1 ELSE v_due_year END,
        "IsActive" = CASE WHEN "CurrentInstallment" + 1 > "Installments" THEN FALSE ELSE TRUE END
    WHERE "Id" = id AND "UserId" = user_id;
END;
$$;

CREATE OR REPLACE FUNCTION sp_credit_card_get_monthly_total(user_id INT, month INT, year INT)
RETURNS DECIMAL(18,2) LANGUAGE plpgsql AS $$
DECLARE total DECIMAL(18,2);
BEGIN
    SELECT COALESCE(SUM(
        ("TotalAmount"*(1+"InterestRate"/100.0))/NULLIF("Installments",0)
    ),0) INTO total
    FROM "CreditCardPurchases"
    WHERE "UserId"=user_id AND "IsActive"=TRUE
      AND "DueMonth"=month AND "DueYear"=year;
    RETURN total;
END;
$$;

-- Update sp_balance_get_monthly: filter CC by due month/year, add vehicle costs
DROP FUNCTION IF EXISTS sp_balance_get_monthly(integer, integer, integer);

CREATE OR REPLACE FUNCTION sp_balance_get_monthly(user_id INT, month INT, year INT)
RETURNS TABLE(
    "TotalIncome"           DECIMAL(18,2),
    "TotalServices"         DECIMAL(18,2),
    "TotalCreditCard"       DECIMAL(18,2),
    "TotalFixedLiabilities" DECIMAL(18,2),
    "TotalVehicles"         DECIMAL(18,2),
    "TotalLiabilities"      DECIMAL(18,2),
    "TotalOtherExpenses"    DECIMAL(18,2),
    "RemainingBalance"      DECIMAL(18,2)
) LANGUAGE plpgsql AS $$
DECLARE
    v_income   DECIMAL(18,2);
    v_services DECIMAL(18,2);
    v_cc       DECIMAL(18,2);
    v_fixed    DECIMAL(18,2);
    v_vehicles DECIMAL(18,2);
    v_expenses DECIMAL(18,2);
BEGIN
    SELECT COALESCE(SUM("Amount"),0) INTO v_income
    FROM "Income" WHERE "UserId"=user_id AND "Month"=month AND "Year"=year;

    SELECT COALESCE(SUM("Amount"),0) INTO v_services
    FROM "HousingServices"
    WHERE "UserId"=user_id AND "IsActive"=TRUE AND "Periodicity"='MONTHLY';

    SELECT COALESCE(SUM(
        ("TotalAmount"*(1+"InterestRate"/100.0))/NULLIF("Installments",0)
    ),0) INTO v_cc
    FROM "CreditCardPurchases"
    WHERE "UserId"=user_id AND "IsActive"=TRUE
      AND "DueMonth"=month AND "DueYear"=year;

    SELECT COALESCE(SUM("MonthlyAmount"),0) INTO v_fixed
    FROM "FixedLiabilities" WHERE "UserId"=user_id AND "IsActive"=TRUE;

    SELECT COALESCE(SUM(
        COALESCE(CASE WHEN "FuelPricePerLiter" IS NOT NULL AND "FuelEfficiencyKmL" > 0
            THEN ("WeeklyKm" * 52.0 / 12.0) / "FuelEfficiencyKmL" * "FuelPricePerLiter"
            ELSE 0 END, 0) +
        COALESCE("InsuranceMonthly", 0) +
        COALESCE("PatenteAnnual" / 12.0, 0)
    ), 0) INTO v_vehicles
    FROM "Vehicles" WHERE "UserId"=user_id AND "IsActive"=TRUE;

    SELECT COALESCE(SUM("Amount"),0) INTO v_expenses
    FROM "OtherExpenses" WHERE "UserId"=user_id AND "Month"=month AND "Year"=year;

    RETURN QUERY SELECT
        v_income,
        v_services,
        v_cc,
        v_fixed,
        ROUND(v_vehicles, 2),
        v_services + v_cc + v_fixed + ROUND(v_vehicles, 2),
        v_expenses,
        v_income - (v_services + v_cc + v_fixed + ROUND(v_vehicles, 2)) - v_expenses;
END;
$$;

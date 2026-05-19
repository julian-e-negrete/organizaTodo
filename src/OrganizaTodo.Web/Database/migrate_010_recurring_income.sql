-- ============================================================
-- TABLE – Recurring Incomes (Sueldo / Ingresos Fijos)
-- ============================================================

CREATE TABLE IF NOT EXISTS "RecurringIncomes" (
    "Id"          SERIAL PRIMARY KEY,
    "UserId"      INT NOT NULL REFERENCES "Users"("Id") ON DELETE CASCADE,
    "Description" VARCHAR(200) NOT NULL,
    "Amount"      NUMERIC(18,2) NOT NULL,
    "IsActive"    BOOLEAN NOT NULL DEFAULT TRUE,
    "CreatedAt"   TIMESTAMP NOT NULL DEFAULT NOW()
);

-- ============================================================
-- FUNCTIONS – Recurring Incomes
-- ============================================================

CREATE OR REPLACE FUNCTION sp_recurring_income_create(
    p_user_id     INT,
    p_description VARCHAR,
    p_amount      NUMERIC
) RETURNS VOID LANGUAGE plpgsql AS $$
BEGIN
    INSERT INTO "RecurringIncomes"("UserId","Description","Amount")
    VALUES (p_user_id, p_description, p_amount);
END;
$$;

CREATE OR REPLACE FUNCTION sp_recurring_income_get_by_user_id(p_user_id INT)
RETURNS TABLE(
    "Id" INT, "UserId" INT, "Description" VARCHAR,
    "Amount" NUMERIC, "IsActive" BOOLEAN, "CreatedAt" TIMESTAMP
) LANGUAGE plpgsql AS $$
BEGIN
    RETURN QUERY
    SELECT r."Id", r."UserId", r."Description", r."Amount", r."IsActive", r."CreatedAt"
    FROM "RecurringIncomes" r
    WHERE r."UserId" = p_user_id
    ORDER BY r."IsActive" DESC, r."Description";
END;
$$;

CREATE OR REPLACE FUNCTION sp_recurring_income_get_by_id(p_id INT, p_user_id INT)
RETURNS TABLE(
    "Id" INT, "UserId" INT, "Description" VARCHAR,
    "Amount" NUMERIC, "IsActive" BOOLEAN, "CreatedAt" TIMESTAMP
) LANGUAGE plpgsql AS $$
BEGIN
    RETURN QUERY
    SELECT r."Id", r."UserId", r."Description", r."Amount", r."IsActive", r."CreatedAt"
    FROM "RecurringIncomes" r
    WHERE r."Id" = p_id AND r."UserId" = p_user_id;
END;
$$;

CREATE OR REPLACE FUNCTION sp_recurring_income_update(
    p_id          INT,
    p_user_id     INT,
    p_description VARCHAR,
    p_amount      NUMERIC
) RETURNS VOID LANGUAGE plpgsql AS $$
BEGIN
    UPDATE "RecurringIncomes"
    SET "Description" = p_description, "Amount" = p_amount
    WHERE "Id" = p_id AND "UserId" = p_user_id;
END;
$$;

CREATE OR REPLACE FUNCTION sp_recurring_income_toggle_active(p_id INT, p_user_id INT)
RETURNS VOID LANGUAGE plpgsql AS $$
BEGIN
    UPDATE "RecurringIncomes"
    SET "IsActive" = NOT "IsActive"
    WHERE "Id" = p_id AND "UserId" = p_user_id;
END;
$$;

CREATE OR REPLACE FUNCTION sp_recurring_income_delete(p_id INT, p_user_id INT)
RETURNS VOID LANGUAGE plpgsql AS $$
BEGIN
    DELETE FROM "RecurringIncomes" WHERE "Id" = p_id AND "UserId" = p_user_id;
END;
$$;

CREATE OR REPLACE FUNCTION sp_recurring_income_get_total(p_user_id INT)
RETURNS NUMERIC LANGUAGE plpgsql AS $$
DECLARE total NUMERIC;
BEGIN
    SELECT COALESCE(SUM("Amount"), 0) INTO total
    FROM "RecurringIncomes"
    WHERE "UserId" = p_user_id AND "IsActive" = TRUE;
    RETURN ROUND(total, 2);
END;
$$;

-- ============================================================
-- Update sp_balance_get_monthly — TotalIncome now includes
-- recurring income + one-off income for the month
-- ============================================================

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
    -- One-off income for the month
    SELECT COALESCE(SUM("Amount"), 0) INTO v_income
    FROM "Income" WHERE "UserId"=user_id AND "Month"=month AND "Year"=year;

    -- Add active recurring income (salary, etc.)
    SELECT v_income + COALESCE(SUM("Amount"), 0) INTO v_income
    FROM "RecurringIncomes" WHERE "UserId"=user_id AND "IsActive"=TRUE;

    SELECT COALESCE(SUM("Amount"), 0) INTO v_services
    FROM "HousingServices"
    WHERE "UserId"=user_id AND "IsActive"=TRUE AND "Periodicity"='MONTHLY';

    SELECT COALESCE(SUM(
        ("TotalAmount"*(1+"InterestRate"/100.0))/NULLIF("Installments",0)
    ), 0) INTO v_cc
    FROM "CreditCardPurchases"
    WHERE "UserId"=user_id AND "IsActive"=TRUE
      AND "DueMonth"=month AND "DueYear"=year;

    SELECT COALESCE(SUM("MonthlyAmount"), 0) INTO v_fixed
    FROM "FixedLiabilities" WHERE "UserId"=user_id AND "IsActive"=TRUE;

    SELECT COALESCE(SUM(
        COALESCE(CASE WHEN "FuelPricePerLiter" IS NOT NULL AND "FuelEfficiencyKmL" > 0
            THEN ("WeeklyKm" * 52.0 / 12.0) / "FuelEfficiencyKmL" * "FuelPricePerLiter"
            ELSE 0 END, 0) +
        COALESCE("InsuranceMonthly", 0) +
        COALESCE("PatenteAnnual" / 12.0, 0) +
        CASE
            WHEN "OilChangeCostEstimate" IS NOT NULL
                 AND "OilChangeIntervalKm" IS NOT NULL
                 AND "LastOilChangeDate" IS NOT NULL
                 AND "WeeklyKm" > 0
                 AND EXTRACT(MONTH FROM ("LastOilChangeDate" + ("OilChangeIntervalKm" / "WeeklyKm" * 7.0)::INT)) = month
                 AND EXTRACT(YEAR  FROM ("LastOilChangeDate" + ("OilChangeIntervalKm" / "WeeklyKm" * 7.0)::INT)) = year
            THEN "OilChangeCostEstimate"
            ELSE 0
        END
    ), 0) INTO v_vehicles
    FROM "Vehicles" WHERE "UserId"=user_id AND "IsActive"=TRUE;

    SELECT COALESCE(SUM("Amount"), 0) INTO v_expenses
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

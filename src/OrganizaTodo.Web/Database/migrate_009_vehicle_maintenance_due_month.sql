-- ============================================================
-- Change oil change cost from amortized-monthly to due-month only.
-- MonthlyOilChangeCost is now OilChangeCostEstimate (full amount)
-- but only non-zero in the month when NextOilChangeDueDate falls.
-- ============================================================

-- No DROP needed — same param list and return type, just updated body.

CREATE OR REPLACE FUNCTION sp_vehicles_get_by_user_id(p_user_id INT, p_month INT, p_year INT)
RETURNS TABLE(
    "Id" INT, "UserId" INT, "Plate" VARCHAR, "Make" VARCHAR, "Model" VARCHAR, "Year" INT,
    "WeeklyKm" NUMERIC, "FuelEfficiencyKmL" NUMERIC, "FuelPricePerLiter" NUMERIC,
    "InsuranceMonthly" NUMERIC, "PatenteAnnual" NUMERIC,
    "IsActive" BOOLEAN, "CreatedAt" TIMESTAMP,
    "MonthlyFuelCost" NUMERIC, "MonthlyPatente" NUMERIC, "TotalMonthlyCost" NUMERIC,
    "FuelPaid" BOOLEAN, "InsurancePaid" BOOLEAN, "PatentePaid" BOOLEAN,
    "OilChangeIntervalKm" NUMERIC, "OilChangeCostEstimate" NUMERIC,
    "LastOilChangeDate" DATE, "LastOilChangeKm" NUMERIC,
    "MonthlyOilChangeCost" NUMERIC,
    "KmSinceLastChange" NUMERIC, "KmToNextChange" NUMERIC,
    "NextOilChangeDueDate" DATE,
    "MaintenancePaid" BOOLEAN
) LANGUAGE plpgsql AS $$
BEGIN
    RETURN QUERY
    SELECT
        v."Id", v."UserId", v."Plate", v."Make", v."Model", v."Year",
        v."WeeklyKm", v."FuelEfficiencyKmL", v."FuelPricePerLiter",
        v."InsuranceMonthly", v."PatenteAnnual",
        v."IsActive", v."CreatedAt",
        -- MonthlyFuelCost
        CASE WHEN v."FuelPricePerLiter" IS NOT NULL AND v."FuelEfficiencyKmL" > 0
            THEN ROUND((v."WeeklyKm" * 52.0 / 12.0) / v."FuelEfficiencyKmL" * v."FuelPricePerLiter", 2)
            ELSE 0
        END::NUMERIC AS "MonthlyFuelCost",
        -- MonthlyPatente
        ROUND(COALESCE(v."PatenteAnnual", 0) / 12.0, 2)::NUMERIC AS "MonthlyPatente",
        -- TotalMonthlyCost — oil change only counted in the due month
        ROUND(
            COALESCE(CASE WHEN v."FuelPricePerLiter" IS NOT NULL AND v."FuelEfficiencyKmL" > 0
                THEN (v."WeeklyKm" * 52.0 / 12.0) / v."FuelEfficiencyKmL" * v."FuelPricePerLiter"
                ELSE 0 END, 0) +
            COALESCE(v."InsuranceMonthly", 0) +
            COALESCE(v."PatenteAnnual" / 12.0, 0) +
            CASE
                WHEN v."OilChangeCostEstimate" IS NOT NULL
                     AND v."OilChangeIntervalKm" IS NOT NULL
                     AND v."LastOilChangeDate" IS NOT NULL
                     AND v."WeeklyKm" > 0
                     AND EXTRACT(MONTH FROM (v."LastOilChangeDate" + (v."OilChangeIntervalKm" / v."WeeklyKm" * 7.0)::INT)) = p_month
                     AND EXTRACT(YEAR  FROM (v."LastOilChangeDate" + (v."OilChangeIntervalKm" / v."WeeklyKm" * 7.0)::INT)) = p_year
                THEN v."OilChangeCostEstimate"
                ELSE 0
            END
        , 2)::NUMERIC AS "TotalMonthlyCost",
        -- Payment flags
        EXISTS(SELECT 1 FROM "VehicleMonthlyPayments" p
               WHERE p."VehicleId"=v."Id" AND p."Month"=p_month AND p."Year"=p_year
               AND p."PaymentType"='FUEL') AS "FuelPaid",
        EXISTS(SELECT 1 FROM "VehicleMonthlyPayments" p
               WHERE p."VehicleId"=v."Id" AND p."Month"=p_month AND p."Year"=p_year
               AND p."PaymentType"='INSURANCE') AS "InsurancePaid",
        EXISTS(SELECT 1 FROM "VehicleMonthlyPayments" p
               WHERE p."VehicleId"=v."Id" AND p."Month"=p_month AND p."Year"=p_year
               AND p."PaymentType"='PATENTE') AS "PatentePaid",
        -- Oil change fields
        v."OilChangeIntervalKm",
        v."OilChangeCostEstimate",
        v."LastOilChangeDate",
        v."LastOilChangeKm",
        -- MonthlyOilChangeCost: full amount ONLY in the month the change is due
        CASE
            WHEN v."OilChangeCostEstimate" IS NOT NULL
                 AND v."OilChangeIntervalKm" IS NOT NULL
                 AND v."LastOilChangeDate" IS NOT NULL
                 AND v."WeeklyKm" > 0
                 AND EXTRACT(MONTH FROM (v."LastOilChangeDate" + (v."OilChangeIntervalKm" / v."WeeklyKm" * 7.0)::INT)) = p_month
                 AND EXTRACT(YEAR  FROM (v."LastOilChangeDate" + (v."OilChangeIntervalKm" / v."WeeklyKm" * 7.0)::INT)) = p_year
            THEN v."OilChangeCostEstimate"
            ELSE 0
        END::NUMERIC AS "MonthlyOilChangeCost",
        -- Km tracking
        CASE WHEN v."LastOilChangeDate" IS NOT NULL
            THEN ROUND((CURRENT_DATE - v."LastOilChangeDate")::NUMERIC / 7.0 * v."WeeklyKm", 0)
            ELSE NULL
        END::NUMERIC AS "KmSinceLastChange",
        CASE WHEN v."LastOilChangeDate" IS NOT NULL AND v."OilChangeIntervalKm" IS NOT NULL
            THEN ROUND(v."OilChangeIntervalKm" - (CURRENT_DATE - v."LastOilChangeDate")::NUMERIC / 7.0 * v."WeeklyKm", 0)
            ELSE NULL
        END::NUMERIC AS "KmToNextChange",
        CASE WHEN v."LastOilChangeDate" IS NOT NULL AND v."OilChangeIntervalKm" IS NOT NULL AND v."WeeklyKm" > 0
            THEN (v."LastOilChangeDate" + (v."OilChangeIntervalKm" / v."WeeklyKm" * 7.0)::INT)
            ELSE NULL
        END::DATE AS "NextOilChangeDueDate",
        EXISTS(SELECT 1 FROM "VehicleMonthlyPayments" p
               WHERE p."VehicleId"=v."Id" AND p."Month"=p_month AND p."Year"=p_year
               AND p."PaymentType"='MAINTENANCE') AS "MaintenancePaid"
    FROM "Vehicles" v
    WHERE v."UserId" = p_user_id AND v."IsActive" = TRUE
    ORDER BY v."Make", v."Model";
END;
$$;

-- ============================================================
-- sp_balance_get_monthly — oil change cost only in due month
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

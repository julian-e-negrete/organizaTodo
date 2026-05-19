-- ============================================================
-- Add maintenance tracking columns to Vehicles
-- ============================================================

ALTER TABLE "Vehicles"
    ADD COLUMN IF NOT EXISTS "OilChangeIntervalKm"   NUMERIC(10,2),
    ADD COLUMN IF NOT EXISTS "OilChangeCostEstimate" NUMERIC(18,2),
    ADD COLUMN IF NOT EXISTS "LastOilChangeDate"     DATE,
    ADD COLUMN IF NOT EXISTS "LastOilChangeKm"       NUMERIC(10,2);

-- ============================================================
-- TABLE – Vehicle Maintenance Logs
-- ============================================================

CREATE TABLE IF NOT EXISTS "VehicleMaintenanceLogs" (
    "Id"          SERIAL PRIMARY KEY,
    "VehicleId"   INT NOT NULL REFERENCES "Vehicles"("Id") ON DELETE CASCADE,
    "UserId"      INT NOT NULL REFERENCES "Users"("Id"),
    "ServiceDate" DATE NOT NULL,
    "KmAtService" NUMERIC(10,2) NOT NULL,
    "Cost"        NUMERIC(18,2),
    "Notes"       TEXT,
    "CreatedAt"   TIMESTAMP NOT NULL DEFAULT NOW()
);

-- ============================================================
-- Drop functions whose return type or param list changes
-- ============================================================

DROP FUNCTION IF EXISTS sp_vehicles_get_by_user_id(integer, integer, integer);
DROP FUNCTION IF EXISTS sp_vehicles_get_by_id(integer, integer);
DROP FUNCTION IF EXISTS sp_vehicles_create(integer, character varying, character varying, character varying, integer, numeric, numeric, numeric, numeric, numeric);
DROP FUNCTION IF EXISTS sp_vehicles_update(integer, integer, character varying, character varying, character varying, integer, numeric, numeric, numeric, numeric, numeric);

-- ============================================================
-- sp_vehicles_create — includes oil change fields
-- ============================================================

CREATE OR REPLACE FUNCTION sp_vehicles_create(
    p_user_id                INT,
    p_plate                  VARCHAR,
    p_make                   VARCHAR,
    p_model                  VARCHAR,
    p_year                   INT,
    p_weekly_km              NUMERIC,
    p_fuel_efficiency_kml    NUMERIC,
    p_fuel_price             NUMERIC,
    p_insurance_monthly      NUMERIC,
    p_patente_annual         NUMERIC,
    p_oil_change_interval_km NUMERIC,
    p_oil_change_cost        NUMERIC,
    p_last_oil_change_date   DATE,
    p_last_oil_change_km     NUMERIC
) RETURNS VOID LANGUAGE plpgsql AS $$
BEGIN
    INSERT INTO "Vehicles"(
        "UserId","Plate","Make","Model","Year",
        "WeeklyKm","FuelEfficiencyKmL","FuelPricePerLiter",
        "InsuranceMonthly","PatenteAnnual",
        "OilChangeIntervalKm","OilChangeCostEstimate",
        "LastOilChangeDate","LastOilChangeKm"
    ) VALUES (
        p_user_id, p_plate, p_make, p_model, p_year,
        p_weekly_km, p_fuel_efficiency_kml, p_fuel_price,
        p_insurance_monthly, p_patente_annual,
        p_oil_change_interval_km, p_oil_change_cost,
        p_last_oil_change_date, p_last_oil_change_km
    );
END;
$$;

-- ============================================================
-- sp_vehicles_update — includes oil change fields
-- ============================================================

CREATE OR REPLACE FUNCTION sp_vehicles_update(
    p_id                     INT,
    p_user_id                INT,
    p_plate                  VARCHAR,
    p_make                   VARCHAR,
    p_model                  VARCHAR,
    p_year                   INT,
    p_weekly_km              NUMERIC,
    p_fuel_efficiency_kml    NUMERIC,
    p_fuel_price             NUMERIC,
    p_insurance_monthly      NUMERIC,
    p_patente_annual         NUMERIC,
    p_oil_change_interval_km NUMERIC,
    p_oil_change_cost        NUMERIC,
    p_last_oil_change_date   DATE,
    p_last_oil_change_km     NUMERIC
) RETURNS VOID LANGUAGE plpgsql AS $$
BEGIN
    UPDATE "Vehicles" SET
        "Plate"                 = p_plate,
        "Make"                  = p_make,
        "Model"                 = p_model,
        "Year"                  = p_year,
        "WeeklyKm"              = p_weekly_km,
        "FuelEfficiencyKmL"     = p_fuel_efficiency_kml,
        "FuelPricePerLiter"     = p_fuel_price,
        "InsuranceMonthly"      = p_insurance_monthly,
        "PatenteAnnual"         = p_patente_annual,
        "OilChangeIntervalKm"   = p_oil_change_interval_km,
        "OilChangeCostEstimate" = p_oil_change_cost,
        "LastOilChangeDate"     = p_last_oil_change_date,
        "LastOilChangeKm"       = p_last_oil_change_km
    WHERE "Id" = p_id AND "UserId" = p_user_id;
END;
$$;

-- ============================================================
-- sp_vehicles_get_by_user_id — adds maintenance computed cols
-- ============================================================

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
        CASE WHEN v."FuelPricePerLiter" IS NOT NULL AND v."FuelEfficiencyKmL" > 0
            THEN ROUND((v."WeeklyKm" * 52.0 / 12.0) / v."FuelEfficiencyKmL" * v."FuelPricePerLiter", 2)
            ELSE 0
        END::NUMERIC AS "MonthlyFuelCost",
        ROUND(COALESCE(v."PatenteAnnual", 0) / 12.0, 2)::NUMERIC AS "MonthlyPatente",
        ROUND(
            COALESCE(CASE WHEN v."FuelPricePerLiter" IS NOT NULL AND v."FuelEfficiencyKmL" > 0
                THEN (v."WeeklyKm" * 52.0 / 12.0) / v."FuelEfficiencyKmL" * v."FuelPricePerLiter"
                ELSE 0 END, 0) +
            COALESCE(v."InsuranceMonthly", 0) +
            COALESCE(v."PatenteAnnual" / 12.0, 0) +
            COALESCE(CASE WHEN v."OilChangeCostEstimate" IS NOT NULL AND v."OilChangeIntervalKm" > 0
                THEN v."OilChangeCostEstimate" * (v."WeeklyKm" * 52.0 / 12.0) / v."OilChangeIntervalKm"
                ELSE 0 END, 0)
        , 2)::NUMERIC AS "TotalMonthlyCost",
        EXISTS(SELECT 1 FROM "VehicleMonthlyPayments" p
               WHERE p."VehicleId"=v."Id" AND p."Month"=p_month AND p."Year"=p_year
               AND p."PaymentType"='FUEL') AS "FuelPaid",
        EXISTS(SELECT 1 FROM "VehicleMonthlyPayments" p
               WHERE p."VehicleId"=v."Id" AND p."Month"=p_month AND p."Year"=p_year
               AND p."PaymentType"='INSURANCE') AS "InsurancePaid",
        EXISTS(SELECT 1 FROM "VehicleMonthlyPayments" p
               WHERE p."VehicleId"=v."Id" AND p."Month"=p_month AND p."Year"=p_year
               AND p."PaymentType"='PATENTE') AS "PatentePaid",
        v."OilChangeIntervalKm",
        v."OilChangeCostEstimate",
        v."LastOilChangeDate",
        v."LastOilChangeKm",
        CASE WHEN v."OilChangeCostEstimate" IS NOT NULL AND v."OilChangeIntervalKm" IS NOT NULL AND v."OilChangeIntervalKm" > 0
            THEN ROUND(v."OilChangeCostEstimate" * (v."WeeklyKm" * 52.0 / 12.0) / v."OilChangeIntervalKm", 2)
            ELSE 0
        END::NUMERIC AS "MonthlyOilChangeCost",
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
-- sp_vehicles_get_by_id — same expanded return type
-- ============================================================

CREATE OR REPLACE FUNCTION sp_vehicles_get_by_id(p_id INT, p_user_id INT)
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
        CASE WHEN v."FuelPricePerLiter" IS NOT NULL AND v."FuelEfficiencyKmL" > 0
            THEN ROUND((v."WeeklyKm" * 52.0 / 12.0) / v."FuelEfficiencyKmL" * v."FuelPricePerLiter", 2)
            ELSE 0
        END::NUMERIC,
        ROUND(COALESCE(v."PatenteAnnual", 0) / 12.0, 2)::NUMERIC,
        ROUND(
            COALESCE(CASE WHEN v."FuelPricePerLiter" IS NOT NULL AND v."FuelEfficiencyKmL" > 0
                THEN (v."WeeklyKm" * 52.0 / 12.0) / v."FuelEfficiencyKmL" * v."FuelPricePerLiter"
                ELSE 0 END, 0) +
            COALESCE(v."InsuranceMonthly", 0) +
            COALESCE(v."PatenteAnnual" / 12.0, 0) +
            COALESCE(CASE WHEN v."OilChangeCostEstimate" IS NOT NULL AND v."OilChangeIntervalKm" > 0
                THEN v."OilChangeCostEstimate" * (v."WeeklyKm" * 52.0 / 12.0) / v."OilChangeIntervalKm"
                ELSE 0 END, 0)
        , 2)::NUMERIC,
        FALSE::BOOLEAN, FALSE::BOOLEAN, FALSE::BOOLEAN,
        v."OilChangeIntervalKm",
        v."OilChangeCostEstimate",
        v."LastOilChangeDate",
        v."LastOilChangeKm",
        CASE WHEN v."OilChangeCostEstimate" IS NOT NULL AND v."OilChangeIntervalKm" IS NOT NULL AND v."OilChangeIntervalKm" > 0
            THEN ROUND(v."OilChangeCostEstimate" * (v."WeeklyKm" * 52.0 / 12.0) / v."OilChangeIntervalKm", 2)
            ELSE 0
        END::NUMERIC,
        CASE WHEN v."LastOilChangeDate" IS NOT NULL
            THEN ROUND((CURRENT_DATE - v."LastOilChangeDate")::NUMERIC / 7.0 * v."WeeklyKm", 0)
            ELSE NULL
        END::NUMERIC,
        CASE WHEN v."LastOilChangeDate" IS NOT NULL AND v."OilChangeIntervalKm" IS NOT NULL
            THEN ROUND(v."OilChangeIntervalKm" - (CURRENT_DATE - v."LastOilChangeDate")::NUMERIC / 7.0 * v."WeeklyKm", 0)
            ELSE NULL
        END::NUMERIC,
        CASE WHEN v."LastOilChangeDate" IS NOT NULL AND v."OilChangeIntervalKm" IS NOT NULL AND v."WeeklyKm" > 0
            THEN (v."LastOilChangeDate" + (v."OilChangeIntervalKm" / v."WeeklyKm" * 7.0)::INT)
            ELSE NULL
        END::DATE,
        FALSE::BOOLEAN
    FROM "Vehicles" v
    WHERE v."Id" = p_id AND v."UserId" = p_user_id;
END;
$$;

-- ============================================================
-- sp_vehicle_maintenance_log_create
-- ============================================================

CREATE OR REPLACE FUNCTION sp_vehicle_maintenance_log_create(
    p_vehicle_id    INT,
    p_user_id       INT,
    p_service_date  DATE,
    p_km_at_service NUMERIC,
    p_cost          NUMERIC,
    p_notes         TEXT
) RETURNS VOID LANGUAGE plpgsql AS $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM "Vehicles"
        WHERE "Id"=p_vehicle_id AND "UserId"=p_user_id AND "IsActive"=TRUE
    ) THEN RETURN; END IF;

    INSERT INTO "VehicleMaintenanceLogs"(
        "VehicleId","UserId","ServiceDate","KmAtService","Cost","Notes"
    ) VALUES (p_vehicle_id, p_user_id, p_service_date, p_km_at_service, p_cost, p_notes);

    -- Update vehicle's last oil change fields only if this is the most recent service date
    UPDATE "Vehicles"
    SET "LastOilChangeDate" = p_service_date,
        "LastOilChangeKm"   = p_km_at_service
    WHERE "Id" = p_vehicle_id AND "UserId" = p_user_id
      AND ("LastOilChangeDate" IS NULL OR p_service_date >= "LastOilChangeDate");
END;
$$;

-- ============================================================
-- sp_vehicle_maintenance_logs_get
-- ============================================================

CREATE OR REPLACE FUNCTION sp_vehicle_maintenance_logs_get(p_vehicle_id INT, p_user_id INT)
RETURNS TABLE(
    "Id" INT, "VehicleId" INT, "UserId" INT,
    "ServiceDate" DATE, "KmAtService" NUMERIC,
    "Cost" NUMERIC, "Notes" TEXT, "CreatedAt" TIMESTAMP
) LANGUAGE plpgsql AS $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM "Vehicles" WHERE "Id"=p_vehicle_id AND "UserId"=p_user_id
    ) THEN RETURN; END IF;

    RETURN QUERY
    SELECT l."Id", l."VehicleId", l."UserId",
           l."ServiceDate", l."KmAtService",
           l."Cost", l."Notes", l."CreatedAt"
    FROM "VehicleMaintenanceLogs" l
    WHERE l."VehicleId" = p_vehicle_id AND l."UserId" = p_user_id
    ORDER BY l."ServiceDate" DESC;
END;
$$;

-- ============================================================
-- Update sp_balance_get_monthly — include oil change amortization in vehicles total
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
        COALESCE(CASE WHEN "OilChangeCostEstimate" IS NOT NULL AND "OilChangeIntervalKm" > 0
            THEN "OilChangeCostEstimate" * ("WeeklyKm" * 52.0 / 12.0) / "OilChangeIntervalKm"
            ELSE 0 END, 0)
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

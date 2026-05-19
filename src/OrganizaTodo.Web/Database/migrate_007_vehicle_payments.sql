-- ============================================================
-- TABLE – Vehicle Monthly Payments
-- ============================================================

CREATE TABLE IF NOT EXISTS "VehicleMonthlyPayments" (
    "Id"          SERIAL PRIMARY KEY,
    "VehicleId"   INT NOT NULL REFERENCES "Vehicles"("Id") ON DELETE CASCADE,
    "Month"       INT NOT NULL,
    "Year"        INT NOT NULL,
    "PaymentType" VARCHAR(20) NOT NULL,  -- 'FUEL', 'INSURANCE', 'PATENTE'
    "PaidAt"      TIMESTAMP NOT NULL DEFAULT NOW(),
    UNIQUE("VehicleId", "Month", "Year", "PaymentType")
);

-- ============================================================
-- Drop old single-param signature before replacing with month/year version
-- ============================================================

DROP FUNCTION IF EXISTS sp_vehicles_get_by_user_id(integer);
DROP FUNCTION IF EXISTS sp_vehicles_get_by_id(integer, integer);

-- ============================================================
-- Updated sp_vehicles_get_by_user_id — includes paid status
-- ============================================================

CREATE OR REPLACE FUNCTION sp_vehicles_get_by_user_id(p_user_id INT, p_month INT, p_year INT)
RETURNS TABLE(
    "Id" INT, "UserId" INT, "Plate" VARCHAR, "Make" VARCHAR, "Model" VARCHAR, "Year" INT,
    "WeeklyKm" NUMERIC, "FuelEfficiencyKmL" NUMERIC, "FuelPricePerLiter" NUMERIC,
    "InsuranceMonthly" NUMERIC, "PatenteAnnual" NUMERIC,
    "IsActive" BOOLEAN, "CreatedAt" TIMESTAMP,
    "MonthlyFuelCost" NUMERIC, "MonthlyPatente" NUMERIC, "TotalMonthlyCost" NUMERIC,
    "FuelPaid" BOOLEAN, "InsurancePaid" BOOLEAN, "PatentePaid" BOOLEAN
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
            COALESCE(v."PatenteAnnual" / 12.0, 0)
        , 2)::NUMERIC AS "TotalMonthlyCost",
        EXISTS(SELECT 1 FROM "VehicleMonthlyPayments" p
               WHERE p."VehicleId"=v."Id" AND p."Month"=p_month AND p."Year"=p_year
               AND p."PaymentType"='FUEL') AS "FuelPaid",
        EXISTS(SELECT 1 FROM "VehicleMonthlyPayments" p
               WHERE p."VehicleId"=v."Id" AND p."Month"=p_month AND p."Year"=p_year
               AND p."PaymentType"='INSURANCE') AS "InsurancePaid",
        EXISTS(SELECT 1 FROM "VehicleMonthlyPayments" p
               WHERE p."VehicleId"=v."Id" AND p."Month"=p_month AND p."Year"=p_year
               AND p."PaymentType"='PATENTE') AS "PatentePaid"
    FROM "Vehicles" v
    WHERE v."UserId" = p_user_id AND v."IsActive" = TRUE
    ORDER BY v."Make", v."Model";
END;
$$;

-- ============================================================
-- Recreate sp_vehicles_get_by_id (same data, keeps consistency)
-- ============================================================

CREATE OR REPLACE FUNCTION sp_vehicles_get_by_id(p_id INT, p_user_id INT)
RETURNS TABLE(
    "Id" INT, "UserId" INT, "Plate" VARCHAR, "Make" VARCHAR, "Model" VARCHAR, "Year" INT,
    "WeeklyKm" NUMERIC, "FuelEfficiencyKmL" NUMERIC, "FuelPricePerLiter" NUMERIC,
    "InsuranceMonthly" NUMERIC, "PatenteAnnual" NUMERIC,
    "IsActive" BOOLEAN, "CreatedAt" TIMESTAMP,
    "MonthlyFuelCost" NUMERIC, "MonthlyPatente" NUMERIC, "TotalMonthlyCost" NUMERIC,
    "FuelPaid" BOOLEAN, "InsurancePaid" BOOLEAN, "PatentePaid" BOOLEAN
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
            COALESCE(v."PatenteAnnual" / 12.0, 0)
        , 2)::NUMERIC,
        FALSE::BOOLEAN, FALSE::BOOLEAN, FALSE::BOOLEAN
    FROM "Vehicles" v
    WHERE v."Id" = p_id AND v."UserId" = p_user_id;
END;
$$;

-- ============================================================
-- Toggle payment paid/unpaid for a vehicle cost
-- ============================================================

CREATE OR REPLACE FUNCTION sp_vehicle_payments_toggle(
    p_vehicle_id   INT,
    p_user_id      INT,
    p_month        INT,
    p_year         INT,
    p_payment_type VARCHAR
) RETURNS VOID LANGUAGE plpgsql AS $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM "Vehicles"
        WHERE "Id"=p_vehicle_id AND "UserId"=p_user_id AND "IsActive"=TRUE
    ) THEN
        RETURN;
    END IF;

    IF EXISTS (
        SELECT 1 FROM "VehicleMonthlyPayments"
        WHERE "VehicleId"=p_vehicle_id AND "Month"=p_month
          AND "Year"=p_year AND "PaymentType"=p_payment_type
    ) THEN
        DELETE FROM "VehicleMonthlyPayments"
        WHERE "VehicleId"=p_vehicle_id AND "Month"=p_month
          AND "Year"=p_year AND "PaymentType"=p_payment_type;
    ELSE
        INSERT INTO "VehicleMonthlyPayments"("VehicleId","Month","Year","PaymentType")
        VALUES (p_vehicle_id, p_month, p_year, p_payment_type);
    END IF;
END;
$$;

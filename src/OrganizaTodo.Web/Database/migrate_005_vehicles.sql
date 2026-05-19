-- ============================================================
-- TABLE – Vehicles
-- ============================================================

CREATE TABLE IF NOT EXISTS "Vehicles" (
    "Id"                  SERIAL PRIMARY KEY,
    "UserId"              INT NOT NULL REFERENCES "Users"("Id") ON DELETE CASCADE,
    "Plate"               VARCHAR(20) NOT NULL,
    "Make"                VARCHAR(100) NOT NULL,
    "Model"               VARCHAR(100) NOT NULL,
    "Year"                INT,
    "WeeklyKm"            NUMERIC(10,2) NOT NULL DEFAULT 350,
    "FuelEfficiencyKmL"   NUMERIC(6,2)  NOT NULL DEFAULT 10,
    "FuelPricePerLiter"   NUMERIC(10,2),
    "InsuranceMonthly"    NUMERIC(18,2),
    "PatenteAnnual"       NUMERIC(18,2),
    "IsActive"            BOOLEAN NOT NULL DEFAULT TRUE,
    "CreatedAt"           TIMESTAMP NOT NULL DEFAULT NOW()
);

-- ============================================================
-- FUNCTIONS – Vehicles
-- ============================================================

CREATE OR REPLACE FUNCTION sp_vehicles_create(
    p_user_id             INT,
    p_plate               VARCHAR,
    p_make                VARCHAR,
    p_model               VARCHAR,
    p_year                INT,
    p_weekly_km           NUMERIC,
    p_fuel_efficiency_kml NUMERIC,
    p_fuel_price          NUMERIC,
    p_insurance_monthly   NUMERIC,
    p_patente_annual      NUMERIC
) RETURNS VOID LANGUAGE plpgsql AS $$
BEGIN
    INSERT INTO "Vehicles"(
        "UserId","Plate","Make","Model","Year",
        "WeeklyKm","FuelEfficiencyKmL","FuelPricePerLiter",
        "InsuranceMonthly","PatenteAnnual"
    ) VALUES (
        p_user_id, p_plate, p_make, p_model, p_year,
        p_weekly_km, p_fuel_efficiency_kml, p_fuel_price,
        p_insurance_monthly, p_patente_annual
    );
END;
$$;

CREATE OR REPLACE FUNCTION sp_vehicles_get_by_user_id(p_user_id INT)
RETURNS TABLE(
    "Id" INT, "UserId" INT, "Plate" VARCHAR, "Make" VARCHAR, "Model" VARCHAR, "Year" INT,
    "WeeklyKm" NUMERIC, "FuelEfficiencyKmL" NUMERIC, "FuelPricePerLiter" NUMERIC,
    "InsuranceMonthly" NUMERIC, "PatenteAnnual" NUMERIC,
    "IsActive" BOOLEAN, "CreatedAt" TIMESTAMP,
    "MonthlyFuelCost" NUMERIC, "MonthlyPatente" NUMERIC, "TotalMonthlyCost" NUMERIC
) LANGUAGE plpgsql AS $$
BEGIN
    RETURN QUERY
    SELECT
        v."Id", v."UserId", v."Plate", v."Make", v."Model", v."Year",
        v."WeeklyKm", v."FuelEfficiencyKmL", v."FuelPricePerLiter",
        v."InsuranceMonthly", v."PatenteAnnual",
        v."IsActive", v."CreatedAt",
        CASE
            WHEN v."FuelPricePerLiter" IS NOT NULL AND v."FuelEfficiencyKmL" > 0
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
        , 2)::NUMERIC AS "TotalMonthlyCost"
    FROM "Vehicles" v
    WHERE v."UserId" = p_user_id AND v."IsActive" = TRUE
    ORDER BY v."Make", v."Model";
END;
$$;

CREATE OR REPLACE FUNCTION sp_vehicles_get_by_id(p_id INT, p_user_id INT)
RETURNS TABLE(
    "Id" INT, "UserId" INT, "Plate" VARCHAR, "Make" VARCHAR, "Model" VARCHAR, "Year" INT,
    "WeeklyKm" NUMERIC, "FuelEfficiencyKmL" NUMERIC, "FuelPricePerLiter" NUMERIC,
    "InsuranceMonthly" NUMERIC, "PatenteAnnual" NUMERIC,
    "IsActive" BOOLEAN, "CreatedAt" TIMESTAMP,
    "MonthlyFuelCost" NUMERIC, "MonthlyPatente" NUMERIC, "TotalMonthlyCost" NUMERIC
) LANGUAGE plpgsql AS $$
BEGIN
    RETURN QUERY
    SELECT
        v."Id", v."UserId", v."Plate", v."Make", v."Model", v."Year",
        v."WeeklyKm", v."FuelEfficiencyKmL", v."FuelPricePerLiter",
        v."InsuranceMonthly", v."PatenteAnnual",
        v."IsActive", v."CreatedAt",
        CASE
            WHEN v."FuelPricePerLiter" IS NOT NULL AND v."FuelEfficiencyKmL" > 0
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
        , 2)::NUMERIC AS "TotalMonthlyCost"
    FROM "Vehicles" v
    WHERE v."Id" = p_id AND v."UserId" = p_user_id;
END;
$$;

CREATE OR REPLACE FUNCTION sp_vehicles_update(
    p_id                  INT,
    p_user_id             INT,
    p_plate               VARCHAR,
    p_make                VARCHAR,
    p_model               VARCHAR,
    p_year                INT,
    p_weekly_km           NUMERIC,
    p_fuel_efficiency_kml NUMERIC,
    p_fuel_price          NUMERIC,
    p_insurance_monthly   NUMERIC,
    p_patente_annual      NUMERIC
) RETURNS VOID LANGUAGE plpgsql AS $$
BEGIN
    UPDATE "Vehicles" SET
        "Plate"             = p_plate,
        "Make"              = p_make,
        "Model"             = p_model,
        "Year"              = p_year,
        "WeeklyKm"          = p_weekly_km,
        "FuelEfficiencyKmL" = p_fuel_efficiency_kml,
        "FuelPricePerLiter" = p_fuel_price,
        "InsuranceMonthly"  = p_insurance_monthly,
        "PatenteAnnual"     = p_patente_annual
    WHERE "Id" = p_id AND "UserId" = p_user_id;
END;
$$;

CREATE OR REPLACE FUNCTION sp_vehicles_delete(p_id INT, p_user_id INT)
RETURNS VOID LANGUAGE plpgsql AS $$
BEGIN
    UPDATE "Vehicles" SET "IsActive" = FALSE WHERE "Id" = p_id AND "UserId" = p_user_id;
END;
$$;

CREATE OR REPLACE FUNCTION sp_vehicles_get_monthly_total(p_user_id INT)
RETURNS NUMERIC LANGUAGE plpgsql AS $$
DECLARE total NUMERIC;
BEGIN
    SELECT COALESCE(SUM(
        COALESCE(CASE WHEN "FuelPricePerLiter" IS NOT NULL AND "FuelEfficiencyKmL" > 0
            THEN ("WeeklyKm" * 52.0 / 12.0) / "FuelEfficiencyKmL" * "FuelPricePerLiter"
            ELSE 0 END, 0) +
        COALESCE("InsuranceMonthly", 0) +
        COALESCE("PatenteAnnual" / 12.0, 0)
    ), 0) INTO total
    FROM "Vehicles"
    WHERE "UserId" = p_user_id AND "IsActive" = TRUE;
    RETURN ROUND(total, 2);
END;
$$;

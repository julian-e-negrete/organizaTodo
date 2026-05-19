-- ============================================================
-- Add optional VehicleId FK to OtherExpenses
-- ============================================================

ALTER TABLE "OtherExpenses"
    ADD COLUMN IF NOT EXISTS "VehicleId" INT REFERENCES "Vehicles"("Id") ON DELETE SET NULL;

-- ============================================================
-- Drop functions whose return type or param list changes
-- ============================================================

DROP FUNCTION IF EXISTS sp_other_expenses_get_by_user_id_and_period(integer, integer, integer);
DROP FUNCTION IF EXISTS sp_other_expenses_get_by_id(integer, integer);
DROP FUNCTION IF EXISTS sp_other_expenses_create(integer, character varying, numeric, integer, integer);
DROP FUNCTION IF EXISTS sp_other_expenses_update(integer, integer, character varying, numeric);

-- ============================================================
-- sp_other_expenses_create — includes vehicle_id
-- ============================================================

CREATE OR REPLACE FUNCTION sp_other_expenses_create(
    p_user_id     INT,
    p_description VARCHAR(200),
    p_amount      DECIMAL(18,2),
    p_month       INT,
    p_year        INT,
    p_vehicle_id  INT
) RETURNS VOID LANGUAGE plpgsql AS $$
BEGIN
    INSERT INTO "OtherExpenses"("UserId","Description","Amount","Month","Year","VehicleId")
    VALUES (p_user_id, p_description, p_amount, p_month, p_year, p_vehicle_id);
END;
$$;

-- ============================================================
-- sp_other_expenses_update — includes vehicle_id
-- ============================================================

CREATE OR REPLACE FUNCTION sp_other_expenses_update(
    p_id          INT,
    p_user_id     INT,
    p_description VARCHAR(200),
    p_amount      DECIMAL(18,2),
    p_vehicle_id  INT
) RETURNS VOID LANGUAGE plpgsql AS $$
BEGIN
    UPDATE "OtherExpenses"
    SET "Description" = p_description,
        "Amount"      = p_amount,
        "VehicleId"   = p_vehicle_id
    WHERE "Id" = p_id AND "UserId" = p_user_id;
END;
$$;

-- ============================================================
-- sp_other_expenses_get_by_user_id_and_period — adds VehicleId + VehicleName
-- ============================================================

CREATE OR REPLACE FUNCTION sp_other_expenses_get_by_user_id_and_period(
    p_user_id INT, p_month INT, p_year INT
)
RETURNS TABLE(
    "Id" INT, "UserId" INT, "Description" VARCHAR, "Amount" DECIMAL,
    "ExpenseDate" TIMESTAMP, "Month" INT, "Year" INT,
    "VehicleId" INT, "VehicleName" VARCHAR
) LANGUAGE plpgsql AS $$
BEGIN
    RETURN QUERY
    SELECT e."Id", e."UserId", e."Description", e."Amount",
           e."ExpenseDate", e."Month", e."Year",
           e."VehicleId",
           CASE WHEN v."Id" IS NOT NULL THEN (v."Make" || ' ' || v."Model")::VARCHAR ELSE NULL END AS "VehicleName"
    FROM "OtherExpenses" e
    LEFT JOIN "Vehicles" v ON v."Id" = e."VehicleId" AND v."UserId" = p_user_id
    WHERE e."UserId" = p_user_id AND e."Month" = p_month AND e."Year" = p_year
    ORDER BY e."ExpenseDate" DESC;
END;
$$;

-- ============================================================
-- sp_other_expenses_get_by_id — adds VehicleId + VehicleName
-- ============================================================

CREATE OR REPLACE FUNCTION sp_other_expenses_get_by_id(p_id INT, p_user_id INT)
RETURNS TABLE(
    "Id" INT, "UserId" INT, "Description" VARCHAR, "Amount" DECIMAL,
    "ExpenseDate" TIMESTAMP, "Month" INT, "Year" INT,
    "VehicleId" INT, "VehicleName" VARCHAR
) LANGUAGE plpgsql AS $$
BEGIN
    RETURN QUERY
    SELECT e."Id", e."UserId", e."Description", e."Amount",
           e."ExpenseDate", e."Month", e."Year",
           e."VehicleId",
           CASE WHEN v."Id" IS NOT NULL THEN (v."Make" || ' ' || v."Model")::VARCHAR ELSE NULL END AS "VehicleName"
    FROM "OtherExpenses" e
    LEFT JOIN "Vehicles" v ON v."Id" = e."VehicleId" AND v."UserId" = p_user_id
    WHERE e."Id" = p_id AND e."UserId" = p_user_id;
END;
$$;

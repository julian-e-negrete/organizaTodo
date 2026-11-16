-- ============================================================
-- MIGRATION 016 — Soft-delete conversion: redefine existing
-- get/list/delete functions to respect "IsDeleted" (added in
-- migrate_015_sync_infrastructure.sql).
--
-- Two mechanical changes only, applied to the live body of each
-- function (dumped via `psql \sf <name>` against a fully-migrated
-- scratch database — the migration history redefines several of
-- these functions more than once, so the live body is the only
-- reliable source, not any single historical file):
--
--   1. Get/list functions: add `AND <alias>."IsDeleted" = FALSE`
--      to the query's WHERE clause.
--   2. Delete functions: convert hard `DELETE FROM` to
--      `UPDATE ... SET "IsDeleted" = TRUE`, preserving the
--      existing ownership check (UserId match / join) verbatim.
--      A few delete functions (HousingServices, CreditCardPurchases,
--      FixedLiabilities, Vehicles) were already "soft" via an
--      "IsActive" flag rather than a hard DELETE — for those,
--      "IsDeleted" = TRUE is added alongside the existing
--      "IsActive" = FALSE in the same UPDATE, so both the
--      legacy in-app hide flag and the new sync tombstone flag
--      get set together. Three delete functions (OtherExpenses,
--      Savings, AssetPurchases) cascade-delete across multiple
--      linked tables in several statements; every DELETE FROM
--      in those bodies is converted the same way, preserving order
--      and conditions.
-- ============================================================

-- ---------- HousingServices ----------

DROP FUNCTION IF EXISTS sp_housing_services_get_by_id(integer, integer);
CREATE OR REPLACE FUNCTION sp_housing_services_get_by_id(id integer, user_id integer)
 RETURNS TABLE("Id" integer, "UserId" integer, "Name" character varying, "Amount" numeric, "DueDay" integer, "Periodicity" character varying, "IsPaid" boolean, "PaidDate" timestamp without time zone, "IsActive" boolean, "CreatedAt" timestamp without time zone)
 LANGUAGE plpgsql
AS $$
BEGIN
    RETURN QUERY
    SELECT h."Id",h."UserId",h."Name",h."Amount",h."DueDay",
           h."Periodicity",h."IsPaid",h."PaidDate",h."IsActive",h."CreatedAt"
    FROM "HousingServices" h WHERE h."Id" = id AND h."UserId" = user_id AND h."IsDeleted" = FALSE;
END;
$$;

DROP FUNCTION IF EXISTS sp_housing_services_get_by_user_id(integer);
CREATE OR REPLACE FUNCTION sp_housing_services_get_by_user_id(user_id integer)
 RETURNS TABLE("Id" integer, "UserId" integer, "Name" character varying, "Amount" numeric, "DueDay" integer, "Periodicity" character varying, "IsPaid" boolean, "PaidDate" timestamp without time zone, "IsActive" boolean, "CreatedAt" timestamp without time zone)
 LANGUAGE plpgsql
AS $$
BEGIN
    RETURN QUERY
    SELECT h."Id",h."UserId",h."Name",h."Amount",h."DueDay",
           h."Periodicity",
           (h."IsPaid" AND h."PaidDate" IS NOT NULL AND DATE_TRUNC('month', h."PaidDate") = DATE_TRUNC('month', NOW())) AS "IsPaid",
           h."PaidDate",h."IsActive",h."CreatedAt"
    FROM "HousingServices" h
    WHERE h."UserId" = user_id AND h."IsActive" = TRUE AND h."IsDeleted" = FALSE ORDER BY h."Name";
END;
$$;

DROP FUNCTION IF EXISTS sp_housing_services_delete(integer, integer);
CREATE OR REPLACE FUNCTION sp_housing_services_delete(id integer, user_id integer)
 RETURNS void
 LANGUAGE plpgsql
AS $$
BEGIN
    UPDATE "HousingServices" SET "IsActive"=FALSE, "IsDeleted"=TRUE WHERE "Id"=id AND "UserId"=user_id;
END;
$$;

-- ---------- CreditCardPurchases ----------

DROP FUNCTION IF EXISTS sp_credit_card_get_by_id(integer, integer);
CREATE OR REPLACE FUNCTION sp_credit_card_get_by_id(id integer, user_id integer)
 RETURNS TABLE("Id" integer, "UserId" integer, "Description" character varying, "TotalAmount" numeric, "Installments" integer, "CurrentInstallment" integer, "InterestRate" numeric, "PurchaseDate" timestamp without time zone, "IsActive" boolean, "DueMonth" integer, "DueYear" integer)
 LANGUAGE plpgsql
AS $$
BEGIN
    RETURN QUERY
    SELECT c."Id",c."UserId",c."Description",c."TotalAmount",c."Installments",
           c."CurrentInstallment",c."InterestRate",c."PurchaseDate",c."IsActive",
           c."DueMonth",c."DueYear"
    FROM "CreditCardPurchases" c WHERE c."Id"=id AND c."UserId"=user_id AND c."IsDeleted" = FALSE;
END;
$$;

DROP FUNCTION IF EXISTS sp_credit_card_get_by_user_id(integer, integer, integer);
CREATE OR REPLACE FUNCTION sp_credit_card_get_by_user_id(user_id integer, month integer, year integer)
 RETURNS TABLE("Id" integer, "UserId" integer, "Description" character varying, "TotalAmount" numeric, "Installments" integer, "CurrentInstallment" integer, "InterestRate" numeric, "PurchaseDate" timestamp without time zone, "IsActive" boolean, "DueMonth" integer, "DueYear" integer, "MonthlyInstallmentAmount" numeric)
 LANGUAGE plpgsql
AS $$
BEGIN
    RETURN QUERY
    SELECT c."Id",c."UserId",c."Description",c."TotalAmount",c."Installments",
           c."CurrentInstallment",c."InterestRate",c."PurchaseDate",c."IsActive",
           c."DueMonth",c."DueYear",
           CAST((c."TotalAmount"*(1+c."InterestRate"/100.0))/NULLIF(c."Installments",0) AS DECIMAL(18,2))
    FROM "CreditCardPurchases" c
    WHERE c."UserId"=user_id AND c."IsActive"=TRUE
      AND c."DueMonth"=month AND c."DueYear"=year AND c."IsDeleted" = FALSE
    ORDER BY c."PurchaseDate" DESC;
END;
$$;

DROP FUNCTION IF EXISTS sp_credit_card_delete(integer, integer);
CREATE OR REPLACE FUNCTION sp_credit_card_delete(id integer, user_id integer)
 RETURNS void
 LANGUAGE plpgsql
AS $$
BEGIN
    UPDATE "CreditCardPurchases" SET "IsActive"=FALSE, "IsDeleted"=TRUE WHERE "Id"=id AND "UserId"=user_id;
END;
$$;

-- ---------- FixedLiabilities ----------

DROP FUNCTION IF EXISTS sp_fixed_liabilities_get_by_id(integer, integer);
CREATE OR REPLACE FUNCTION sp_fixed_liabilities_get_by_id(id integer, user_id integer)
 RETURNS TABLE("Id" integer, "UserId" integer, "Name" character varying, "MonthlyAmount" numeric, "DueDay" integer, "IsActive" boolean, "IsPaid" boolean, "PaidDate" timestamp without time zone, "CreatedAt" timestamp without time zone)
 LANGUAGE plpgsql
AS $$
BEGIN
    RETURN QUERY
    SELECT f."Id",f."UserId",f."Name",f."MonthlyAmount",f."DueDay",f."IsActive",f."IsPaid",f."PaidDate",f."CreatedAt"
    FROM "FixedLiabilities" f WHERE f."Id"=id AND f."UserId"=user_id AND f."IsDeleted" = FALSE;
END;
$$;

DROP FUNCTION IF EXISTS sp_fixed_liabilities_get_by_user_id(integer);
CREATE OR REPLACE FUNCTION sp_fixed_liabilities_get_by_user_id(user_id integer)
 RETURNS TABLE("Id" integer, "UserId" integer, "Name" character varying, "MonthlyAmount" numeric, "DueDay" integer, "IsActive" boolean, "IsPaid" boolean, "PaidDate" timestamp without time zone, "CreatedAt" timestamp without time zone)
 LANGUAGE plpgsql
AS $$
BEGIN
    RETURN QUERY
    SELECT f."Id",f."UserId",f."Name",f."MonthlyAmount",f."DueDay",f."IsActive",f."IsPaid",f."PaidDate",f."CreatedAt"
    FROM "FixedLiabilities" f WHERE f."UserId"=user_id AND f."IsActive"=TRUE AND f."IsDeleted" = FALSE ORDER BY f."Name";
END;
$$;

DROP FUNCTION IF EXISTS sp_fixed_liabilities_delete(integer, integer);
CREATE OR REPLACE FUNCTION sp_fixed_liabilities_delete(id integer, user_id integer)
 RETURNS void
 LANGUAGE plpgsql
AS $$
BEGIN
    UPDATE "FixedLiabilities" SET "IsActive"=FALSE, "IsDeleted"=TRUE WHERE "Id"=id AND "UserId"=user_id;
END;
$$;

-- ---------- Income ----------

DROP FUNCTION IF EXISTS sp_income_get_by_id(integer, integer);
CREATE OR REPLACE FUNCTION sp_income_get_by_id(id integer, user_id integer)
 RETURNS TABLE("Id" integer, "UserId" integer, "Description" character varying, "Amount" numeric, "IncomeDate" timestamp without time zone, "Category" character varying, "Month" integer, "Year" integer)
 LANGUAGE plpgsql
AS $$
BEGIN
    RETURN QUERY
    SELECT i."Id",i."UserId",i."Description",i."Amount",
           i."IncomeDate",i."Category",i."Month",i."Year"
    FROM "Income" i WHERE i."Id"=id AND i."UserId"=user_id AND i."IsDeleted" = FALSE;
END;
$$;

DROP FUNCTION IF EXISTS sp_income_get_by_user_id_and_period(integer, integer, integer);
CREATE OR REPLACE FUNCTION sp_income_get_by_user_id_and_period(user_id integer, month integer, year integer)
 RETURNS TABLE("Id" integer, "UserId" integer, "Description" character varying, "Amount" numeric, "IncomeDate" timestamp without time zone, "Category" character varying, "Month" integer, "Year" integer)
 LANGUAGE plpgsql
AS $$
BEGIN
    RETURN QUERY
    SELECT i."Id",i."UserId",i."Description",i."Amount",
           i."IncomeDate",i."Category",i."Month",i."Year"
    FROM "Income" i
    WHERE i."UserId"=user_id AND i."Month"=month AND i."Year"=year AND i."IsDeleted" = FALSE
    ORDER BY i."IncomeDate" DESC;
END;
$$;

DROP FUNCTION IF EXISTS sp_income_delete(integer, integer);
CREATE OR REPLACE FUNCTION sp_income_delete(id integer, user_id integer)
 RETURNS void
 LANGUAGE plpgsql
AS $$
BEGIN
    UPDATE "Income" SET "IsDeleted"=TRUE WHERE "Id"=id AND "UserId"=user_id;
END;
$$;

-- ---------- OtherExpenses ----------

DROP FUNCTION IF EXISTS sp_other_expenses_get_by_id(integer, integer);
CREATE OR REPLACE FUNCTION sp_other_expenses_get_by_id(p_id integer, p_user_id integer)
 RETURNS TABLE("Id" integer, "UserId" integer, "Description" character varying, "Amount" numeric, "ExpenseDate" timestamp without time zone, "Month" integer, "Year" integer, "VehicleId" integer, "VehicleName" character varying)
 LANGUAGE plpgsql
AS $$
BEGIN
    RETURN QUERY
    SELECT e."Id", e."UserId", e."Description", e."Amount",
           e."ExpenseDate", e."Month", e."Year",
           e."VehicleId",
           CASE WHEN v."Id" IS NOT NULL THEN (v."Make" || ' ' || v."Model")::VARCHAR ELSE NULL END AS "VehicleName"
    FROM "OtherExpenses" e
    LEFT JOIN "Vehicles" v ON v."Id" = e."VehicleId" AND v."UserId" = p_user_id
    WHERE e."Id" = p_id AND e."UserId" = p_user_id AND e."IsDeleted" = FALSE;
END;
$$;

DROP FUNCTION IF EXISTS sp_other_expenses_get_by_user_id_and_period(integer, integer, integer);
CREATE OR REPLACE FUNCTION sp_other_expenses_get_by_user_id_and_period(p_user_id integer, p_month integer, p_year integer)
 RETURNS TABLE("Id" integer, "UserId" integer, "Description" character varying, "Amount" numeric, "ExpenseDate" timestamp without time zone, "Month" integer, "Year" integer, "VehicleId" integer, "VehicleName" character varying)
 LANGUAGE plpgsql
AS $$
BEGIN
    RETURN QUERY
    SELECT e."Id", e."UserId", e."Description", e."Amount",
           e."ExpenseDate", e."Month", e."Year",
           e."VehicleId",
           CASE WHEN v."Id" IS NOT NULL THEN (v."Make" || ' ' || v."Model")::VARCHAR ELSE NULL END AS "VehicleName"
    FROM "OtherExpenses" e
    LEFT JOIN "Vehicles" v ON v."Id" = e."VehicleId" AND v."UserId" = p_user_id
    WHERE e."UserId" = p_user_id AND e."Month" = p_month AND e."Year" = p_year AND e."IsDeleted" = FALSE
    ORDER BY e."ExpenseDate" DESC;
END;
$$;

-- sp_other_expenses_delete cascades across AssetPurchases/Savings/OtherExpenses/
-- LedgerTransactions in multiple statements (not the single-statement pattern
-- most delete functions follow). Every DELETE FROM in the body is converted to
-- UPDATE ... SET "IsDeleted" = TRUE, preserving the original order, conditions,
-- and the sp_ledger_recompute_balances(...) call. Flagged for reviewer per
-- Step 6 of the task brief.
DROP FUNCTION IF EXISTS sp_other_expenses_delete(integer, integer);
CREATE OR REPLACE FUNCTION sp_other_expenses_delete(p_id integer, p_user_id integer)
 RETURNS void
 LANGUAGE plpgsql
AS $$
DECLARE
    v_ap_id            INT;
    v_ap_saving_id     INT;
    v_saving_ledger_id INT;
BEGIN
    -- Read all linked IDs before any deletions
    SELECT "Id", "SavingId" INTO v_ap_id, v_ap_saving_id
    FROM "AssetPurchases"
    WHERE "ExpenseId" = p_id AND "UserId" = p_user_id
    LIMIT 1;

    IF v_ap_saving_id IS NOT NULL THEN
        SELECT "LedgerTransactionId" INTO v_saving_ledger_id
        FROM "Savings" WHERE "Id" = v_ap_saving_id;
    END IF;

    -- Soft-delete AP first (mirrors original hard-delete ordering)
    IF v_ap_id IS NOT NULL THEN
        UPDATE "AssetPurchases" SET "IsDeleted" = TRUE WHERE "Id" = v_ap_id;
    END IF;

    -- Soft-delete linked Saving
    IF v_ap_saving_id IS NOT NULL THEN
        UPDATE "Savings" SET "IsDeleted" = TRUE WHERE "Id" = v_ap_saving_id;
    END IF;

    -- Soft-delete the OtherExpense
    UPDATE "OtherExpenses" SET "IsDeleted" = TRUE WHERE "Id" = p_id AND "UserId" = p_user_id;

    -- Soft-delete Saving's LedgerTransaction last (if it had one)
    IF v_saving_ledger_id IS NOT NULL THEN
        UPDATE "LedgerTransactions" SET "IsDeleted" = TRUE WHERE "Id" = v_saving_ledger_id;
        PERFORM sp_ledger_recompute_balances(p_user_id);
    END IF;
END;
$$;

-- ---------- Savings ----------

DROP FUNCTION IF EXISTS sp_savings_get_by_id(integer, integer);
CREATE OR REPLACE FUNCTION sp_savings_get_by_id(id integer, user_id integer)
 RETURNS TABLE("Id" integer, "UserId" integer, "Amount" numeric, "Month" integer, "Year" integer, "Notes" character varying, "IsInitialBalance" boolean, "CreatedAt" timestamp without time zone)
 LANGUAGE plpgsql
AS $$
BEGIN
    RETURN QUERY
    SELECT s."Id",s."UserId",s."Amount",s."Month",s."Year",s."Notes",s."IsInitialBalance",s."CreatedAt"
    FROM "Savings" s WHERE s."Id" = id AND s."UserId" = user_id AND s."IsDeleted" = FALSE;
END;
$$;

DROP FUNCTION IF EXISTS sp_savings_get_by_user_id(integer);
CREATE OR REPLACE FUNCTION sp_savings_get_by_user_id(user_id integer)
 RETURNS TABLE("Id" integer, "UserId" integer, "Amount" numeric, "Month" integer, "Year" integer, "Notes" character varying, "IsInitialBalance" boolean, "CreatedAt" timestamp without time zone)
 LANGUAGE plpgsql
AS $$
BEGIN
    RETURN QUERY
    SELECT s."Id",s."UserId",s."Amount",s."Month",s."Year",s."Notes",s."IsInitialBalance",s."CreatedAt"
    FROM "Savings" s WHERE s."UserId" = user_id AND s."IsDeleted" = FALSE
    ORDER BY s."IsInitialBalance" DESC, s."Year" DESC, s."Month" DESC;
END;
$$;

-- sp_savings_delete cascades across AssetPurchases/OtherExpenses/Savings/
-- LedgerTransactions in multiple statements. Same treatment as
-- sp_other_expenses_delete above — every DELETE FROM becomes an UPDATE ...
-- SET "IsDeleted" = TRUE. Flagged for reviewer per Step 6.
DROP FUNCTION IF EXISTS sp_savings_delete(integer, integer);
CREATE OR REPLACE FUNCTION sp_savings_delete(p_id integer, p_user_id integer)
 RETURNS void
 LANGUAGE plpgsql
AS $$
DECLARE
    v_ledger_id     INT;
    v_ap_id         INT;
    v_ap_expense_id INT;
BEGIN
    -- Read all linked IDs before any deletions
    SELECT "LedgerTransactionId" INTO v_ledger_id
    FROM "Savings" WHERE "Id" = p_id AND "UserId" = p_user_id;

    SELECT "Id", "ExpenseId" INTO v_ap_id, v_ap_expense_id
    FROM "AssetPurchases"
    WHERE "SavingId" = p_id AND "UserId" = p_user_id
    LIMIT 1;

    -- Soft-delete AP first (mirrors original hard-delete ordering)
    IF v_ap_id IS NOT NULL THEN
        UPDATE "AssetPurchases" SET "IsDeleted" = TRUE WHERE "Id" = v_ap_id;
    END IF;

    -- Soft-delete linked OtherExpense (no LedgerTransaction on expense side)
    IF v_ap_expense_id IS NOT NULL THEN
        UPDATE "OtherExpenses" SET "IsDeleted" = TRUE WHERE "Id" = v_ap_expense_id;
    END IF;

    -- Soft-delete the Saving
    UPDATE "Savings" SET "IsDeleted" = TRUE WHERE "Id" = p_id AND "UserId" = p_user_id;

    -- Soft-delete the Saving's LedgerTransaction last
    IF v_ledger_id IS NOT NULL THEN
        UPDATE "LedgerTransactions" SET "IsDeleted" = TRUE WHERE "Id" = v_ledger_id;
        PERFORM sp_ledger_recompute_balances(p_user_id);
    END IF;
END;
$$;

-- ---------- Shopping (ShoppingLists / ShoppingListItems) ----------

-- Fix (post-review): ItemCount/TotalEstimated originally aggregated over ALL
-- joined "ShoppingListItems" rows regardless of "IsDeleted". Before this
-- migration that was harmless because sp_shopping_delete_item hard-deleted
-- the row (gone from the join entirely); now that sp_shopping_delete_item
-- soft-deletes, a deleted item stays in the table and would keep inflating
-- both aggregates forever. Filter added to the JOIN condition (not the WHERE
-- clause) so lists with zero remaining live items still return a row with
-- ItemCount = 0 / TotalEstimated = 0, preserving existing LEFT JOIN semantics.
DROP FUNCTION IF EXISTS sp_shopping_get_user_lists(integer);
CREATE OR REPLACE FUNCTION sp_shopping_get_user_lists(user_id integer)
 RETURNS TABLE("Id" integer, "UserId" integer, "Name" character varying, "Month" integer, "Year" integer, "CreatedAt" timestamp without time zone, "TotalEstimated" numeric, "ItemCount" bigint)
 LANGUAGE plpgsql
AS $$
BEGIN
    RETURN QUERY
    SELECT l."Id",l."UserId",l."Name",l."Month",l."Year",l."CreatedAt",
           COALESCE(SUM(i."EstimatedPrice"*i."Quantity"),0)::DECIMAL(18,2),
           COUNT(i."Id")
    FROM "ShoppingLists" l
    LEFT JOIN "ShoppingListItems" i ON i."ShoppingListId"=l."Id" AND i."IsDeleted" = FALSE
    WHERE l."UserId"=user_id AND l."IsDeleted" = FALSE
    GROUP BY l."Id",l."UserId",l."Name",l."Month",l."Year",l."CreatedAt"
    ORDER BY l."Year" DESC, l."Month" DESC;
END;
$$;

DROP FUNCTION IF EXISTS sp_shopping_get_list_items(integer, integer);
CREATE OR REPLACE FUNCTION sp_shopping_get_list_items(shopping_list_id integer, user_id integer)
 RETURNS TABLE("Id" integer, "ShoppingListId" integer, "ProductName" character varying, "Quantity" integer, "EstimatedPrice" numeric, "Supermarket" character varying, "Priority" integer)
 LANGUAGE plpgsql
AS $$
BEGIN
    RETURN QUERY
    SELECT i."Id",i."ShoppingListId",i."ProductName",i."Quantity",
           i."EstimatedPrice",i."Supermarket",i."Priority"
    FROM "ShoppingListItems" i
    INNER JOIN "ShoppingLists" l ON l."Id"=i."ShoppingListId"
    WHERE i."ShoppingListId"=shopping_list_id AND l."UserId"=user_id AND i."IsDeleted" = FALSE
    ORDER BY i."Priority", i."ProductName";
END;
$$;

DROP FUNCTION IF EXISTS sp_shopping_delete_item(integer, integer);
CREATE OR REPLACE FUNCTION sp_shopping_delete_item(item_id integer, user_id integer)
 RETURNS void
 LANGUAGE plpgsql
AS $$
BEGIN
    UPDATE "ShoppingListItems" SET "IsDeleted" = TRUE
    WHERE "Id"=item_id
    AND "ShoppingListId" IN (SELECT "Id" FROM "ShoppingLists" WHERE "UserId"=user_id);
END;
$$;

-- ---------- AssetPurchases ----------

DROP FUNCTION IF EXISTS sp_asset_purchases_get_by_id(integer, integer);
CREATE OR REPLACE FUNCTION sp_asset_purchases_get_by_id(p_id integer, p_user_id integer)
 RETURNS TABLE("Id" integer, "UserId" integer, "ExpenseId" integer, "SavingId" integer, "AssetType" character varying, "Quantity" numeric, "UnitPriceArs" numeric, "ExchangeType" character varying, "TotalArs" numeric, "Notes" character varying, "PurchaseDate" date, "CreatedAt" timestamp without time zone, "ExpenseDescription" character varying, "SavingNotes" character varying)
 LANGUAGE plpgsql
AS $$
BEGIN
    RETURN QUERY
    SELECT ap."Id", ap."UserId", ap."ExpenseId", ap."SavingId",
           ap."AssetType", ap."Quantity", ap."UnitPriceArs",
           ap."ExchangeType", ap."TotalArs", ap."Notes",
           ap."PurchaseDate", ap."CreatedAt",
           oe."Description"::VARCHAR,
           s."Notes"::VARCHAR
    FROM "AssetPurchases" ap
    LEFT JOIN "OtherExpenses" oe ON oe."Id" = ap."ExpenseId"
    LEFT JOIN "Savings" s ON s."Id" = ap."SavingId"
    WHERE ap."Id" = p_id AND ap."UserId" = p_user_id AND ap."IsDeleted" = FALSE;
END;
$$;

DROP FUNCTION IF EXISTS sp_asset_purchases_get_by_user_id(integer);
CREATE OR REPLACE FUNCTION sp_asset_purchases_get_by_user_id(p_user_id integer)
 RETURNS TABLE("Id" integer, "UserId" integer, "ExpenseId" integer, "SavingId" integer, "AssetType" character varying, "Quantity" numeric, "UnitPriceArs" numeric, "ExchangeType" character varying, "TotalArs" numeric, "Notes" character varying, "PurchaseDate" date, "CreatedAt" timestamp without time zone, "ExpenseDescription" character varying, "SavingNotes" character varying)
 LANGUAGE plpgsql
AS $$
BEGIN
    RETURN QUERY
    SELECT ap."Id", ap."UserId", ap."ExpenseId", ap."SavingId",
           ap."AssetType", ap."Quantity", ap."UnitPriceArs",
           ap."ExchangeType", ap."TotalArs", ap."Notes",
           ap."PurchaseDate", ap."CreatedAt",
           oe."Description"::VARCHAR,
           s."Notes"::VARCHAR
    FROM "AssetPurchases" ap
    LEFT JOIN "OtherExpenses" oe ON oe."Id" = ap."ExpenseId"
    LEFT JOIN "Savings" s ON s."Id" = ap."SavingId"
    WHERE ap."UserId" = p_user_id AND ap."IsDeleted" = FALSE
    ORDER BY ap."PurchaseDate" DESC, ap."CreatedAt" DESC;
END;
$$;

-- sp_asset_purchases_delete cascades across AssetPurchases/OtherExpenses/
-- Savings/LedgerTransactions in multiple statements. Same treatment as the
-- other cascading deletes above. Flagged for reviewer per Step 6.
DROP FUNCTION IF EXISTS sp_asset_purchases_delete(integer, integer);
CREATE OR REPLACE FUNCTION sp_asset_purchases_delete(p_id integer, p_user_id integer)
 RETURNS void
 LANGUAGE plpgsql
AS $$
DECLARE
    v_expense_id       INT;
    v_saving_id        INT;
    v_saving_ledger_id INT;
BEGIN
    -- Read all linked IDs before any deletions
    SELECT "ExpenseId", "SavingId" INTO v_expense_id, v_saving_id
    FROM "AssetPurchases" WHERE "Id" = p_id AND "UserId" = p_user_id;

    IF v_saving_id IS NOT NULL THEN
        SELECT "LedgerTransactionId" INTO v_saving_ledger_id
        FROM "Savings" WHERE "Id" = v_saving_id;
    END IF;

    -- Soft-delete AP first (mirrors original hard-delete ordering)
    UPDATE "AssetPurchases" SET "IsDeleted" = TRUE WHERE "Id" = p_id AND "UserId" = p_user_id;

    -- Soft-delete linked OtherExpense (no LedgerTransaction on its side)
    IF v_expense_id IS NOT NULL THEN
        UPDATE "OtherExpenses" SET "IsDeleted" = TRUE WHERE "Id" = v_expense_id;
    END IF;

    -- Soft-delete linked Saving
    IF v_saving_id IS NOT NULL THEN
        UPDATE "Savings" SET "IsDeleted" = TRUE WHERE "Id" = v_saving_id;
    END IF;

    -- Soft-delete Saving's LedgerTransaction last
    IF v_saving_ledger_id IS NOT NULL THEN
        UPDATE "LedgerTransactions" SET "IsDeleted" = TRUE WHERE "Id" = v_saving_ledger_id;
        PERFORM sp_ledger_recompute_balances(p_user_id);
    END IF;
END;
$$;

-- ---------- Vehicles ----------

DROP FUNCTION IF EXISTS sp_vehicles_get_by_id(integer, integer);
CREATE OR REPLACE FUNCTION sp_vehicles_get_by_id(p_id integer, p_user_id integer)
 RETURNS TABLE("Id" integer, "UserId" integer, "Plate" character varying, "Make" character varying, "Model" character varying, "Year" integer, "WeeklyKm" numeric, "FuelEfficiencyKmL" numeric, "FuelPricePerLiter" numeric, "InsuranceMonthly" numeric, "PatenteAnnual" numeric, "IsActive" boolean, "CreatedAt" timestamp without time zone, "MonthlyFuelCost" numeric, "MonthlyPatente" numeric, "TotalMonthlyCost" numeric, "FuelPaid" boolean, "InsurancePaid" boolean, "PatentePaid" boolean, "OilChangeIntervalKm" numeric, "OilChangeCostEstimate" numeric, "LastOilChangeDate" date, "LastOilChangeKm" numeric, "MonthlyOilChangeCost" numeric, "KmSinceLastChange" numeric, "KmToNextChange" numeric, "NextOilChangeDueDate" date, "MaintenancePaid" boolean)
 LANGUAGE plpgsql
AS $$
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
    WHERE v."Id" = p_id AND v."UserId" = p_user_id AND v."IsDeleted" = FALSE;
END;
$$;

DROP FUNCTION IF EXISTS sp_vehicles_get_by_user_id(integer, integer, integer);
CREATE OR REPLACE FUNCTION sp_vehicles_get_by_user_id(p_user_id integer, p_month integer, p_year integer)
 RETURNS TABLE("Id" integer, "UserId" integer, "Plate" character varying, "Make" character varying, "Model" character varying, "Year" integer, "WeeklyKm" numeric, "FuelEfficiencyKmL" numeric, "FuelPricePerLiter" numeric, "InsuranceMonthly" numeric, "PatenteAnnual" numeric, "IsActive" boolean, "CreatedAt" timestamp without time zone, "MonthlyFuelCost" numeric, "MonthlyPatente" numeric, "TotalMonthlyCost" numeric, "FuelPaid" boolean, "InsurancePaid" boolean, "PatentePaid" boolean, "OilChangeIntervalKm" numeric, "OilChangeCostEstimate" numeric, "LastOilChangeDate" date, "LastOilChangeKm" numeric, "MonthlyOilChangeCost" numeric, "KmSinceLastChange" numeric, "KmToNextChange" numeric, "NextOilChangeDueDate" date, "MaintenancePaid" boolean)
 LANGUAGE plpgsql
AS $$
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
    WHERE v."UserId" = p_user_id AND v."IsActive" = TRUE AND v."IsDeleted" = FALSE
    ORDER BY v."Make", v."Model";
END;
$$;

DROP FUNCTION IF EXISTS sp_vehicles_delete(integer, integer);
CREATE OR REPLACE FUNCTION sp_vehicles_delete(p_id integer, p_user_id integer)
 RETURNS void
 LANGUAGE plpgsql
AS $$
BEGIN
    UPDATE "Vehicles" SET "IsActive" = FALSE, "IsDeleted" = TRUE WHERE "Id" = p_id AND "UserId" = p_user_id;
END;
$$;

-- ---------- VehicleMaintenanceLogs ----------

DROP FUNCTION IF EXISTS sp_vehicle_maintenance_logs_get(integer, integer);
CREATE OR REPLACE FUNCTION sp_vehicle_maintenance_logs_get(p_vehicle_id integer, p_user_id integer)
 RETURNS TABLE("Id" integer, "VehicleId" integer, "UserId" integer, "ServiceDate" date, "KmAtService" numeric, "Cost" numeric, "Notes" text, "CreatedAt" timestamp without time zone)
 LANGUAGE plpgsql
AS $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM "Vehicles" WHERE "Id"=p_vehicle_id AND "UserId"=p_user_id
    ) THEN RETURN; END IF;

    RETURN QUERY
    SELECT l."Id", l."VehicleId", l."UserId",
           l."ServiceDate", l."KmAtService",
           l."Cost", l."Notes", l."CreatedAt"
    FROM "VehicleMaintenanceLogs" l
    WHERE l."VehicleId" = p_vehicle_id AND l."UserId" = p_user_id AND l."IsDeleted" = FALSE
    ORDER BY l."ServiceDate" DESC;
END;
$$;

-- ---------- RecurringIncomes ----------

DROP FUNCTION IF EXISTS sp_recurring_income_get_by_id(integer, integer);
CREATE OR REPLACE FUNCTION sp_recurring_income_get_by_id(p_id integer, p_user_id integer)
 RETURNS TABLE("Id" integer, "UserId" integer, "Description" character varying, "Amount" numeric, "IsActive" boolean, "CreatedAt" timestamp without time zone)
 LANGUAGE plpgsql
AS $$
BEGIN
    RETURN QUERY
    SELECT r."Id", r."UserId", r."Description", r."Amount", r."IsActive", r."CreatedAt"
    FROM "RecurringIncomes" r
    WHERE r."Id" = p_id AND r."UserId" = p_user_id AND r."IsDeleted" = FALSE;
END;
$$;

DROP FUNCTION IF EXISTS sp_recurring_income_get_by_user_id(integer);
CREATE OR REPLACE FUNCTION sp_recurring_income_get_by_user_id(p_user_id integer)
 RETURNS TABLE("Id" integer, "UserId" integer, "Description" character varying, "Amount" numeric, "IsActive" boolean, "CreatedAt" timestamp without time zone)
 LANGUAGE plpgsql
AS $$
BEGIN
    RETURN QUERY
    SELECT r."Id", r."UserId", r."Description", r."Amount", r."IsActive", r."CreatedAt"
    FROM "RecurringIncomes" r
    WHERE r."UserId" = p_user_id AND r."IsDeleted" = FALSE
    ORDER BY r."IsActive" DESC, r."Description";
END;
$$;

DROP FUNCTION IF EXISTS sp_recurring_income_delete(integer, integer);
CREATE OR REPLACE FUNCTION sp_recurring_income_delete(p_id integer, p_user_id integer)
 RETURNS void
 LANGUAGE plpgsql
AS $$
BEGIN
    UPDATE "RecurringIncomes" SET "IsDeleted" = TRUE WHERE "Id" = p_id AND "UserId" = p_user_id;
END;
$$;

-- ---------- LedgerTags ----------

-- Fix (post-review): UsageCount originally counted ltt."TransactionId", which
-- stays non-null even after sp_savings_delete/sp_other_expenses_delete/
-- sp_asset_purchases_delete soft-delete the linked "LedgerTransactions" row —
-- those cascades never touch "LedgerTransactionTags" itself, so a tag's usage
-- count would silently keep counting transactions that are now soft-deleted.
-- Added a second LEFT JOIN to "LedgerTransactions" gated on "IsDeleted" =
-- FALSE and count that join's Id instead, so a soft-deleted transaction no
-- longer contributes to the count (same LEFT JOIN semantics: tags with zero
-- live usages still return a row with UsageCount = 0).
DROP FUNCTION IF EXISTS sp_ledger_tags_get_by_user_id(integer);
CREATE OR REPLACE FUNCTION sp_ledger_tags_get_by_user_id(p_user_id integer)
 RETURNS TABLE("Id" integer, "UserId" integer, "Name" character varying, "CreatedAt" timestamp without time zone, "UsageCount" bigint)
 LANGUAGE plpgsql
AS $$
BEGIN
    RETURN QUERY
    SELECT t."Id",t."UserId",t."Name",t."CreatedAt", COUNT(lt."Id")
    FROM "LedgerTags" t
    LEFT JOIN "LedgerTransactionTags" ltt ON ltt."TagId"=t."Id"
    LEFT JOIN "LedgerTransactions" lt ON lt."Id"=ltt."TransactionId" AND lt."IsDeleted" = FALSE
    WHERE t."UserId"=p_user_id AND t."IsDeleted" = FALSE
    GROUP BY t."Id",t."UserId",t."Name",t."CreatedAt"
    ORDER BY t."Name";
END;
$$;

-- ---------- LedgerTransactions ----------

DROP FUNCTION IF EXISTS sp_ledger_transaction_get_children(integer, integer);
CREATE OR REPLACE FUNCTION sp_ledger_transaction_get_children(p_id integer, p_user_id integer)
 RETURNS TABLE("Id" integer, "Amount" numeric, "TransactionDate" timestamp without time zone, "InterestRate" numeric, "BalanceAfter" numeric)
 LANGUAGE plpgsql
AS $$
BEGIN
    RETURN QUERY
    SELECT t."Id",t."Amount",t."TransactionDate",t."InterestRate",t."BalanceAfter"
    FROM "LedgerTransactions" t
    WHERE t."ParentTransactionId"=p_id AND t."UserId"=p_user_id AND t."IsDeleted" = FALSE
    ORDER BY t."TransactionDate";
END;
$$;

DROP FUNCTION IF EXISTS sp_ledger_transaction_get_detail(integer, integer);
CREATE OR REPLACE FUNCTION sp_ledger_transaction_get_detail(p_id integer, p_user_id integer)
 RETURNS TABLE("Id" integer, "UserId" integer, "Amount" numeric, "Type" character varying, "Description" character varying, "TransactionDate" timestamp without time zone, "BalanceAfter" numeric, "GeneratesInterest" boolean, "InterestRate" numeric, "ParentTransactionId" integer, "CreatedAt" timestamp without time zone, "Tags" text, "ParentDescription" character varying, "ParentTransactionDate" timestamp without time zone)
 LANGUAGE plpgsql
AS $$
BEGIN
    RETURN QUERY
    SELECT t."Id",t."UserId",t."Amount",t."Type",t."Description",t."TransactionDate",
           t."BalanceAfter",t."GeneratesInterest",t."InterestRate",t."ParentTransactionId",t."CreatedAt",
           tags."Tags", parent."Description", parent."TransactionDate"
    FROM "LedgerTransactions" t
    LEFT JOIN LATERAL (
        SELECT array_to_string(array_agg(g."Name" ORDER BY g."Name"), ',') AS "Tags"
        FROM "LedgerTransactionTags" ltt
        JOIN "LedgerTags" g ON g."Id"=ltt."TagId"
        WHERE ltt."TransactionId"=t."Id"
    ) tags ON TRUE
    LEFT JOIN "LedgerTransactions" parent ON parent."Id"=t."ParentTransactionId"
    WHERE t."Id"=p_id AND t."UserId"=p_user_id AND t."IsDeleted" = FALSE;
END;
$$;

DROP FUNCTION IF EXISTS sp_ledger_history_get(integer, timestamp without time zone, timestamp without time zone, character varying, integer[], numeric, numeric, boolean, boolean, character varying, integer, integer);
CREATE OR REPLACE FUNCTION sp_ledger_history_get(p_user_id integer, p_date_from timestamp without time zone, p_date_to timestamp without time zone, p_type character varying, p_tag_ids integer[], p_amount_min numeric, p_amount_max numeric, p_only_with_interest boolean, p_only_negative_balance boolean, p_search character varying, p_page integer, p_page_size integer)
 RETURNS TABLE("Id" integer, "UserId" integer, "Amount" numeric, "Type" character varying, "Description" character varying, "TransactionDate" timestamp without time zone, "BalanceAfter" numeric, "GeneratesInterest" boolean, "InterestRate" numeric, "ParentTransactionId" integer, "CreatedAt" timestamp without time zone, "Tags" text, "TotalCount" bigint)
 LANGUAGE plpgsql
AS $$
BEGIN
    RETURN QUERY
    SELECT t."Id",t."UserId",t."Amount",t."Type",t."Description",t."TransactionDate",
           t."BalanceAfter",t."GeneratesInterest",t."InterestRate",t."ParentTransactionId",t."CreatedAt",
           tags."Tags",
           COUNT(*) OVER()::BIGINT AS "TotalCount"
    FROM "LedgerTransactions" t
    LEFT JOIN LATERAL (
        SELECT array_to_string(array_agg(g."Name" ORDER BY g."Name"), ',') AS "Tags"
        FROM "LedgerTransactionTags" ltt
        JOIN "LedgerTags" g ON g."Id"=ltt."TagId"
        WHERE ltt."TransactionId"=t."Id"
    ) tags ON TRUE
    WHERE t."UserId"=p_user_id
      AND t."IsDeleted" = FALSE
      AND (p_date_from IS NULL OR t."TransactionDate" >= p_date_from)
      AND (p_date_to IS NULL OR t."TransactionDate" <= p_date_to)
      AND (p_type IS NULL OR t."Type" = p_type)
      AND (p_tag_ids IS NULL OR array_length(p_tag_ids,1) IS NULL OR EXISTS (
          SELECT 1 FROM "LedgerTransactionTags" ltt2
          WHERE ltt2."TransactionId"=t."Id" AND ltt2."TagId" = ANY(p_tag_ids)
      ))
      AND (p_amount_min IS NULL OR ABS(t."Amount") >= p_amount_min)
      AND (p_amount_max IS NULL OR ABS(t."Amount") <= p_amount_max)
      AND (p_only_with_interest IS NOT TRUE OR t."GeneratesInterest" = TRUE OR t."Type"='interest')
      AND (p_only_negative_balance IS NOT TRUE OR t."BalanceAfter" < 0)
      AND (p_search IS NULL OR p_search = '' OR
           t."Description" ILIKE '%'||p_search||'%' OR
           t."Id"::TEXT = p_search OR
           EXISTS (
               SELECT 1 FROM "LedgerTransactionTags" ltt3
               JOIN "LedgerTags" g2 ON g2."Id"=ltt3."TagId"
               WHERE ltt3."TransactionId"=t."Id" AND g2."Name" ILIKE '%'||p_search||'%'
           ))
    ORDER BY t."TransactionDate" DESC, t."Id" DESC
    OFFSET (p_page-1)*p_page_size LIMIT p_page_size;
END;
$$;

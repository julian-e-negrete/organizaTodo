-- OrganizaTodo – PostgreSQL initialization
-- Run once against the target database.  Idempotent: safe to re-run.
-- Requires PostgreSQL 14+.

-- ============================================================
-- TABLES
-- ============================================================

CREATE TABLE IF NOT EXISTS "Users" (
    "Id"           SERIAL       PRIMARY KEY,
    "Email"        VARCHAR(256) NOT NULL UNIQUE,
    "PasswordHash" VARCHAR(512) NOT NULL,
    "FullName"     VARCHAR(200) NOT NULL,
    "Currency"     VARCHAR(10)  NOT NULL DEFAULT 'ARS',
    "Role"         VARCHAR(20)  NOT NULL DEFAULT 'USER',
    "IsActive"     BOOLEAN      NOT NULL DEFAULT TRUE,
    "CreatedAt"    TIMESTAMP    NOT NULL DEFAULT NOW(),
    "LastAccessAt" TIMESTAMP    NULL
);

CREATE TABLE IF NOT EXISTS "PasswordResetTokens" (
    "Id"        SERIAL       PRIMARY KEY,
    "UserId"    INT          NOT NULL REFERENCES "Users"("Id") ON DELETE CASCADE,
    "Token"     VARCHAR(200) NOT NULL,
    "ExpiresAt" TIMESTAMP    NOT NULL,
    "UsedAt"    TIMESTAMP    NULL
);

CREATE TABLE IF NOT EXISTS "HousingServices" (
    "Id"          SERIAL       PRIMARY KEY,
    "UserId"      INT          NOT NULL REFERENCES "Users"("Id") ON DELETE CASCADE,
    "Name"        VARCHAR(100) NOT NULL,
    "Amount"      DECIMAL(18,2) NOT NULL,
    "DueDay"      INT          NOT NULL DEFAULT 1,
    "Periodicity" VARCHAR(20)  NOT NULL DEFAULT 'MONTHLY',
    "IsPaid"      BOOLEAN      NOT NULL DEFAULT FALSE,
    "PaidDate"    TIMESTAMP    NULL,
    "IsActive"    BOOLEAN      NOT NULL DEFAULT TRUE,
    "CreatedAt"   TIMESTAMP    NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS "CreditCardPurchases" (
    "Id"                 SERIAL        PRIMARY KEY,
    "UserId"             INT           NOT NULL REFERENCES "Users"("Id") ON DELETE CASCADE,
    "Description"        VARCHAR(200)  NOT NULL,
    "TotalAmount"        DECIMAL(18,2) NOT NULL,
    "Installments"       INT           NOT NULL DEFAULT 1,
    "CurrentInstallment" INT           NOT NULL DEFAULT 1,
    "InterestRate"       DECIMAL(5,2)  NOT NULL DEFAULT 0,
    "PurchaseDate"       TIMESTAMP     NOT NULL DEFAULT NOW(),
    "IsActive"           BOOLEAN       NOT NULL DEFAULT TRUE
);

CREATE TABLE IF NOT EXISTS "FixedLiabilities" (
    "Id"            SERIAL        PRIMARY KEY,
    "UserId"        INT           NOT NULL REFERENCES "Users"("Id") ON DELETE CASCADE,
    "Name"          VARCHAR(100)  NOT NULL,
    "MonthlyAmount" DECIMAL(18,2) NOT NULL,
    "DueDay"        INT           NULL,
    "IsActive"      BOOLEAN       NOT NULL DEFAULT TRUE,
    "IsPaid"        BOOLEAN       NOT NULL DEFAULT FALSE,
    "PaidDate"      TIMESTAMP     NULL,
    "CreatedAt"     TIMESTAMP     NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS "Income" (
    "Id"          SERIAL        PRIMARY KEY,
    "UserId"      INT           NOT NULL REFERENCES "Users"("Id") ON DELETE CASCADE,
    "Description" VARCHAR(200)  NOT NULL,
    "Amount"      DECIMAL(18,2) NOT NULL,
    "IncomeDate"  TIMESTAMP     NOT NULL DEFAULT NOW(),
    "Category"    VARCHAR(50)   NULL,
    "Month"       INT           NOT NULL,
    "Year"        INT           NOT NULL
);

CREATE TABLE IF NOT EXISTS "OtherExpenses" (
    "Id"          SERIAL        PRIMARY KEY,
    "UserId"      INT           NOT NULL REFERENCES "Users"("Id") ON DELETE CASCADE,
    "Description" VARCHAR(200)  NOT NULL,
    "Amount"      DECIMAL(18,2) NOT NULL,
    "ExpenseDate" TIMESTAMP     NOT NULL DEFAULT NOW(),
    "Month"       INT           NOT NULL,
    "Year"        INT           NOT NULL
);

CREATE TABLE IF NOT EXISTS "ShoppingLists" (
    "Id"        SERIAL       PRIMARY KEY,
    "UserId"    INT          NOT NULL REFERENCES "Users"("Id") ON DELETE CASCADE,
    "Name"      VARCHAR(100) NOT NULL DEFAULT 'Lista del Mes',
    "Month"     INT          NOT NULL,
    "Year"      INT          NOT NULL,
    "CreatedAt" TIMESTAMP    NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS "ShoppingListItems" (
    "Id"             SERIAL        PRIMARY KEY,
    "ShoppingListId" INT           NOT NULL REFERENCES "ShoppingLists"("Id") ON DELETE CASCADE,
    "ProductName"    VARCHAR(200)  NOT NULL,
    "Quantity"       INT           NOT NULL DEFAULT 1,
    "EstimatedPrice" DECIMAL(18,2) NULL,
    "Supermarket"    VARCHAR(50)   NULL,
    "Priority"       INT           NOT NULL DEFAULT 2
);

CREATE TABLE IF NOT EXISTS "MockProducts" (
    "Id"             SERIAL        PRIMARY KEY,
    "Name"           VARCHAR(200)  NOT NULL,
    "Category"       VARCHAR(100)  NOT NULL,
    "CotoPrice"      DECIMAL(18,2) NULL,
    "CarrefourPrice" DECIMAL(18,2) NULL,
    "Unit"           VARCHAR(50)   NOT NULL DEFAULT 'unidad',
    "LastUpdated"    TIMESTAMP     NOT NULL DEFAULT NOW()
);

-- ============================================================
-- FUNCTIONS – Users
-- ============================================================

CREATE OR REPLACE FUNCTION sp_users_create(
    email        VARCHAR(256),
    passwordhash VARCHAR(512),
    fullname     VARCHAR(200),
    currency     VARCHAR(10) DEFAULT 'ARS'
) RETURNS INT LANGUAGE plpgsql AS $$
DECLARE new_id INT;
BEGIN
    INSERT INTO "Users" ("Email","PasswordHash","FullName","Currency")
    VALUES (email, passwordhash, fullname, currency)
    RETURNING "Id" INTO new_id;
    RETURN new_id;
END;
$$;

CREATE OR REPLACE FUNCTION sp_users_get_by_email(email VARCHAR(256))
RETURNS TABLE(
    "Id" INT, "Email" VARCHAR, "PasswordHash" VARCHAR, "FullName" VARCHAR,
    "Currency" VARCHAR, "Role" VARCHAR, "IsActive" BOOLEAN,
    "CreatedAt" TIMESTAMP, "LastAccessAt" TIMESTAMP
) LANGUAGE plpgsql AS $$
BEGIN
    RETURN QUERY
    SELECT u."Id", u."Email", u."PasswordHash", u."FullName",
           u."Currency", u."Role", u."IsActive", u."CreatedAt", u."LastAccessAt"
    FROM "Users" u WHERE u."Email" = email;
END;
$$;

CREATE OR REPLACE FUNCTION sp_users_get_by_id(user_id INT)
RETURNS TABLE(
    "Id" INT, "Email" VARCHAR, "PasswordHash" VARCHAR, "FullName" VARCHAR,
    "Currency" VARCHAR, "Role" VARCHAR, "IsActive" BOOLEAN,
    "CreatedAt" TIMESTAMP, "LastAccessAt" TIMESTAMP
) LANGUAGE plpgsql AS $$
BEGIN
    RETURN QUERY
    SELECT u."Id", u."Email", u."PasswordHash", u."FullName",
           u."Currency", u."Role", u."IsActive", u."CreatedAt", u."LastAccessAt"
    FROM "Users" u WHERE u."Id" = user_id;
END;
$$;

CREATE OR REPLACE FUNCTION sp_users_email_exists(email VARCHAR(256))
RETURNS BOOLEAN LANGUAGE plpgsql AS $$
BEGIN
    RETURN EXISTS(SELECT 1 FROM "Users" WHERE "Email" = email);
END;
$$;

CREATE OR REPLACE FUNCTION sp_users_update_last_access(user_id INT)
RETURNS VOID LANGUAGE plpgsql AS $$
BEGIN
    UPDATE "Users" SET "LastAccessAt" = NOW() WHERE "Id" = user_id;
END;
$$;

CREATE OR REPLACE FUNCTION sp_users_update_profile(
    user_id  INT,
    fullname VARCHAR(200),
    currency VARCHAR(10)
) RETURNS VOID LANGUAGE plpgsql AS $$
BEGIN
    UPDATE "Users" SET "FullName" = fullname, "Currency" = currency WHERE "Id" = user_id;
END;
$$;

CREATE OR REPLACE FUNCTION sp_users_set_active(user_id INT, is_active BOOLEAN)
RETURNS VOID LANGUAGE plpgsql AS $$
BEGIN
    UPDATE "Users" SET "IsActive" = is_active WHERE "Id" = user_id;
END;
$$;

CREATE OR REPLACE FUNCTION sp_users_set_role(user_id INT, role VARCHAR(20))
RETURNS VOID LANGUAGE plpgsql AS $$
BEGIN
    UPDATE "Users" SET "Role" = role WHERE "Id" = user_id;
END;
$$;

CREATE OR REPLACE FUNCTION sp_users_delete(user_id INT)
RETURNS VOID LANGUAGE plpgsql AS $$
BEGIN
    DELETE FROM "Users" WHERE "Id" = user_id;
END;
$$;

CREATE OR REPLACE FUNCTION sp_users_get_all()
RETURNS TABLE(
    "Id" INT, "Email" VARCHAR, "FullName" VARCHAR, "Currency" VARCHAR,
    "Role" VARCHAR, "IsActive" BOOLEAN, "CreatedAt" TIMESTAMP, "LastAccessAt" TIMESTAMP
) LANGUAGE plpgsql AS $$
BEGIN
    RETURN QUERY
    SELECT u."Id", u."Email", u."FullName", u."Currency",
           u."Role", u."IsActive", u."CreatedAt", u."LastAccessAt"
    FROM "Users" u ORDER BY u."CreatedAt" DESC;
END;
$$;

-- ============================================================
-- FUNCTIONS – Housing Services
-- ============================================================

CREATE OR REPLACE FUNCTION sp_housing_services_create(
    user_id INT, name VARCHAR(100), amount DECIMAL(18,2), due_day INT, periodicity VARCHAR(20)
) RETURNS VOID LANGUAGE plpgsql AS $$
BEGIN
    INSERT INTO "HousingServices"("UserId","Name","Amount","DueDay","Periodicity")
    VALUES (user_id, name, amount, due_day, periodicity);
END;
$$;

CREATE OR REPLACE FUNCTION sp_housing_services_get_by_user_id(user_id INT)
RETURNS TABLE(
    "Id" INT,"UserId" INT,"Name" VARCHAR,"Amount" DECIMAL,"DueDay" INT,
    "Periodicity" VARCHAR,"IsPaid" BOOLEAN,"PaidDate" TIMESTAMP,"IsActive" BOOLEAN,"CreatedAt" TIMESTAMP
) LANGUAGE plpgsql AS $$
BEGIN
    RETURN QUERY
    SELECT h."Id",h."UserId",h."Name",h."Amount",h."DueDay",
           h."Periodicity",h."IsPaid",h."PaidDate",h."IsActive",h."CreatedAt"
    FROM "HousingServices" h
    WHERE h."UserId" = user_id AND h."IsActive" = TRUE ORDER BY h."Name";
END;
$$;

CREATE OR REPLACE FUNCTION sp_housing_services_get_by_id(id INT, user_id INT)
RETURNS TABLE(
    "Id" INT,"UserId" INT,"Name" VARCHAR,"Amount" DECIMAL,"DueDay" INT,
    "Periodicity" VARCHAR,"IsPaid" BOOLEAN,"PaidDate" TIMESTAMP,"IsActive" BOOLEAN,"CreatedAt" TIMESTAMP
) LANGUAGE plpgsql AS $$
BEGIN
    RETURN QUERY
    SELECT h."Id",h."UserId",h."Name",h."Amount",h."DueDay",
           h."Periodicity",h."IsPaid",h."PaidDate",h."IsActive",h."CreatedAt"
    FROM "HousingServices" h WHERE h."Id" = id AND h."UserId" = user_id;
END;
$$;

CREATE OR REPLACE FUNCTION sp_housing_services_update(
    id INT, user_id INT, name VARCHAR(100), amount DECIMAL(18,2), due_day INT, periodicity VARCHAR(20)
) RETURNS VOID LANGUAGE plpgsql AS $$
BEGIN
    UPDATE "HousingServices"
    SET "Name"=name,"Amount"=amount,"DueDay"=due_day,"Periodicity"=periodicity
    WHERE "Id"=id AND "UserId"=user_id;
END;
$$;

CREATE OR REPLACE FUNCTION sp_housing_services_delete(id INT, user_id INT)
RETURNS VOID LANGUAGE plpgsql AS $$
BEGIN
    UPDATE "HousingServices" SET "IsActive"=FALSE WHERE "Id"=id AND "UserId"=user_id;
END;
$$;

CREATE OR REPLACE FUNCTION sp_housing_services_mark_paid(id INT, user_id INT, is_paid BOOLEAN)
RETURNS VOID LANGUAGE plpgsql AS $$
BEGIN
    UPDATE "HousingServices"
    SET "IsPaid"=is_paid, "PaidDate"=CASE WHEN is_paid THEN NOW() ELSE NULL END
    WHERE "Id"=id AND "UserId"=user_id;
END;
$$;

CREATE OR REPLACE FUNCTION sp_housing_services_get_monthly_total(user_id INT)
RETURNS DECIMAL(18,2) LANGUAGE plpgsql AS $$
DECLARE total DECIMAL(18,2);
BEGIN
    SELECT COALESCE(SUM("Amount"),0) INTO total
    FROM "HousingServices"
    WHERE "UserId"=user_id AND "IsActive"=TRUE AND "Periodicity"='MONTHLY';
    RETURN total;
END;
$$;

-- ============================================================
-- FUNCTIONS – Credit Card
-- ============================================================

CREATE OR REPLACE FUNCTION sp_credit_card_create(
    user_id INT, description VARCHAR(200), total_amount DECIMAL(18,2),
    installments INT, interest_rate DECIMAL(5,2), purchase_date TIMESTAMP
) RETURNS VOID LANGUAGE plpgsql AS $$
BEGIN
    INSERT INTO "CreditCardPurchases"
        ("UserId","Description","TotalAmount","Installments","CurrentInstallment","InterestRate","PurchaseDate")
    VALUES (user_id, description, total_amount, installments, 1, interest_rate, purchase_date);
END;
$$;

CREATE OR REPLACE FUNCTION sp_credit_card_get_by_user_id(user_id INT)
RETURNS TABLE(
    "Id" INT,"UserId" INT,"Description" VARCHAR,"TotalAmount" DECIMAL,"Installments" INT,
    "CurrentInstallment" INT,"InterestRate" DECIMAL,"PurchaseDate" TIMESTAMP,"IsActive" BOOLEAN,
    "MonthlyInstallmentAmount" DECIMAL
) LANGUAGE plpgsql AS $$
BEGIN
    RETURN QUERY
    SELECT c."Id",c."UserId",c."Description",c."TotalAmount",c."Installments",
           c."CurrentInstallment",c."InterestRate",c."PurchaseDate",c."IsActive",
           CAST((c."TotalAmount"*(1+c."InterestRate"/100.0))/NULLIF(c."Installments",0) AS DECIMAL(18,2))
    FROM "CreditCardPurchases" c
    WHERE c."UserId"=user_id AND c."IsActive"=TRUE ORDER BY c."PurchaseDate" DESC;
END;
$$;

CREATE OR REPLACE FUNCTION sp_credit_card_get_by_id(id INT, user_id INT)
RETURNS TABLE(
    "Id" INT,"UserId" INT,"Description" VARCHAR,"TotalAmount" DECIMAL,"Installments" INT,
    "CurrentInstallment" INT,"InterestRate" DECIMAL,"PurchaseDate" TIMESTAMP,"IsActive" BOOLEAN
) LANGUAGE plpgsql AS $$
BEGIN
    RETURN QUERY
    SELECT c."Id",c."UserId",c."Description",c."TotalAmount",c."Installments",
           c."CurrentInstallment",c."InterestRate",c."PurchaseDate",c."IsActive"
    FROM "CreditCardPurchases" c WHERE c."Id"=id AND c."UserId"=user_id;
END;
$$;

CREATE OR REPLACE FUNCTION sp_credit_card_update(
    id INT, user_id INT, description VARCHAR(200),
    total_amount DECIMAL(18,2), installments INT, interest_rate DECIMAL(5,2)
) RETURNS VOID LANGUAGE plpgsql AS $$
BEGIN
    UPDATE "CreditCardPurchases"
    SET "Description"=description,"TotalAmount"=total_amount,
        "Installments"=installments,"InterestRate"=interest_rate
    WHERE "Id"=id AND "UserId"=user_id;
END;
$$;

CREATE OR REPLACE FUNCTION sp_credit_card_delete(id INT, user_id INT)
RETURNS VOID LANGUAGE plpgsql AS $$
BEGIN
    UPDATE "CreditCardPurchases" SET "IsActive"=FALSE WHERE "Id"=id AND "UserId"=user_id;
END;
$$;

CREATE OR REPLACE FUNCTION sp_credit_card_advance_installment(id INT, user_id INT)
RETURNS VOID LANGUAGE plpgsql AS $$
BEGIN
    UPDATE "CreditCardPurchases"
    SET "CurrentInstallment" = "CurrentInstallment" + 1,
        "IsActive" = CASE WHEN "CurrentInstallment" + 1 > "Installments" THEN FALSE ELSE TRUE END
    WHERE "Id"=id AND "UserId"=user_id;
END;
$$;

CREATE OR REPLACE FUNCTION sp_credit_card_get_monthly_total(user_id INT)
RETURNS DECIMAL(18,2) LANGUAGE plpgsql AS $$
DECLARE total DECIMAL(18,2);
BEGIN
    SELECT COALESCE(SUM(
        ("TotalAmount"*(1+"InterestRate"/100.0))/NULLIF("Installments",0)
    ),0) INTO total
    FROM "CreditCardPurchases"
    WHERE "UserId"=user_id AND "IsActive"=TRUE AND "CurrentInstallment"<="Installments";
    RETURN total;
END;
$$;

-- ============================================================
-- FUNCTIONS – Fixed Liabilities
-- ============================================================

CREATE OR REPLACE FUNCTION sp_fixed_liabilities_create(
    user_id INT, name VARCHAR(100), monthly_amount DECIMAL(18,2), due_day INT DEFAULT NULL
) RETURNS VOID LANGUAGE plpgsql AS $$
BEGIN
    INSERT INTO "FixedLiabilities"("UserId","Name","MonthlyAmount","DueDay")
    VALUES (user_id, name, monthly_amount, due_day);
END;
$$;

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

CREATE OR REPLACE FUNCTION sp_fixed_liabilities_update(
    id INT, user_id INT, name VARCHAR(100), monthly_amount DECIMAL(18,2), due_day INT DEFAULT NULL
) RETURNS VOID LANGUAGE plpgsql AS $$
BEGIN
    UPDATE "FixedLiabilities"
    SET "Name"=name,"MonthlyAmount"=monthly_amount,"DueDay"=due_day
    WHERE "Id"=id AND "UserId"=user_id;
END;
$$;

CREATE OR REPLACE FUNCTION sp_fixed_liabilities_delete(id INT, user_id INT)
RETURNS VOID LANGUAGE plpgsql AS $$
BEGIN
    UPDATE "FixedLiabilities" SET "IsActive"=FALSE WHERE "Id"=id AND "UserId"=user_id;
END;
$$;

CREATE OR REPLACE FUNCTION sp_fixed_liabilities_get_monthly_total(user_id INT)
RETURNS DECIMAL(18,2) LANGUAGE plpgsql AS $$
DECLARE total DECIMAL(18,2);
BEGIN
    SELECT COALESCE(SUM("MonthlyAmount"),0) INTO total
    FROM "FixedLiabilities" WHERE "UserId"=user_id AND "IsActive"=TRUE;
    RETURN total;
END;
$$;

-- ============================================================
-- FUNCTIONS – Income
-- ============================================================

CREATE OR REPLACE FUNCTION sp_income_create(
    user_id INT, description VARCHAR(200), amount DECIMAL(18,2),
    category VARCHAR(50), month INT, year INT
) RETURNS VOID LANGUAGE plpgsql AS $$
BEGIN
    INSERT INTO "Income"("UserId","Description","Amount","Category","Month","Year")
    VALUES (user_id, description, amount, category, month, year);
END;
$$;

CREATE OR REPLACE FUNCTION sp_income_get_by_user_id_and_period(user_id INT, month INT, year INT)
RETURNS TABLE(
    "Id" INT,"UserId" INT,"Description" VARCHAR,"Amount" DECIMAL,
    "IncomeDate" TIMESTAMP,"Category" VARCHAR,"Month" INT,"Year" INT
) LANGUAGE plpgsql AS $$
BEGIN
    RETURN QUERY
    SELECT i."Id",i."UserId",i."Description",i."Amount",
           i."IncomeDate",i."Category",i."Month",i."Year"
    FROM "Income" i
    WHERE i."UserId"=user_id AND i."Month"=month AND i."Year"=year
    ORDER BY i."IncomeDate" DESC;
END;
$$;

CREATE OR REPLACE FUNCTION sp_income_get_by_id(id INT, user_id INT)
RETURNS TABLE(
    "Id" INT,"UserId" INT,"Description" VARCHAR,"Amount" DECIMAL,
    "IncomeDate" TIMESTAMP,"Category" VARCHAR,"Month" INT,"Year" INT
) LANGUAGE plpgsql AS $$
BEGIN
    RETURN QUERY
    SELECT i."Id",i."UserId",i."Description",i."Amount",
           i."IncomeDate",i."Category",i."Month",i."Year"
    FROM "Income" i WHERE i."Id"=id AND i."UserId"=user_id;
END;
$$;

CREATE OR REPLACE FUNCTION sp_income_update(
    id INT, user_id INT, description VARCHAR(200), amount DECIMAL(18,2), category VARCHAR(50)
) RETURNS VOID LANGUAGE plpgsql AS $$
BEGIN
    UPDATE "Income" SET "Description"=description,"Amount"=amount,"Category"=category
    WHERE "Id"=id AND "UserId"=user_id;
END;
$$;

CREATE OR REPLACE FUNCTION sp_income_delete(id INT, user_id INT)
RETURNS VOID LANGUAGE plpgsql AS $$
BEGIN
    DELETE FROM "Income" WHERE "Id"=id AND "UserId"=user_id;
END;
$$;

CREATE OR REPLACE FUNCTION sp_income_get_monthly_total(user_id INT, month INT, year INT)
RETURNS DECIMAL(18,2) LANGUAGE plpgsql AS $$
DECLARE total DECIMAL(18,2);
BEGIN
    SELECT COALESCE(SUM("Amount"),0) INTO total
    FROM "Income" WHERE "UserId"=user_id AND "Month"=month AND "Year"=year;
    RETURN total;
END;
$$;

-- ============================================================
-- FUNCTIONS – Other Expenses
-- ============================================================

CREATE OR REPLACE FUNCTION sp_other_expenses_create(
    user_id INT, description VARCHAR(200), amount DECIMAL(18,2), month INT, year INT
) RETURNS VOID LANGUAGE plpgsql AS $$
BEGIN
    INSERT INTO "OtherExpenses"("UserId","Description","Amount","Month","Year")
    VALUES (user_id, description, amount, month, year);
END;
$$;

CREATE OR REPLACE FUNCTION sp_other_expenses_get_by_user_id_and_period(user_id INT, month INT, year INT)
RETURNS TABLE(
    "Id" INT,"UserId" INT,"Description" VARCHAR,"Amount" DECIMAL,
    "ExpenseDate" TIMESTAMP,"Month" INT,"Year" INT
) LANGUAGE plpgsql AS $$
BEGIN
    RETURN QUERY
    SELECT e."Id",e."UserId",e."Description",e."Amount",
           e."ExpenseDate",e."Month",e."Year"
    FROM "OtherExpenses" e
    WHERE e."UserId"=user_id AND e."Month"=month AND e."Year"=year
    ORDER BY e."ExpenseDate" DESC;
END;
$$;

CREATE OR REPLACE FUNCTION sp_other_expenses_get_by_id(id INT, user_id INT)
RETURNS TABLE(
    "Id" INT,"UserId" INT,"Description" VARCHAR,"Amount" DECIMAL,
    "ExpenseDate" TIMESTAMP,"Month" INT,"Year" INT
) LANGUAGE plpgsql AS $$
BEGIN
    RETURN QUERY
    SELECT e."Id",e."UserId",e."Description",e."Amount",
           e."ExpenseDate",e."Month",e."Year"
    FROM "OtherExpenses" e WHERE e."Id"=id AND e."UserId"=user_id;
END;
$$;

CREATE OR REPLACE FUNCTION sp_other_expenses_update(
    id INT, user_id INT, description VARCHAR(200), amount DECIMAL(18,2)
) RETURNS VOID LANGUAGE plpgsql AS $$
BEGIN
    UPDATE "OtherExpenses" SET "Description"=description,"Amount"=amount
    WHERE "Id"=id AND "UserId"=user_id;
END;
$$;

CREATE OR REPLACE FUNCTION sp_other_expenses_delete(id INT, user_id INT)
RETURNS VOID LANGUAGE plpgsql AS $$
BEGIN
    DELETE FROM "OtherExpenses" WHERE "Id"=id AND "UserId"=user_id;
END;
$$;

CREATE OR REPLACE FUNCTION sp_other_expenses_get_monthly_total(user_id INT, month INT, year INT)
RETURNS DECIMAL(18,2) LANGUAGE plpgsql AS $$
DECLARE total DECIMAL(18,2);
BEGIN
    SELECT COALESCE(SUM("Amount"),0) INTO total
    FROM "OtherExpenses" WHERE "UserId"=user_id AND "Month"=month AND "Year"=year;
    RETURN total;
END;
$$;

-- ============================================================
-- FUNCTION – Balance (combined monthly view)
-- ============================================================

CREATE OR REPLACE FUNCTION sp_balance_get_monthly(user_id INT, month INT, year INT)
RETURNS TABLE(
    "TotalIncome"          DECIMAL(18,2),
    "TotalServices"        DECIMAL(18,2),
    "TotalCreditCard"      DECIMAL(18,2),
    "TotalFixedLiabilities" DECIMAL(18,2),
    "TotalLiabilities"     DECIMAL(18,2),
    "TotalOtherExpenses"   DECIMAL(18,2),
    "RemainingBalance"     DECIMAL(18,2)
) LANGUAGE plpgsql AS $$
DECLARE
    v_income    DECIMAL(18,2);
    v_services  DECIMAL(18,2);
    v_cc        DECIMAL(18,2);
    v_fixed     DECIMAL(18,2);
    v_expenses  DECIMAL(18,2);
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
    WHERE "UserId"=user_id AND "IsActive"=TRUE AND "CurrentInstallment"<="Installments";

    SELECT COALESCE(SUM("MonthlyAmount"),0) INTO v_fixed
    FROM "FixedLiabilities" WHERE "UserId"=user_id AND "IsActive"=TRUE;

    SELECT COALESCE(SUM("Amount"),0) INTO v_expenses
    FROM "OtherExpenses" WHERE "UserId"=user_id AND "Month"=month AND "Year"=year;

    RETURN QUERY SELECT
        v_income,
        v_services,
        v_cc,
        v_fixed,
        v_services + v_cc + v_fixed,
        v_expenses,
        v_income - (v_services + v_cc + v_fixed) - v_expenses;
END;
$$;

-- ============================================================
-- FUNCTIONS – Shopping
-- ============================================================

CREATE OR REPLACE FUNCTION sp_shopping_get_or_create_list(user_id INT, month INT, year INT)
RETURNS TABLE(
    "Id" INT,"UserId" INT,"Name" VARCHAR,"Month" INT,"Year" INT,"CreatedAt" TIMESTAMP
) LANGUAGE plpgsql AS $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM "ShoppingLists" WHERE "UserId"=user_id AND "Month"=month AND "Year"=year) THEN
        INSERT INTO "ShoppingLists"("UserId","Month","Year") VALUES (user_id, month, year);
    END IF;
    RETURN QUERY
    SELECT l."Id",l."UserId",l."Name",l."Month",l."Year",l."CreatedAt"
    FROM "ShoppingLists" l WHERE l."UserId"=user_id AND l."Month"=month AND l."Year"=year;
END;
$$;

CREATE OR REPLACE FUNCTION sp_shopping_get_list_items(shopping_list_id INT, user_id INT)
RETURNS TABLE(
    "Id" INT,"ShoppingListId" INT,"ProductName" VARCHAR,"Quantity" INT,
    "EstimatedPrice" DECIMAL,"Supermarket" VARCHAR,"Priority" INT
) LANGUAGE plpgsql AS $$
BEGIN
    RETURN QUERY
    SELECT i."Id",i."ShoppingListId",i."ProductName",i."Quantity",
           i."EstimatedPrice",i."Supermarket",i."Priority"
    FROM "ShoppingListItems" i
    INNER JOIN "ShoppingLists" l ON l."Id"=i."ShoppingListId"
    WHERE i."ShoppingListId"=shopping_list_id AND l."UserId"=user_id
    ORDER BY i."Priority", i."ProductName";
END;
$$;

CREATE OR REPLACE FUNCTION sp_shopping_add_item(
    shopping_list_id INT, user_id INT, product_name VARCHAR(200),
    quantity INT, estimated_price DECIMAL, supermarket VARCHAR(50), priority INT
) RETURNS VOID LANGUAGE plpgsql AS $$
BEGIN
    IF EXISTS (SELECT 1 FROM "ShoppingLists" WHERE "Id"=shopping_list_id AND "UserId"=user_id) THEN
        INSERT INTO "ShoppingListItems"("ShoppingListId","ProductName","Quantity","EstimatedPrice","Supermarket","Priority")
        VALUES (shopping_list_id, product_name, quantity, estimated_price, supermarket, priority);
    END IF;
END;
$$;

CREATE OR REPLACE FUNCTION sp_shopping_delete_item(item_id INT, user_id INT)
RETURNS VOID LANGUAGE plpgsql AS $$
BEGIN
    DELETE FROM "ShoppingListItems"
    WHERE "Id"=item_id
    AND "ShoppingListId" IN (SELECT "Id" FROM "ShoppingLists" WHERE "UserId"=user_id);
END;
$$;

CREATE OR REPLACE FUNCTION sp_shopping_get_user_lists(user_id INT)
RETURNS TABLE(
    "Id" INT,"UserId" INT,"Name" VARCHAR,"Month" INT,"Year" INT,"CreatedAt" TIMESTAMP,
    "TotalEstimated" DECIMAL,"ItemCount" BIGINT
) LANGUAGE plpgsql AS $$
BEGIN
    RETURN QUERY
    SELECT l."Id",l."UserId",l."Name",l."Month",l."Year",l."CreatedAt",
           COALESCE(SUM(i."EstimatedPrice"*i."Quantity"),0)::DECIMAL(18,2),
           COUNT(i."Id")
    FROM "ShoppingLists" l
    LEFT JOIN "ShoppingListItems" i ON i."ShoppingListId"=l."Id"
    WHERE l."UserId"=user_id
    GROUP BY l."Id",l."UserId",l."Name",l."Month",l."Year",l."CreatedAt"
    ORDER BY l."Year" DESC, l."Month" DESC;
END;
$$;

-- ============================================================
-- FUNCTIONS – Mock Products
-- ============================================================

CREATE OR REPLACE FUNCTION sp_mock_products_search(query VARCHAR(200))
RETURNS TABLE(
    "Id" INT,"Name" VARCHAR,"Category" VARCHAR,
    "CotoPrice" DECIMAL,"CarrefourPrice" DECIMAL,"Unit" VARCHAR
) LANGUAGE plpgsql AS $$
BEGIN
    RETURN QUERY
    SELECT p."Id",p."Name",p."Category",p."CotoPrice",p."CarrefourPrice",p."Unit"
    FROM "MockProducts" p
    WHERE p."Name" ILIKE '%' || query || '%' OR p."Category" ILIKE '%' || query || '%'
    ORDER BY p."Name";
END;
$$;

CREATE OR REPLACE FUNCTION sp_mock_products_get_categories()
RETURNS TABLE("Category" VARCHAR) LANGUAGE plpgsql AS $$
BEGIN
    RETURN QUERY SELECT DISTINCT p."Category" FROM "MockProducts" p ORDER BY p."Category";
END;
$$;

CREATE OR REPLACE FUNCTION sp_mock_products_get_by_category(category VARCHAR(100))
RETURNS TABLE(
    "Id" INT,"Name" VARCHAR,"Category" VARCHAR,
    "CotoPrice" DECIMAL,"CarrefourPrice" DECIMAL,"Unit" VARCHAR
) LANGUAGE plpgsql AS $$
BEGIN
    RETURN QUERY
    SELECT p."Id",p."Name",p."Category",p."CotoPrice",p."CarrefourPrice",p."Unit"
    FROM "MockProducts" p WHERE p."Category"=category ORDER BY p."Name";
END;
$$;

-- ============================================================
-- FUNCTION – Admin stats
-- ============================================================

CREATE OR REPLACE FUNCTION sp_admin_get_stats()
RETURNS TABLE(
    "TotalUsers" INT, "ActiveUsers" INT, "AdminCount" INT,
    "TotalIncomeAllUsers" DECIMAL(18,2), "TotalIncomeRecords" INT
) LANGUAGE plpgsql AS $$
BEGIN
    RETURN QUERY SELECT
        (SELECT COUNT(*)::INT FROM "Users"),
        (SELECT COUNT(*)::INT FROM "Users" WHERE "IsActive"=TRUE),
        (SELECT COUNT(*)::INT FROM "Users" WHERE "Role"='ADMIN'),
        (SELECT COALESCE(SUM("Amount"),0)::DECIMAL(18,2) FROM "Income"),
        (SELECT COUNT(*)::INT FROM "Income");
END;
$$;

-- ============================================================
-- SEED – Mock Supermarket Products
-- ============================================================

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM "MockProducts" LIMIT 1) THEN
        INSERT INTO "MockProducts"("Name","Category","CotoPrice","CarrefourPrice","Unit") VALUES
        ('Leche entera La Serenísima 1L',        'Lácteos',             850.00,  820.00,  'litro'),
        ('Queso cremoso Mila x400g',             'Lácteos',            1850.00, 1920.00, '400g'),
        ('Yogur natural Danone x125g',           'Lácteos',             450.00,  430.00,  'unidad'),
        ('Manteca La Serenísima 200g',           'Lácteos',            1050.00, 1020.00, '200g'),
        ('Arroz largo fino Gallo Oro 1kg',       'Almacén',             780.00,  760.00,  'kg'),
        ('Fideos tallarines Don Vicente 500g',   'Almacén',             520.00,  510.00,  '500g'),
        ('Aceite girasol Natura 1.5L',           'Almacén',            1950.00, 1890.00, '1.5L'),
        ('Azúcar Ledesma 1kg',                   'Almacén',             680.00,  660.00,  'kg'),
        ('Harina 000 Blancaflor 1kg',            'Almacén',             620.00,  600.00,  'kg'),
        ('Tomate entero La Campagnola 400g',     'Almacén',             680.00,  660.00,  '400g'),
        ('Yerba Rosamonte 1kg',                  'Almacén',            1850.00, 1780.00, 'kg'),
        ('Café molido La Virginia 250g',         'Almacén',            1450.00, 1380.00, '250g'),
        ('Galletitas Oreo 117g',                 'Almacén',             750.00,  720.00,  '117g'),
        ('Mermelada Arcor frutilla 390g',        'Almacén',             980.00,  950.00,  '390g'),
        ('Atún en aceite Alicia 170g',           'Almacén',             750.00,  720.00,  '170g'),
        ('Pan lactal Bimbo 500g',                'Panadería',            850.00,  820.00,  '500g'),
        ('Huevos blancos docena',                'Frescos',            1800.00, 1750.00, 'docena'),
        ('Pollo entero x kg',                    'Carnes',             1950.00, 1980.00, 'kg'),
        ('Carne picada común x kg',             'Carnes',             2800.00, 2850.00, 'kg'),
        ('Coca-Cola 2.25L',                      'Bebidas',            1450.00, 1380.00, 'unidad'),
        ('Agua mineral Villavicencio 2L',        'Bebidas',             680.00,  650.00,  'unidad'),
        ('Cerveza Quilmes 1L',                   'Bebidas',            1250.00, 1200.00, 'unidad'),
        ('Detergente Magistral 750ml',           'Limpieza',            820.00,  800.00,  '750ml'),
        ('Lavandina Lejía 1L',                   'Limpieza',            450.00,  430.00,  'litro'),
        ('Jabón en polvo Skip 800g',             'Limpieza',           1850.00, 1780.00, '800g'),
        ('Limpiador multiuso Mr. Músculo 500ml', 'Limpieza',            980.00,  950.00,  '500ml'),
        ('Papel higiénico Elite x4',            'Higiene',             980.00,  950.00,  'pack x4'),
        ('Shampoo Pantene 400ml',                'Higiene',            1650.00, 1580.00, '400ml'),
        ('Jabón de tocador Dove 90g x3',         'Higiene',             880.00,  850.00,  'pack x3'),
        ('Desodorante Rexona aerosol 150ml',     'Higiene',            1250.00, 1200.00, '150ml'),
        ('Papa x kg',                            'Verduras y Frutas',   650.00,  620.00,  'kg'),
        ('Cebolla x kg',                         'Verduras y Frutas',   480.00,  460.00,  'kg'),
        ('Tomate redondo x kg',                  'Verduras y Frutas',   890.00,  850.00,  'kg'),
        ('Banana x kg',                          'Verduras y Frutas',   620.00,  590.00,  'kg'),
        ('Manzana roja x kg',                    'Verduras y Frutas',   780.00,  750.00,  'kg');
    END IF;
END;
$$;

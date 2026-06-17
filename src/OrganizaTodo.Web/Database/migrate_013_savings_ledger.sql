-- Migration 013: Savings/Debt Ledger module
-- New, additive module: tags + immutable transaction ledger with running balance,
-- expense/income/interest/initial transaction types, and monthly compound interest.
-- Does NOT modify the existing "Savings"/"AssetPurchases" tables or functions.

-- ============================================================
-- TABLES – Ledger
-- ============================================================

CREATE TABLE IF NOT EXISTS "LedgerTags" (
    "Id"        SERIAL        PRIMARY KEY,
    "UserId"    INT           NOT NULL REFERENCES "Users"("Id") ON DELETE CASCADE,
    "Name"      VARCHAR(50)   NOT NULL,
    "CreatedAt" TIMESTAMP     NOT NULL DEFAULT NOW(),
    UNIQUE ("UserId", "Name")
);

CREATE TABLE IF NOT EXISTS "LedgerTransactions" (
    "Id"                  SERIAL        PRIMARY KEY,
    "UserId"              INT           NOT NULL REFERENCES "Users"("Id") ON DELETE CASCADE,
    "Amount"              DECIMAL(18,2) NOT NULL,
    "Type"                VARCHAR(20)   NOT NULL CHECK ("Type" IN ('expense','income','interest','initial')),
    "Description"         VARCHAR(300)  NOT NULL DEFAULT '',
    "TransactionDate"     TIMESTAMP     NOT NULL,
    "BalanceAfter"        DECIMAL(18,2) NOT NULL,
    "GeneratesInterest"   BOOLEAN       NOT NULL DEFAULT FALSE,
    "InterestRate"        DECIMAL(9,4)  NULL,
    "ParentTransactionId" INT           NULL REFERENCES "LedgerTransactions"("Id") ON DELETE SET NULL,
    "CreatedAt"           TIMESTAMP     NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS "LedgerTransactionTags" (
    "TransactionId" INT NOT NULL REFERENCES "LedgerTransactions"("Id") ON DELETE CASCADE,
    "TagId"         INT NOT NULL REFERENCES "LedgerTags"("Id") ON DELETE CASCADE,
    PRIMARY KEY ("TransactionId", "TagId")
);

CREATE INDEX IF NOT EXISTS "IX_LedgerTransactions_UserDate" ON "LedgerTransactions"("UserId","TransactionDate");
CREATE INDEX IF NOT EXISTS "IX_LedgerTransactions_UserType" ON "LedgerTransactions"("UserId","Type");
CREATE INDEX IF NOT EXISTS "IX_LedgerTransactionTags_TagId" ON "LedgerTransactionTags"("TagId");

-- ============================================================
-- FUNCTIONS – Ledger Tags
-- ============================================================

CREATE OR REPLACE FUNCTION sp_ledger_tags_get_or_create(p_user_id INT, p_name VARCHAR(50))
RETURNS TABLE("Id" INT,"UserId" INT,"Name" VARCHAR,"CreatedAt" TIMESTAMP)
LANGUAGE plpgsql AS $$
#variable_conflict use_column
BEGIN
    INSERT INTO "LedgerTags"("UserId","Name")
    VALUES (p_user_id, lower(trim(p_name)))
    ON CONFLICT ("UserId","Name") DO NOTHING;

    RETURN QUERY
    SELECT t."Id",t."UserId",t."Name",t."CreatedAt"
    FROM "LedgerTags" t
    WHERE t."UserId"=p_user_id AND t."Name"=lower(trim(p_name));
END;
$$;

CREATE OR REPLACE FUNCTION sp_ledger_tags_get_by_user_id(p_user_id INT)
RETURNS TABLE("Id" INT,"UserId" INT,"Name" VARCHAR,"CreatedAt" TIMESTAMP,"UsageCount" BIGINT)
LANGUAGE plpgsql AS $$
BEGIN
    RETURN QUERY
    SELECT t."Id",t."UserId",t."Name",t."CreatedAt", COUNT(ltt."TransactionId")
    FROM "LedgerTags" t
    LEFT JOIN "LedgerTransactionTags" ltt ON ltt."TagId"=t."Id"
    WHERE t."UserId"=p_user_id
    GROUP BY t."Id",t."UserId",t."Name",t."CreatedAt"
    ORDER BY t."Name";
END;
$$;

CREATE OR REPLACE FUNCTION sp_ledger_tags_link(p_transaction_id INT, p_tag_id INT)
RETURNS VOID LANGUAGE plpgsql AS $$
BEGIN
    INSERT INTO "LedgerTransactionTags"("TransactionId","TagId")
    VALUES (p_transaction_id, p_tag_id)
    ON CONFLICT DO NOTHING;
END;
$$;

-- ============================================================
-- FUNCTIONS – Ledger Balance
-- ============================================================

CREATE OR REPLACE FUNCTION sp_ledger_balance_get_current(p_user_id INT)
RETURNS DECIMAL(18,2) LANGUAGE plpgsql AS $$
DECLARE v_balance DECIMAL(18,2);
BEGIN
    SELECT "BalanceAfter" INTO v_balance
    FROM "LedgerTransactions"
    WHERE "UserId"=p_user_id
    ORDER BY "TransactionDate" DESC, "Id" DESC
    LIMIT 1;
    RETURN COALESCE(v_balance, 0);
END;
$$;

CREATE OR REPLACE FUNCTION sp_ledger_balance_get_as_of(p_user_id INT, p_as_of TIMESTAMP)
RETURNS DECIMAL(18,2) LANGUAGE plpgsql AS $$
DECLARE v_balance DECIMAL(18,2);
BEGIN
    SELECT "BalanceAfter" INTO v_balance
    FROM "LedgerTransactions"
    WHERE "UserId"=p_user_id AND "TransactionDate" <= p_as_of
    ORDER BY "TransactionDate" DESC, "Id" DESC
    LIMIT 1;
    RETURN COALESCE(v_balance, 0);
END;
$$;

CREATE OR REPLACE FUNCTION sp_ledger_recompute_balances(p_user_id INT)
RETURNS VOID LANGUAGE plpgsql AS $$
BEGIN
    UPDATE "LedgerTransactions" t
    SET "BalanceAfter" = sub."RunningBalance"
    FROM (
        SELECT "Id", SUM("Amount") OVER (ORDER BY "TransactionDate", "Id") AS "RunningBalance"
        FROM "LedgerTransactions"
        WHERE "UserId" = p_user_id
    ) sub
    WHERE t."Id" = sub."Id" AND t."UserId" = p_user_id AND t."BalanceAfter" IS DISTINCT FROM sub."RunningBalance";
END;
$$;

-- ============================================================
-- FUNCTIONS – Ledger Transactions: create
-- ============================================================

CREATE OR REPLACE FUNCTION sp_ledger_transaction_create_expense(
    p_user_id INT, p_amount DECIMAL(18,2), p_description VARCHAR(300), p_transaction_date TIMESTAMP,
    p_generates_interest BOOLEAN, p_interest_rate DECIMAL(9,4)
) RETURNS INT LANGUAGE plpgsql AS $$
DECLARE
    v_amount DECIMAL(18,2) := -ABS(p_amount);
    v_balance_after DECIMAL(18,2);
    v_id INT;
BEGIN
    v_balance_after := sp_ledger_balance_get_current(p_user_id) + v_amount;
    INSERT INTO "LedgerTransactions"
        ("UserId","Amount","Type","Description","TransactionDate","BalanceAfter","GeneratesInterest","InterestRate")
    VALUES
        (p_user_id, v_amount, 'expense', COALESCE(p_description,''), p_transaction_date, v_balance_after,
         p_generates_interest, CASE WHEN p_generates_interest THEN p_interest_rate ELSE NULL END)
    RETURNING "Id" INTO v_id;
    PERFORM sp_ledger_recompute_balances(p_user_id);
    RETURN v_id;
END;
$$;

CREATE OR REPLACE FUNCTION sp_ledger_transaction_create_income(
    p_user_id INT, p_amount DECIMAL(18,2), p_description VARCHAR(300), p_transaction_date TIMESTAMP
) RETURNS INT LANGUAGE plpgsql AS $$
DECLARE
    v_amount DECIMAL(18,2) := ABS(p_amount);
    v_balance_after DECIMAL(18,2);
    v_id INT;
BEGIN
    v_balance_after := sp_ledger_balance_get_current(p_user_id) + v_amount;
    INSERT INTO "LedgerTransactions"
        ("UserId","Amount","Type","Description","TransactionDate","BalanceAfter")
    VALUES
        (p_user_id, v_amount, 'income', COALESCE(p_description,''), p_transaction_date, v_balance_after)
    RETURNING "Id" INTO v_id;
    PERFORM sp_ledger_recompute_balances(p_user_id);
    RETURN v_id;
END;
$$;

CREATE OR REPLACE FUNCTION sp_ledger_transaction_create_initial(
    p_user_id INT, p_amount DECIMAL(18,2), p_transaction_date TIMESTAMP
) RETURNS INT LANGUAGE plpgsql AS $$
DECLARE v_id INT;
BEGIN
    INSERT INTO "LedgerTransactions"
        ("UserId","Amount","Type","Description","TransactionDate","BalanceAfter")
    SELECT p_user_id, p_amount, 'initial', 'Saldo inicial migrado de Ahorros', p_transaction_date, p_amount
    WHERE NOT EXISTS (SELECT 1 FROM "LedgerTransactions" WHERE "UserId"=p_user_id)
    RETURNING "Id" INTO v_id;
    PERFORM sp_ledger_recompute_balances(p_user_id);
    RETURN v_id;
END;
$$;

CREATE OR REPLACE FUNCTION sp_ledger_transaction_create_interest(
    p_user_id INT, p_amount DECIMAL(18,2), p_transaction_date TIMESTAMP,
    p_interest_rate DECIMAL(9,4), p_parent_transaction_id INT, p_balance_before DECIMAL(18,2)
) RETURNS INT LANGUAGE plpgsql AS $$
DECLARE
    v_amount DECIMAL(18,2) := -ABS(p_amount);
    v_balance_after DECIMAL(18,2);
    v_id INT;
BEGIN
    v_balance_after := p_balance_before + v_amount;
    INSERT INTO "LedgerTransactions"
        ("UserId","Amount","Type","Description","TransactionDate","BalanceAfter","InterestRate","ParentTransactionId")
    VALUES
        (p_user_id, v_amount, 'interest', 'Interés mensual', p_transaction_date, v_balance_after,
         p_interest_rate, p_parent_transaction_id)
    RETURNING "Id" INTO v_id;
    PERFORM sp_ledger_recompute_balances(p_user_id);
    RETURN v_id;
END;
$$;

-- ============================================================
-- FUNCTIONS – Ledger History / Detail / Chart
-- ============================================================

CREATE OR REPLACE FUNCTION sp_ledger_history_get(
    p_user_id INT, p_date_from TIMESTAMP, p_date_to TIMESTAMP, p_type VARCHAR(20),
    p_tag_ids INT[], p_amount_min DECIMAL(18,2), p_amount_max DECIMAL(18,2),
    p_only_with_interest BOOLEAN, p_only_negative_balance BOOLEAN, p_search VARCHAR(200),
    p_page INT, p_page_size INT
) RETURNS TABLE(
    "Id" INT,"UserId" INT,"Amount" DECIMAL,"Type" VARCHAR,"Description" VARCHAR,
    "TransactionDate" TIMESTAMP,"BalanceAfter" DECIMAL,"GeneratesInterest" BOOLEAN,
    "InterestRate" DECIMAL,"ParentTransactionId" INT,"CreatedAt" TIMESTAMP,
    "Tags" TEXT,"TotalCount" BIGINT
) LANGUAGE plpgsql AS $$
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

CREATE OR REPLACE FUNCTION sp_ledger_chart_get_balance_series(p_user_id INT)
RETURNS TABLE("Id" INT,"TransactionDate" TIMESTAMP,"BalanceAfter" DECIMAL)
LANGUAGE plpgsql AS $$
BEGIN
    RETURN QUERY
    SELECT t."Id",t."TransactionDate",t."BalanceAfter"
    FROM "LedgerTransactions" t
    WHERE t."UserId"=p_user_id
    ORDER BY t."TransactionDate" ASC, t."Id" ASC;
END;
$$;

CREATE OR REPLACE FUNCTION sp_ledger_transaction_get_detail(p_id INT, p_user_id INT)
RETURNS TABLE(
    "Id" INT,"UserId" INT,"Amount" DECIMAL,"Type" VARCHAR,"Description" VARCHAR,
    "TransactionDate" TIMESTAMP,"BalanceAfter" DECIMAL,"GeneratesInterest" BOOLEAN,
    "InterestRate" DECIMAL,"ParentTransactionId" INT,"CreatedAt" TIMESTAMP,
    "Tags" TEXT,"ParentDescription" VARCHAR,"ParentTransactionDate" TIMESTAMP
) LANGUAGE plpgsql AS $$
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
    WHERE t."Id"=p_id AND t."UserId"=p_user_id;
END;
$$;

CREATE OR REPLACE FUNCTION sp_ledger_transaction_get_children(p_id INT, p_user_id INT)
RETURNS TABLE("Id" INT,"Amount" DECIMAL,"TransactionDate" TIMESTAMP,"InterestRate" DECIMAL,"BalanceAfter" DECIMAL)
LANGUAGE plpgsql AS $$
BEGIN
    RETURN QUERY
    SELECT t."Id",t."Amount",t."TransactionDate",t."InterestRate",t."BalanceAfter"
    FROM "LedgerTransactions" t
    WHERE t."ParentTransactionId"=p_id AND t."UserId"=p_user_id
    ORDER BY t."TransactionDate";
END;
$$;

-- ============================================================
-- FUNCTIONS – Ledger Interest Job Support
-- ============================================================

CREATE OR REPLACE FUNCTION sp_ledger_transactions_get_count(p_user_id INT)
RETURNS BIGINT LANGUAGE plpgsql AS $$
DECLARE v_count BIGINT;
BEGIN
    SELECT COUNT(*) INTO v_count FROM "LedgerTransactions" WHERE "UserId"=p_user_id;
    RETURN v_count;
END;
$$;

CREATE OR REPLACE FUNCTION sp_ledger_anchor_expense_get_latest_for_month(p_user_id INT, p_month_end TIMESTAMP)
RETURNS TABLE("Id" INT,"InterestRate" DECIMAL,"TransactionDate" TIMESTAMP)
LANGUAGE plpgsql AS $$
BEGIN
    RETURN QUERY
    SELECT t."Id",t."InterestRate",t."TransactionDate"
    FROM "LedgerTransactions" t
    WHERE t."UserId"=p_user_id AND t."Type"='expense' AND t."GeneratesInterest"=TRUE
      AND t."TransactionDate" <= p_month_end
    ORDER BY t."TransactionDate" DESC, t."Id" DESC
    LIMIT 1;
END;
$$;

CREATE OR REPLACE FUNCTION sp_ledger_interest_get_pointers(p_user_id INT)
RETURNS TABLE("LastInterestDate" TIMESTAMP,"FirstAnchorExpenseDate" TIMESTAMP)
LANGUAGE plpgsql AS $$
BEGIN
    RETURN QUERY
    SELECT
        (SELECT MAX("TransactionDate") FROM "LedgerTransactions" WHERE "UserId"=p_user_id AND "Type"='interest'),
        (SELECT MIN("TransactionDate") FROM "LedgerTransactions" WHERE "UserId"=p_user_id AND "Type"='expense' AND "GeneratesInterest"=TRUE);
END;
$$;

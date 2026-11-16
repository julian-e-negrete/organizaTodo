-- ============================================================
-- MIGRATION 019 — Bill amount history
--
-- Every change to a bill's amount (HousingServices."Amount",
-- FixedLiabilities."MonthlyAmount") is logged in BillAmountHistory by a
-- trigger, so it is recorded whatever made the change: the web form, the
-- desktop app, a sync push, or the Jarvis assistant. New bills log their
-- starting amount (OldAmount NULL).
--
-- Callers may label a change for the log inside their transaction:
--   SELECT set_config('organizatodo.change_source', 'jarvis', true);
--   SELECT set_config('organizatodo.change_note', 'aumento noviembre', true);
-- Unlabelled changes are logged as source 'app'.
--
-- sp_bill_set_amount is the single entry point for assistants: it changes
-- only the amount, only for the given user, and labels the change. It is
-- SECURITY DEFINER so an assistant role can be granted EXECUTE on it alone,
-- with no UPDATE rights on the tables.
-- Idempotent: safe to run more than once.
-- ============================================================

CREATE TABLE IF NOT EXISTS "BillAmountHistory" (
    "Id"        SERIAL        PRIMARY KEY,
    "UserId"    INT           NOT NULL REFERENCES "Users"("Id") ON DELETE CASCADE,
    "BillType"  VARCHAR(10)   NOT NULL CHECK ("BillType" IN ('HOUSING', 'FIXED')),
    "BillId"    INT           NOT NULL,
    "BillName"  VARCHAR(100)  NOT NULL,
    "OldAmount" DECIMAL(18,2) NULL,
    "NewAmount" DECIMAL(18,2) NOT NULL,
    "Source"    VARCHAR(20)   NOT NULL DEFAULT 'app',
    "Note"      VARCHAR(200)  NULL,
    "ChangedAt" TIMESTAMPTZ   NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS "IX_BillAmountHistory_Bill"
    ON "BillAmountHistory" ("UserId", "BillType", "BillId", "ChangedAt" DESC);

-- Trigger: one function for both tables (TG_TABLE_NAME picks the column).
-- SECURITY DEFINER so whoever may update a bill can also write its log line,
-- without being granted INSERT on the history table itself.
CREATE OR REPLACE FUNCTION fn_log_bill_amount_change()
RETURNS TRIGGER LANGUAGE plpgsql SECURITY DEFINER SET search_path = public AS $$
DECLARE
    v_type   VARCHAR(10);
    v_old    DECIMAL(18,2);
    v_new    DECIMAL(18,2);
    v_source VARCHAR(20) := COALESCE(NULLIF(current_setting('organizatodo.change_source', true), ''), 'app');
    v_note   VARCHAR(200) := NULLIF(current_setting('organizatodo.change_note', true), '');
BEGIN
    IF TG_TABLE_NAME = 'HousingServices' THEN
        v_type := 'HOUSING';
        v_new  := NEW."Amount";
        v_old  := CASE WHEN TG_OP = 'UPDATE' THEN OLD."Amount" END;
    ELSE
        v_type := 'FIXED';
        v_new  := NEW."MonthlyAmount";
        v_old  := CASE WHEN TG_OP = 'UPDATE' THEN OLD."MonthlyAmount" END;
    END IF;

    IF TG_OP = 'UPDATE' AND v_old IS NOT DISTINCT FROM v_new THEN
        RETURN NEW;
    END IF;

    INSERT INTO "BillAmountHistory" ("UserId", "BillType", "BillId", "BillName", "OldAmount", "NewAmount", "Source", "Note")
    VALUES (NEW."UserId", v_type, NEW."Id", NEW."Name", v_old, v_new, LEFT(v_source, 20), LEFT(v_note, 200));
    RETURN NEW;
END;
$$;

DROP TRIGGER IF EXISTS trg_log_amount_change ON "HousingServices";
CREATE TRIGGER trg_log_amount_change
    AFTER INSERT OR UPDATE OF "Amount" ON "HousingServices"
    FOR EACH ROW EXECUTE FUNCTION fn_log_bill_amount_change();

DROP TRIGGER IF EXISTS trg_log_amount_change ON "FixedLiabilities";
CREATE TRIGGER trg_log_amount_change
    AFTER INSERT OR UPDATE OF "MonthlyAmount" ON "FixedLiabilities"
    FOR EACH ROW EXECUTE FUNCTION fn_log_bill_amount_change();

-- History of one bill, newest first.
CREATE OR REPLACE FUNCTION sp_bill_amount_history_get(p_user_id INT, p_bill_type VARCHAR(10), p_bill_id INT)
RETURNS TABLE(
    "Id" INT, "BillType" VARCHAR, "BillId" INT, "BillName" VARCHAR, "OldAmount" DECIMAL, "NewAmount" DECIMAL,
    "Source" VARCHAR, "Note" VARCHAR, "ChangedAt" TIMESTAMPTZ
) LANGUAGE plpgsql AS $$
BEGIN
    RETURN QUERY
    SELECT h."Id", h."BillType", h."BillId", h."BillName", h."OldAmount", h."NewAmount", h."Source", h."Note", h."ChangedAt"
    FROM "BillAmountHistory" h
    WHERE h."UserId" = p_user_id AND h."BillType" = p_bill_type AND h."BillId" = p_bill_id
    ORDER BY h."ChangedAt" DESC, h."Id" DESC;
END;
$$;

-- Latest changes across all of a user's bills, newest first.
CREATE OR REPLACE FUNCTION sp_bill_amount_history_get_by_user(p_user_id INT, p_limit INT DEFAULT 50)
RETURNS TABLE(
    "Id" INT, "BillType" VARCHAR, "BillId" INT, "BillName" VARCHAR, "OldAmount" DECIMAL, "NewAmount" DECIMAL,
    "Source" VARCHAR, "Note" VARCHAR, "ChangedAt" TIMESTAMPTZ
) LANGUAGE plpgsql AS $$
BEGIN
    RETURN QUERY
    SELECT h."Id", h."BillType", h."BillId", h."BillName", h."OldAmount", h."NewAmount", h."Source", h."Note", h."ChangedAt"
    FROM "BillAmountHistory" h
    WHERE h."UserId" = p_user_id
    ORDER BY h."ChangedAt" DESC, h."Id" DESC
    LIMIT GREATEST(1, LEAST(COALESCE(p_limit, 50), 500));
END;
$$;

-- Change one bill's amount and label the change. Returns the bill's name and
-- the old/new amounts; raises if the bill doesn't exist for that user, is
-- deleted, or the amount is out of range. Nothing else on the bill changes.
CREATE OR REPLACE FUNCTION sp_bill_set_amount(
    p_user_id INT, p_bill_type VARCHAR(10), p_bill_id INT, p_new_amount DECIMAL(18,2),
    p_source VARCHAR(20) DEFAULT 'app', p_note VARCHAR(200) DEFAULT NULL
) RETURNS TABLE("BillName" VARCHAR, "OldAmount" DECIMAL, "NewAmount" DECIMAL)
LANGUAGE plpgsql SECURITY DEFINER SET search_path = public AS $$
DECLARE
    v_name VARCHAR(100);
    v_old  DECIMAL(18,2);
BEGIN
    IF p_new_amount IS NULL OR p_new_amount <= 0 OR p_new_amount >= 1000000000 THEN
        RAISE EXCEPTION 'Amount must be greater than 0 and less than 1,000,000,000';
    END IF;

    IF p_bill_type = 'HOUSING' THEN
        SELECT h."Name", h."Amount" INTO v_name, v_old FROM "HousingServices" h
        WHERE h."Id" = p_bill_id AND h."UserId" = p_user_id AND h."IsActive" = TRUE
        FOR UPDATE;
    ELSIF p_bill_type = 'FIXED' THEN
        SELECT f."Name", f."MonthlyAmount" INTO v_name, v_old FROM "FixedLiabilities" f
        WHERE f."Id" = p_bill_id AND f."UserId" = p_user_id AND f."IsActive" = TRUE
        FOR UPDATE;
    ELSE
        RAISE EXCEPTION 'Unknown bill type %', p_bill_type;
    END IF;

    IF v_name IS NULL THEN
        RAISE EXCEPTION 'Bill % % not found for this user', p_bill_type, p_bill_id;
    END IF;

    PERFORM set_config('organizatodo.change_source', COALESCE(NULLIF(p_source, ''), 'app'), true);
    PERFORM set_config('organizatodo.change_note', COALESCE(p_note, ''), true);

    IF p_bill_type = 'HOUSING' THEN
        UPDATE "HousingServices" SET "Amount" = p_new_amount WHERE "Id" = p_bill_id AND "UserId" = p_user_id;
    ELSE
        UPDATE "FixedLiabilities" SET "MonthlyAmount" = p_new_amount WHERE "Id" = p_bill_id AND "UserId" = p_user_id;
    END IF;

    PERFORM set_config('organizatodo.change_source', '', true);
    PERFORM set_config('organizatodo.change_note', '', true);

    RETURN QUERY SELECT v_name, v_old, p_new_amount;
END;
$$;

-- Assistant roles exist only on installs that set them up (the home server).
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'jarvis_ro') THEN
        GRANT SELECT ON "BillAmountHistory" TO jarvis_ro;
        GRANT EXECUTE ON FUNCTION sp_bill_amount_history_get(INT, VARCHAR, INT),
                                  sp_bill_amount_history_get_by_user(INT, INT) TO jarvis_ro;
    END IF;
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'jarvis_pay') THEN
        GRANT EXECUTE ON FUNCTION sp_bill_set_amount(INT, VARCHAR, INT, DECIMAL, VARCHAR, VARCHAR) TO jarvis_pay;
    END IF;
END $$;

-- SECURITY DEFINER functions are callable by PUBLIC by default; keep the
-- amount setter to the app's own role and the roles granted above.
REVOKE EXECUTE ON FUNCTION sp_bill_set_amount(INT, VARCHAR, INT, DECIMAL, VARCHAR, VARCHAR) FROM PUBLIC;

-- ============================================================
-- MIGRATION 015 — Sync infrastructure: SyncId/UpdatedAt/IsDeleted
-- on every user-data table, an auto-maintained UpdatedAt trigger,
-- and a SyncTokens table for remote-linked desktop devices.
--
-- LedgerTransactionTags' composite PK (TransactionId, TagId) is
-- converted to a surrogate Id (uniqueness preserved via a UNIQUE
-- constraint) so it fits the same sync scheme as every other table.
-- ============================================================

-- Conditional, not unconditional: only auto-stamp NOW() when the caller's
-- UPDATE didn't itself change UpdatedAt (the normal app-code path, whose
-- existing sp_*_update functions never mention UpdatedAt in their SET
-- clause, so NEW = OLD for that column going into the trigger). The sync
-- push path (Task 6) explicitly sets UpdatedAt to the pushing device's own
-- timestamp — if this trigger stamped NOW() unconditionally, that explicit
-- value would be silently clobbered on every synced write, destroying
-- last-write-wins correctness. An unconditional trigger looks right until
-- you write the code that needs to preserve a caller-supplied timestamp.
CREATE OR REPLACE FUNCTION fn_set_updated_at()
RETURNS TRIGGER LANGUAGE plpgsql AS $$
BEGIN
    IF NEW."UpdatedAt" IS NOT DISTINCT FROM OLD."UpdatedAt" THEN
        NEW."UpdatedAt" = NOW();
    END IF;
    RETURN NEW;
END;
$$;

-- One block per table: add the three sync columns, then attach the trigger.
-- Table list (16): HousingServices, CreditCardPurchases, FixedLiabilities,
-- Income, OtherExpenses, Savings, ShoppingLists, ShoppingListItems,
-- AssetPurchases, Vehicles, VehicleMonthlyPayments, VehicleMaintenanceLogs,
-- RecurringIncomes, LedgerTags, LedgerTransactions, LedgerTransactionTags.

DO $$
DECLARE
    t TEXT;
BEGIN
    FOREACH t IN ARRAY ARRAY[
        'HousingServices', 'CreditCardPurchases', 'FixedLiabilities',
        'Income', 'OtherExpenses', 'Savings', 'ShoppingLists', 'ShoppingListItems',
        'AssetPurchases', 'Vehicles', 'VehicleMonthlyPayments', 'VehicleMaintenanceLogs',
        'RecurringIncomes', 'LedgerTags', 'LedgerTransactions'
    ]
    LOOP
        EXECUTE format('ALTER TABLE %I ADD COLUMN IF NOT EXISTS "SyncId" UUID NOT NULL DEFAULT gen_random_uuid() UNIQUE', t);
        EXECUTE format('ALTER TABLE %I ADD COLUMN IF NOT EXISTS "UpdatedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW()', t);
        EXECUTE format('ALTER TABLE %I ADD COLUMN IF NOT EXISTS "IsDeleted" BOOLEAN NOT NULL DEFAULT FALSE', t);
        EXECUTE format('DROP TRIGGER IF EXISTS trg_set_updated_at ON %I', t);
        EXECUTE format('CREATE TRIGGER trg_set_updated_at BEFORE UPDATE ON %I FOR EACH ROW EXECUTE FUNCTION fn_set_updated_at()', t);
    END LOOP;
END $$;

-- LedgerTransactionTags: composite PK -> surrogate Id + UNIQUE constraint,
-- then the same three sync columns and trigger as every other table.
ALTER TABLE "LedgerTransactionTags" DROP CONSTRAINT IF EXISTS "LedgerTransactionTags_pkey";
ALTER TABLE "LedgerTransactionTags" ADD COLUMN IF NOT EXISTS "Id" SERIAL PRIMARY KEY;
ALTER TABLE "LedgerTransactionTags" ADD CONSTRAINT "LedgerTransactionTags_transaction_tag_unique" UNIQUE ("TransactionId", "TagId");
ALTER TABLE "LedgerTransactionTags" ADD COLUMN IF NOT EXISTS "SyncId" UUID NOT NULL DEFAULT gen_random_uuid() UNIQUE;
ALTER TABLE "LedgerTransactionTags" ADD COLUMN IF NOT EXISTS "UpdatedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW();
ALTER TABLE "LedgerTransactionTags" ADD COLUMN IF NOT EXISTS "IsDeleted" BOOLEAN NOT NULL DEFAULT FALSE;
DROP TRIGGER IF EXISTS trg_set_updated_at ON "LedgerTransactionTags";
CREATE TRIGGER trg_set_updated_at BEFORE UPDATE ON "LedgerTransactionTags" FOR EACH ROW EXECUTE FUNCTION fn_set_updated_at();

CREATE TABLE IF NOT EXISTS "SyncTokens" (
    "Id"         SERIAL       PRIMARY KEY,
    "UserId"     INT          NOT NULL REFERENCES "Users"("Id") ON DELETE CASCADE,
    "TokenHash"  VARCHAR(200) NOT NULL UNIQUE,
    "CreatedAt"  TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    "LastUsedAt" TIMESTAMPTZ  NULL
);

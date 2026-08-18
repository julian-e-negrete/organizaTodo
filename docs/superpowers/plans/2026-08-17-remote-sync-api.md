# Remote Sync API Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the server side of remote data sync — schema changes enabling soft-delete/last-write-wins across all 16 user-data tables, and a new authenticated `/api/sync` endpoint on `OrganizaTodo.Web` that a future desktop sync client (a separate follow-up plan) will call.

**Architecture:** A new numbered migration adds `SyncId`/`UpdatedAt`/`IsDeleted` plus a `BEFORE UPDATE` trigger to all 16 user-data tables (including converting `LedgerTransactionTags`' composite PK to a surrogate `Id`), and a `SyncTokens` table. Every existing get/list function gets an `IsDeleted = FALSE` filter; every existing delete function becomes a soft-delete. A generic, table-driven C# sync engine (a static registry of the 16 tables, not 32 hand-written PG functions — a deliberate, disclosed exception to the "all DB access through PG functions" convention, justified the same way as the desktop schema bootstrapper: this is generic infrastructure operating uniformly across tables, not per-table business logic) implements pull (rows changed since a watermark) and push (last-write-wins upsert, scoped strictly to the authenticated caller's `UserId`) against real Postgres via Dapper.

**Tech Stack:** .NET 8, C# nullable-enabled, Dapper + Npgsql (no EF Core), xUnit, PostgreSQL 16.

**Spec:** [docs/superpowers/specs/2026-08-17-remote-data-sync-design.md](../specs/2026-08-17-remote-data-sync-design.md)

## Global Constraints

- No EF Core anywhere in this plan — Dapper only.
- Every write in `/api/sync` is scoped to the authenticated caller's `UserId`; a payload row implying a different user must be rejected, never silently ignored or trusted.
- `ShoppingListItems` (owned via `ShoppingListId → ShoppingLists.UserId`) and `VehicleMonthlyPayments` (owned via `VehicleId → Vehicles.UserId`) have no direct `UserId` column — ownership checks for these two tables use a JOIN/EXISTS against their parent table, not a direct column.
- `LedgerTransactionTags`' PK is converted from composite `(TransactionId, TagId)` to a surrogate `Id`, with `(TransactionId, TagId)` enforced via a `UNIQUE` constraint instead — existing code that queries by both columns together is unaffected functionally.
- This plan is server-side only. No desktop-app changes — that's a separate follow-up plan once this API is proven working.
- Follow the project's superpowers TDD workflow: write the failing test before the implementation for every task below.

---

### Task 1: Schema migration — sync columns, trigger, and `SyncTokens`

**Files:**
- Create: `src/OrganizaTodo.Web/Database/migrate_015_sync_infrastructure.sql`
- Test: `tests/OrganizaTodo.Desktop.Tests/SyncSchemaMigrationIntegrationTests.cs`

**Interfaces:**
- Produces: on every one of the 16 tables listed below — columns `"SyncId" UUID NOT NULL DEFAULT gen_random_uuid() UNIQUE`, `"UpdatedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW()`, `"IsDeleted" BOOLEAN NOT NULL DEFAULT FALSE`, and a `BEFORE UPDATE` trigger calling `fn_set_updated_at()`. Also produces table `"SyncTokens"("Id" SERIAL PRIMARY KEY, "UserId" INT NOT NULL REFERENCES "Users"("Id") ON DELETE CASCADE, "TokenHash" VARCHAR(200) NOT NULL UNIQUE, "CreatedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW(), "LastUsedAt" TIMESTAMPTZ NULL)`.

This test project already exists from the desktop-app-runtime plan (`tests/OrganizaTodo.Desktop.Tests`) and already has the real-Postgres integration-test pattern (`PostgresTestEnvironment.ResolvePgBinDirectory()`, a `PostgresManager` fixture) — reuse it rather than building a new harness. Real Postgres 16 binaries are available via `pg_config --bindir` or `ORGANIZATODO_PG_BIN_DIR` on this machine.

- [ ] **Step 1: Write the failing integration test**

`tests/OrganizaTodo.Desktop.Tests/SyncSchemaMigrationIntegrationTests.cs`:

```csharp
using Dapper;
using Npgsql;
using OrganizaTodo.Desktop.Lifecycle;

namespace OrganizaTodo.Desktop.Tests;

public class SyncSchemaMigrationIntegrationTests : IAsyncLifetime
{
    private string _pgBinDirectory = "";
    private string _tempDir = "";
    private PostgresManager _manager = null!;
    private int _port;
    private DbCredentials _credentials = null!;

    public async Task InitializeAsync()
    {
        _pgBinDirectory = PostgresTestEnvironment.ResolvePgBinDirectory();
        _tempDir = Directory.CreateTempSubdirectory("organizatodo-syncschema-test-").FullName;
        var logsDir = Path.Combine(_tempDir, "logs");
        Directory.CreateDirectory(logsDir);
        _port = PortAllocator.FindAvailablePort();
        _credentials = new DbCredentials("organizatodo_app", "test-password-123");

        _manager = new PostgresManager(_pgBinDirectory, Path.Combine(_tempDir, "pgdata"), _port, _credentials, logsDir);
        await _manager.InitializeIfNeededAsync();
        await _manager.StartAsync();
        await _manager.WaitUntilReadyAsync(TimeSpan.FromSeconds(30));
    }

    public async Task DisposeAsync()
    {
        await _manager.StopAsync();
        Directory.Delete(_tempDir, recursive: true);
    }

    [Theory]
    [InlineData("HousingServices")]
    [InlineData("CreditCardPurchases")]
    [InlineData("FixedLiabilities")]
    [InlineData("Income")]
    [InlineData("OtherExpenses")]
    [InlineData("Savings")]
    [InlineData("ShoppingLists")]
    [InlineData("ShoppingListItems")]
    [InlineData("AssetPurchases")]
    [InlineData("Vehicles")]
    [InlineData("VehicleMonthlyPayments")]
    [InlineData("VehicleMaintenanceLogs")]
    [InlineData("RecurringIncomes")]
    [InlineData("LedgerTags")]
    [InlineData("LedgerTransactions")]
    [InlineData("LedgerTransactionTags")]
    public async Task Migration_AddsSyncColumns_ToEveryTable(string tableName)
    {
        await using var conn = await OpenBootstrappedConnectionAsync();

        var columns = (await conn.QueryAsync<string>(
            "SELECT column_name FROM information_schema.columns WHERE table_name = @tableName",
            new { tableName })).ToHashSet();

        Assert.Contains("SyncId", columns);
        Assert.Contains("UpdatedAt", columns);
        Assert.Contains("IsDeleted", columns);
    }

    [Fact]
    public async Task Migration_TriggerSetsUpdatedAt_OnUpdate()
    {
        await using var conn = await OpenBootstrappedConnectionAsync();

        var userId = await conn.ExecuteScalarAsync<int>(
            "INSERT INTO \"Users\" (\"Email\", \"PasswordHash\", \"Role\") VALUES ('t@t.com', 'x', 'User') RETURNING \"Id\"");
        var id = await conn.ExecuteScalarAsync<int>(
            "INSERT INTO \"HousingServices\" (\"UserId\", \"Name\", \"Amount\") VALUES (@userId, 'Test', 100) RETURNING \"Id\"",
            new { userId });
        var firstUpdatedAt = await conn.ExecuteScalarAsync<DateTime>(
            "SELECT \"UpdatedAt\" FROM \"HousingServices\" WHERE \"Id\" = @id", new { id });

        await Task.Delay(50);
        await conn.ExecuteAsync("UPDATE \"HousingServices\" SET \"Amount\" = 200 WHERE \"Id\" = @id", new { id });

        var secondUpdatedAt = await conn.ExecuteScalarAsync<DateTime>(
            "SELECT \"UpdatedAt\" FROM \"HousingServices\" WHERE \"Id\" = @id", new { id });

        Assert.True(secondUpdatedAt > firstUpdatedAt);
    }

    [Fact]
    public async Task Migration_TriggerPreservesExplicitUpdatedAt_WhenCallerSetsItThemselves()
    {
        // This is what SyncPushService (Task 6) depends on: pushing a record must
        // preserve the pushing device's own UpdatedAt, not have it silently
        // overwritten with the server's receive time (NOW()) by this trigger.
        await using var conn = await OpenBootstrappedConnectionAsync();

        var userId = await conn.ExecuteScalarAsync<int>(
            "INSERT INTO \"Users\" (\"Email\", \"PasswordHash\", \"Role\") VALUES ('trigger-test@t.com', 'x', 'User') RETURNING \"Id\"");
        var id = await conn.ExecuteScalarAsync<int>(
            "INSERT INTO \"HousingServices\" (\"UserId\", \"Name\", \"Amount\") VALUES (@userId, 'Test', 100) RETURNING \"Id\"",
            new { userId });

        var explicitTimestamp = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        await conn.ExecuteAsync(
            "UPDATE \"HousingServices\" SET \"Amount\" = 200, \"UpdatedAt\" = @explicitTimestamp WHERE \"Id\" = @id",
            new { id, explicitTimestamp });

        var storedUpdatedAt = await conn.ExecuteScalarAsync<DateTime>(
            "SELECT \"UpdatedAt\" FROM \"HousingServices\" WHERE \"Id\" = @id", new { id });

        Assert.Equal(explicitTimestamp, storedUpdatedAt, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task Migration_ConvertsLedgerTransactionTagsPrimaryKey_ToSurrogateId()
    {
        await using var conn = await OpenBootstrappedConnectionAsync();

        var columns = (await conn.QueryAsync<string>(
            "SELECT column_name FROM information_schema.columns WHERE table_name = 'LedgerTransactionTags'")).ToHashSet();
        Assert.Contains("Id", columns);

        var uniqueConstraintExists = await conn.ExecuteScalarAsync<bool>(
            """
            SELECT EXISTS (
                SELECT 1 FROM information_schema.table_constraints
                WHERE table_name = 'LedgerTransactionTags' AND constraint_type = 'UNIQUE'
            )
            """);
        Assert.True(uniqueConstraintExists);
    }

    [Fact]
    public async Task Migration_CreatesSyncTokensTable()
    {
        await using var conn = await OpenBootstrappedConnectionAsync();

        var exists = await conn.ExecuteScalarAsync<bool>(
            "SELECT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'SyncTokens')");
        Assert.True(exists);
    }

    private async Task<NpgsqlConnection> OpenBootstrappedConnectionAsync()
    {
        var adminConnectionString = PostgresConnectionStringBuilder.Build(_port, "postgres", _credentials.Username, _credentials.Password);
        await using var adminConnection = new NpgsqlConnection(adminConnectionString);
        await adminConnection.OpenAsync();

        var sqlDir = Path.Combine(AppContext.BaseDirectory, "Database");
        var bootstrapper = new SchemaBootstrapper(sqlDir);
        await bootstrapper.BootstrapAsync(adminConnection, "organizatodo", isShoppingDatabase: false);

        var connectionString = PostgresConnectionStringBuilder.Build(_port, "organizatodo", _credentials.Username, _credentials.Password);
        var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();
        return conn;
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `export ORGANIZATODO_PG_BIN_DIR=<your pg bin dir>; dotnet test tests/OrganizaTodo.Desktop.Tests --filter SyncSchemaMigrationIntegrationTests`
Expected: FAIL (the migration file doesn't exist yet, so the columns/table/trigger aren't there — `information_schema` queries return empty/false)

- [ ] **Step 3: Write the migration**

`src/OrganizaTodo.Web/Database/migrate_015_sync_infrastructure.sql`:

```sql
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
```

Note: `gen_random_uuid()` requires the `pgcrypto` extension (or Postgres 13+'s built-in `gen_random_uuid()` — Postgres 16, which this project targets, has it built into core, no extension needed).

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/OrganizaTodo.Desktop.Tests --filter SyncSchemaMigrationIntegrationTests`
Expected: PASS (16 theory cases + 4 facts = 20 tests)

- [ ] **Step 5: Commit**

```bash
git add src/OrganizaTodo.Web/Database/migrate_015_sync_infrastructure.sql tests/OrganizaTodo.Desktop.Tests/SyncSchemaMigrationIntegrationTests.cs
git commit -m "add sync infrastructure migration: SyncId/UpdatedAt/IsDeleted, trigger, SyncTokens"
```

---

### Task 2: Soft-delete conversion — redefine existing get/list/delete functions

**Files:**
- Create: `src/OrganizaTodo.Web/Database/migrate_016_soft_delete_filters.sql`
- Test: `tests/OrganizaTodo.Desktop.Tests/SoftDeleteFilterIntegrationTests.cs`

**Interfaces:** No new C# interfaces — this task only touches SQL function bodies. The function signatures (name, parameters, return shape) are unchanged; only bodies change (add `AND "IsDeleted" = FALSE` to get/list functions; convert `DELETE FROM` to `UPDATE ... SET "IsDeleted" = TRUE` in delete functions).

This task is discovery-driven, not pre-written, because several of these functions are redefined multiple times across the existing migration history (e.g. `sp_vehicles_get_by_id` appears in `migrate_005`, `migrate_007`, and `migrate_008` — each redefining it) — the version actually active after all migrations run is the one that matters, and hand-tracing that across files risks transcription errors in production SQL. Instead, read each function's live body directly from a freshly-bootstrapped database.

- [ ] **Step 1: Bootstrap a scratch database and dump the current, actually-active body of every function in scope**

The functions in scope (get/list functions get the `IsDeleted` filter; delete functions become soft-deletes):

```
sp_housing_services_get_by_id          sp_housing_services_get_by_user_id      sp_housing_services_delete
sp_credit_card_get_by_id               sp_credit_card_get_by_user_id           sp_credit_card_delete
sp_fixed_liabilities_get_by_id         sp_fixed_liabilities_get_by_user_id     sp_fixed_liabilities_delete
sp_income_get_by_id                    sp_income_get_by_user_id_and_period     sp_income_delete
sp_other_expenses_get_by_id            sp_other_expenses_get_by_user_id_and_period   sp_other_expenses_delete
sp_savings_get_by_id                   sp_savings_get_by_user_id               sp_savings_delete
sp_shopping_get_user_lists             sp_shopping_get_list_items              sp_shopping_delete_item
sp_asset_purchases_get_by_id           sp_asset_purchases_get_by_user_id       sp_asset_purchases_delete
sp_vehicles_get_by_id                  sp_vehicles_get_by_user_id              sp_vehicles_delete
sp_vehicle_maintenance_logs_get
sp_recurring_income_get_by_id          sp_recurring_income_get_by_user_id      sp_recurring_income_delete
sp_ledger_tags_get_by_user_id
sp_ledger_transaction_get_children     sp_ledger_transaction_get_detail
sp_ledger_history_get
```

Bootstrap a scratch Postgres instance and database exactly as `SyncSchemaMigrationIntegrationTests` does (real Postgres, `SchemaBootstrapper` applying `init.sql` + all pending `migrate_*.sql` through `migrate_015`), then for each function above run:

```bash
psql -h 127.0.0.1 -p <port> -U organizatodo_app -d organizatodo -c "\sf <function_name>"
```

(`\sf` prints the function's current, live `CREATE OR REPLACE FUNCTION` body — this is ground truth, immune to which historical file last touched it.) Save each output.

- [ ] **Step 2: Write the failing integration tests**

`tests/OrganizaTodo.Desktop.Tests/SoftDeleteFilterIntegrationTests.cs` — one representative test per table family is enough to prove the pattern holds (not all 16, to keep this file focused); cover at least one direct table, one get-by-id, one get-by-user-id/list, and one delete function per the two shapes below:

```csharp
using Dapper;
using Npgsql;
using OrganizaTodo.Desktop.Lifecycle;

namespace OrganizaTodo.Desktop.Tests;

public class SoftDeleteFilterIntegrationTests : IAsyncLifetime
{
    private string _pgBinDirectory = "";
    private string _tempDir = "";
    private PostgresManager _manager = null!;
    private int _port;
    private DbCredentials _credentials = null!;
    private int _userId;

    public async Task InitializeAsync()
    {
        _pgBinDirectory = PostgresTestEnvironment.ResolvePgBinDirectory();
        _tempDir = Directory.CreateTempSubdirectory("organizatodo-softdelete-test-").FullName;
        var logsDir = Path.Combine(_tempDir, "logs");
        Directory.CreateDirectory(logsDir);
        _port = PortAllocator.FindAvailablePort();
        _credentials = new DbCredentials("organizatodo_app", "test-password-123");

        _manager = new PostgresManager(_pgBinDirectory, Path.Combine(_tempDir, "pgdata"), _port, _credentials, logsDir);
        await _manager.InitializeIfNeededAsync();
        await _manager.StartAsync();
        await _manager.WaitUntilReadyAsync(TimeSpan.FromSeconds(30));

        var adminConnectionString = PostgresConnectionStringBuilder.Build(_port, "postgres", _credentials.Username, _credentials.Password);
        await using var adminConnection = new NpgsqlConnection(adminConnectionString);
        await adminConnection.OpenAsync();
        var bootstrapper = new SchemaBootstrapper(Path.Combine(AppContext.BaseDirectory, "Database"));
        await bootstrapper.BootstrapAsync(adminConnection, "organizatodo", isShoppingDatabase: false);

        await using var conn = await OpenConnectionAsync();
        _userId = await conn.ExecuteScalarAsync<int>(
            "INSERT INTO \"Users\" (\"Email\", \"PasswordHash\", \"Role\") VALUES ('softdelete@t.com', 'x', 'User') RETURNING \"Id\"");
    }

    public async Task DisposeAsync()
    {
        await _manager.StopAsync();
        Directory.Delete(_tempDir, recursive: true);
    }

    private async Task<NpgsqlConnection> OpenConnectionAsync()
    {
        var connectionString = PostgresConnectionStringBuilder.Build(_port, "organizatodo", _credentials.Username, _credentials.Password);
        var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();
        return conn;
    }

    [Fact]
    public async Task HousingServicesDelete_SoftDeletes_RowStaysInTableButHiddenFromGetByUserId()
    {
        await using var conn = await OpenConnectionAsync();

        var id = await conn.ExecuteScalarAsync<int>(
            "SELECT sp_housing_services_create(@userId, 'Electricity', 100, 10, 'MONTHLY')",
            new { userId = _userId });

        await conn.ExecuteAsync("SELECT sp_housing_services_delete(@id, @userId)", new { id, userId = _userId });

        var stillInTable = await conn.ExecuteScalarAsync<bool>(
            "SELECT EXISTS (SELECT 1 FROM \"HousingServices\" WHERE \"Id\" = @id)", new { id });
        Assert.True(stillInTable, "delete must be soft (row stays), not a hard DELETE");

        var isDeletedFlagSet = await conn.ExecuteScalarAsync<bool>(
            "SELECT \"IsDeleted\" FROM \"HousingServices\" WHERE \"Id\" = @id", new { id });
        Assert.True(isDeletedFlagSet);

        var visibleRows = await conn.QueryAsync(
            "SELECT * FROM sp_housing_services_get_by_user_id(@userId)", new { userId = _userId });
        Assert.DoesNotContain(visibleRows, row => (int)row.Id == id);
    }

    [Fact]
    public async Task VehiclesDelete_SoftDeletes_RowStaysInTableButHiddenFromGetByUserId()
    {
        await using var conn = await OpenConnectionAsync();

        var id = await conn.ExecuteScalarAsync<int>(
            "SELECT sp_vehicles_create(@userId, 'ABC123', 'Toyota', 'Corolla', 2020, 350, 10, NULL, NULL, NULL)",
            new { userId = _userId });

        await conn.ExecuteAsync("SELECT sp_vehicles_delete(@id, @userId)", new { id, userId = _userId });

        var isDeletedFlagSet = await conn.ExecuteScalarAsync<bool>(
            "SELECT \"IsDeleted\" FROM \"Vehicles\" WHERE \"Id\" = @id", new { id });
        Assert.True(isDeletedFlagSet);

        var visibleRows = await conn.QueryAsync(
            "SELECT * FROM sp_vehicles_get_by_user_id(@userId)", new { userId = _userId });
        Assert.DoesNotContain(visibleRows, row => (int)row.Id == id);
    }
}
```

If `sp_housing_services_create`'s or `sp_vehicles_create`'s actual parameter list differs from what's guessed above (verify against the `\sf` dump from Step 1 — this task's own discovery step), adjust the test calls to match the real signatures. Signatures are not changing in this task, only bodies.

- [ ] **Step 3: Run tests to verify they fail**

Run: `dotnet test tests/OrganizaTodo.Desktop.Tests --filter SoftDeleteFilterIntegrationTests`
Expected: FAIL (`sp_housing_services_delete`/`sp_vehicles_delete` still hard-delete, so `stillInTable` is false and the test fails on that assertion)

- [ ] **Step 4: Write the migration**

`src/OrganizaTodo.Web/Database/migrate_016_soft_delete_filters.sql` — for every function in the Step 1 list: `DROP FUNCTION IF EXISTS <name>(<param types from the \sf dump>);` followed by `CREATE OR REPLACE FUNCTION <name>(...) ... $$` using the Step 1 dump as the base body, with exactly one mechanical change applied:
- **Get/list functions**: add `AND "IsDeleted" = FALSE` to the `WHERE` clause (or `AND NOT t."IsDeleted"` if the function uses a table alias — match the existing alias convention already in that function's body).
- **Delete functions**: replace the body's `DELETE FROM "<Table>" WHERE "Id" = <id_param> AND "UserId" = <user_id_param>;` with `UPDATE "<Table>" SET "IsDeleted" = TRUE WHERE "Id" = <id_param> AND "UserId" = <user_id_param>;` — keep the same ownership check (`UserId` match) the original delete already had, don't loosen it.

For the two tables without a direct `UserId` (`ShoppingListItems` via `ShoppingListId`, and any delete/list function touching them — `sp_shopping_delete_item`, `sp_shopping_get_list_items`): the ownership check already joins through `ShoppingLists` in the existing body (verify via the `\sf` dump) — preserve that join, only add the `IsDeleted` filter/soft-delete conversion on top of it.

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test tests/OrganizaTodo.Desktop.Tests --filter SoftDeleteFilterIntegrationTests`
Expected: PASS (2 tests)

- [ ] **Step 6: Verify completeness across all remaining functions**

For every function in the Step 1 list not directly covered by the two tests above, manually diff the migration's new body against the Step 1 `\sf` dump to confirm exactly one mechanical change was applied and nothing else drifted. Note any function whose body doesn't fit the two mechanical patterns above (e.g., a function with more complex logic) in the task report as a concern for the reviewer to double check.

- [ ] **Step 7: Commit**

```bash
git add src/OrganizaTodo.Web/Database/migrate_016_soft_delete_filters.sql tests/OrganizaTodo.Desktop.Tests/SoftDeleteFilterIntegrationTests.cs
git commit -m "redefine get/list/delete functions to respect soft-delete (IsDeleted)"
```

---

### Task 3: `SyncTokenService` — token generation, hashing, verification

**Files:**
- Create: `src/OrganizaTodo.Web/Services/ISyncTokenService.cs`
- Create: `src/OrganizaTodo.Web/Services/SyncTokenService.cs`
- Modify: `src/OrganizaTodo.Web/Program.cs` (register `ISyncTokenService`)
- Test: `tests/OrganizaTodo.Desktop.Tests/SyncTokenServiceTests.cs`

**Interfaces:**
- Consumes: nothing from earlier tasks (pure hashing logic, no DB).
- Produces:
  ```csharp
  namespace OrganizaTodo.Web.Services;

  public interface ISyncTokenService
  {
      (string PlaintextToken, string TokenHash) GenerateToken();
      string HashToken(string plaintextToken);
  }
  ```

- [ ] **Step 1: Write the failing test**

`tests/OrganizaTodo.Desktop.Tests/SyncTokenServiceTests.cs`:

```csharp
using OrganizaTodo.Web.Services;

namespace OrganizaTodo.Desktop.Tests;

public class SyncTokenServiceTests
{
    [Fact]
    public void GenerateToken_ProducesUniquePlaintextTokensAndMatchingHashes()
    {
        var service = new SyncTokenService();

        var (plaintext1, hash1) = service.GenerateToken();
        var (plaintext2, hash2) = service.GenerateToken();

        Assert.NotEqual(plaintext1, plaintext2);
        Assert.NotEqual(hash1, hash2);
        Assert.Equal(hash1, service.HashToken(plaintext1));
    }

    [Fact]
    public void HashToken_IsDeterministic_SameInputSameOutput()
    {
        var service = new SyncTokenService();

        var hashA = service.HashToken("some-fixed-token-value");
        var hashB = service.HashToken("some-fixed-token-value");

        Assert.Equal(hashA, hashB);
    }

    [Fact]
    public void HashToken_DifferentInputs_ProduceDifferentHashes()
    {
        var service = new SyncTokenService();

        Assert.NotEqual(service.HashToken("token-a"), service.HashToken("token-b"));
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/OrganizaTodo.Desktop.Tests --filter SyncTokenServiceTests`
Expected: FAIL (compile error — `SyncTokenService` doesn't exist yet)

- [ ] **Step 3: Implement it**

`src/OrganizaTodo.Web/Services/ISyncTokenService.cs`:

```csharp
namespace OrganizaTodo.Web.Services;

public interface ISyncTokenService
{
    (string PlaintextToken, string TokenHash) GenerateToken();
    string HashToken(string plaintextToken);
}
```

`src/OrganizaTodo.Web/Services/SyncTokenService.cs`:

```csharp
using System.Security.Cryptography;
using System.Text;

namespace OrganizaTodo.Web.Services;

public sealed class SyncTokenService : ISyncTokenService
{
    public (string PlaintextToken, string TokenHash) GenerateToken()
    {
        var plaintext = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        return (plaintext, HashToken(plaintext));
    }

    public string HashToken(string plaintextToken)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(plaintextToken));
        return Convert.ToHexString(bytes);
    }
}
```

Deterministic SHA-256 (not a salted/slow hash like BCrypt) is intentional here: unlike a user's login password, a `SyncToken` is a high-entropy 256-bit random value the service itself generates — brute-forcing it is infeasible regardless of hash speed, and the token must be looked up by exact hash match on every sync request, which a slow hash would make unnecessarily expensive per-request.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/OrganizaTodo.Desktop.Tests --filter SyncTokenServiceTests`
Expected: PASS (3 tests)

- [ ] **Step 5: Register the service and commit**

Add to `src/OrganizaTodo.Web/Program.cs`'s `BuildApp`, alongside the other `AddScoped` service registrations:
```csharp
builder.Services.AddScoped<ISyncTokenService, SyncTokenService>();
```

```bash
git add src/OrganizaTodo.Web/Services/ISyncTokenService.cs src/OrganizaTodo.Web/Services/SyncTokenService.cs src/OrganizaTodo.Web/Program.cs tests/OrganizaTodo.Desktop.Tests/SyncTokenServiceTests.cs
git commit -m "add SyncTokenService for generating and hashing sync tokens"
```

---

### Task 4: `POST /api/sync/connect` — issue a sync token for an existing account

**Files:**
- Create: `src/OrganizaTodo.Web/Controllers/SyncController.cs`
- Create: `src/OrganizaTodo.Web/Models/ConnectRequest.cs`
- Create: `src/OrganizaTodo.Web/Models/ConnectResponse.cs`
- Create: `src/OrganizaTodo.Web/Repositories/ISyncTokenRepository.cs`
- Create: `src/OrganizaTodo.Web/Repositories/SyncTokenRepository.cs`
- Modify: `src/OrganizaTodo.Web/Program.cs` (register `ISyncTokenRepository`)
- Test: `tests/OrganizaTodo.Desktop.Tests/SyncConnectEndpointIntegrationTests.cs`

**Interfaces:**
- Consumes: `ISyncTokenService` (Task 3), the existing `IAuthService` (verify its actual method signature in `src/OrganizaTodo.Web/Services/AuthService.cs` before writing this task's code — this plan assumes a method shaped like `Task<User?> VerifyCredentialsAsync(string email, string password)` returning the user on success and `null` on failure; adjust the call site if the real signature differs, the intent — verify password without setting a cookie — is what matters), `OrganizaTodo.Web.Program.BuildApp` (already exists).
- Produces:
  ```csharp
  namespace OrganizaTodo.Web.Repositories;

  public interface ISyncTokenRepository
  {
      Task StoreTokenAsync(int userId, string tokenHash);
  }
  ```
  `POST /api/sync/connect` accepting `{ "email": string, "password": string }`, returning `200 { "token": string, "userId": int }` on success or `401` on bad credentials.

- [ ] **Step 1: Check `IAuthService`'s real signature**

Read `src/OrganizaTodo.Web/Services/IAuthService.cs` and `AuthService.cs` before writing the controller — confirm the exact method name/signature for "verify these credentials, return the user or null/failure" (as opposed to the cookie-setting login flow used by `/Account/Login`). Use whatever that real method is; do not add a second password-verification code path if one already exists in a reusable form.

- [ ] **Step 2: Write the failing integration test**

`tests/OrganizaTodo.Desktop.Tests/SyncConnectEndpointIntegrationTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using Dapper;
using Npgsql;
using OrganizaTodo.Desktop.Lifecycle;
using OrganizaTodo.Web.Models;

namespace OrganizaTodo.Desktop.Tests;

public class SyncConnectEndpointIntegrationTests : IAsyncLifetime
{
    private string _pgBinDirectory = "";
    private string _tempDir = "";
    private PostgresManager _manager = null!;
    private int _pgPort;
    private DbCredentials _credentials = null!;
    private OrganizaTodo.Desktop.Lifecycle.KestrelWebAppRunner _runner = null!;
    private string _baseUrl = "";

    public async Task InitializeAsync()
    {
        _pgBinDirectory = PostgresTestEnvironment.ResolvePgBinDirectory();
        _tempDir = Directory.CreateTempSubdirectory("organizatodo-syncconnect-test-").FullName;
        var logsDir = Path.Combine(_tempDir, "logs");
        Directory.CreateDirectory(logsDir);
        _pgPort = PortAllocator.FindAvailablePort();
        _credentials = new DbCredentials("organizatodo_app", "test-password-123");

        _manager = new PostgresManager(_pgBinDirectory, Path.Combine(_tempDir, "pgdata"), _pgPort, _credentials, logsDir);
        await _manager.InitializeIfNeededAsync();
        await _manager.StartAsync();
        await _manager.WaitUntilReadyAsync(TimeSpan.FromSeconds(30));

        var adminConnectionString = PostgresConnectionStringBuilder.Build(_pgPort, "postgres", _credentials.Username, _credentials.Password);
        await using var adminConnection = new NpgsqlConnection(adminConnectionString);
        await adminConnection.OpenAsync();
        var bootstrapper = new SchemaBootstrapper(Path.Combine(AppContext.BaseDirectory, "Database"));
        await bootstrapper.BootstrapAsync(adminConnection, "organizatodo", isShoppingDatabase: false);
        await bootstrapper.BootstrapAsync(adminConnection, "organizatodo_shopping", isShoppingDatabase: true);

        // Seed a real user via the app's own signup path (sp_users_create + a real BCrypt hash)
        // rather than hand-rolling a PasswordHash here.
        await using (var conn = new NpgsqlConnection(PostgresConnectionStringBuilder.Build(_pgPort, "organizatodo", _credentials.Username, _credentials.Password)))
        {
            await conn.OpenAsync();
            var passwordHash = BCrypt.Net.BCrypt.HashPassword("correct-password");
            await conn.ExecuteAsync(
                "SELECT sp_users_create(@email, @passwordHash, 'User')",
                new { email = "sync-test@t.com", passwordHash });
        }

        _runner = new OrganizaTodo.Desktop.Lifecycle.KestrelWebAppRunner();
        _baseUrl = await _runner.StartAsync(
            PostgresConnectionStringBuilder.Build(_pgPort, "organizatodo", _credentials.Username, _credentials.Password),
            PostgresConnectionStringBuilder.Build(_pgPort, "organizatodo_shopping", _credentials.Username, _credentials.Password));
    }

    public async Task DisposeAsync()
    {
        await _runner.StopAsync();
        await _manager.StopAsync();
        Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public async Task Connect_WithCorrectCredentials_ReturnsToken()
    {
        using var client = new HttpClient();

        var response = await client.PostAsJsonAsync($"{_baseUrl}/api/sync/connect",
            new ConnectRequest("sync-test@t.com", "correct-password"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ConnectResponse>();
        Assert.NotNull(body);
        Assert.False(string.IsNullOrWhiteSpace(body!.Token));
        Assert.True(body.UserId > 0);
    }

    [Fact]
    public async Task Connect_WithWrongPassword_ReturnsUnauthorized()
    {
        using var client = new HttpClient();

        var response = await client.PostAsJsonAsync($"{_baseUrl}/api/sync/connect",
            new ConnectRequest("sync-test@t.com", "wrong-password"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
```

`OrganizaTodo.Desktop.Tests` already references `OrganizaTodo.Web` (from the desktop-app-runtime plan) and `Npgsql`; add a `BCrypt.Net-Next` package reference to the test project (`dotnet add tests/OrganizaTodo.Desktop.Tests/OrganizaTodo.Desktop.Tests.csproj package BCrypt.Net-Next`) since `OrganizaTodo.Web` already depends on it and this test needs to hash a real password the same way the app does.

- [ ] **Step 3: Run test to verify it fails**

Run: `dotnet test tests/OrganizaTodo.Desktop.Tests --filter SyncConnectEndpointIntegrationTests`
Expected: FAIL (compile error — `SyncController`, `ConnectRequest`, `ConnectResponse` don't exist yet)

- [ ] **Step 4: Implement the models, repository, and controller**

`src/OrganizaTodo.Web/Models/ConnectRequest.cs`:
```csharp
namespace OrganizaTodo.Web.Models;

public sealed record ConnectRequest(string Email, string Password);
```

`src/OrganizaTodo.Web/Models/ConnectResponse.cs`:
```csharp
namespace OrganizaTodo.Web.Models;

public sealed record ConnectResponse(string Token, int UserId);
```

`src/OrganizaTodo.Web/Repositories/ISyncTokenRepository.cs`:
```csharp
namespace OrganizaTodo.Web.Repositories;

public interface ISyncTokenRepository
{
    Task StoreTokenAsync(int userId, string tokenHash);
}
```

`src/OrganizaTodo.Web/Repositories/SyncTokenRepository.cs`:
```csharp
using Dapper;
using OrganizaTodo.Web.Data;

namespace OrganizaTodo.Web.Repositories;

public sealed class SyncTokenRepository(IDbConnectionFactory connectionFactory) : ISyncTokenRepository
{
    public async Task StoreTokenAsync(int userId, string tokenHash)
    {
        using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync(
            "INSERT INTO \"SyncTokens\" (\"UserId\", \"TokenHash\") VALUES (@userId, @tokenHash)",
            new { userId, tokenHash });
    }
}
```

Check `src/OrganizaTodo.Web/Data/IDbConnectionFactory.cs` for the exact factory method name (this plan assumes `CreateConnection()` returning an open `IDbConnection`, matching the existing repositories' pattern) — adjust if it differs.

`src/OrganizaTodo.Web/Controllers/SyncController.cs`:
```csharp
using Microsoft.AspNetCore.Mvc;
using OrganizaTodo.Web.Models;
using OrganizaTodo.Web.Repositories;
using OrganizaTodo.Web.Services;

namespace OrganizaTodo.Web.Controllers;

[ApiController]
[Route("api/sync")]
public sealed class SyncController(
    IAuthService authService,
    ISyncTokenService syncTokenService,
    ISyncTokenRepository syncTokenRepository) : ControllerBase
{
    [HttpPost("connect")]
    public async Task<IActionResult> Connect([FromBody] ConnectRequest request)
    {
        var user = await authService.VerifyCredentialsAsync(request.Email, request.Password);
        if (user is null)
        {
            return Unauthorized();
        }

        var (plaintextToken, tokenHash) = syncTokenService.GenerateToken();
        await syncTokenRepository.StoreTokenAsync(user.Id, tokenHash);

        return Ok(new ConnectResponse(plaintextToken, user.Id));
    }
}
```

The exact call `authService.VerifyCredentialsAsync(request.Email, request.Password)` and the shape of what it returns (does it have an `.Id` property? Is the method name actually this, or something else like `AuthenticateAsync`/`LoginAsync`?) must be confirmed against the real `IAuthService` from Step 1 and adjusted here to match — this is the one place in this task where the plan's exact code depends on reading existing code first.

Register `ISyncTokenRepository` in `Program.cs`'s `BuildApp`:
```csharp
builder.Services.AddScoped<ISyncTokenRepository, SyncTokenRepository>();
```

`[ApiController]` routing requires `AddControllers()`/`AddControllersWithViews()` (already present) to discover it — no additional registration needed for the controller itself, `.AddApplicationPart` from the desktop-app-runtime plan already covers it.

- [ ] **Step 5: Run test to verify it passes**

Run: `dotnet test tests/OrganizaTodo.Desktop.Tests --filter SyncConnectEndpointIntegrationTests`
Expected: PASS (2 tests)

- [ ] **Step 6: Commit**

```bash
git add src/OrganizaTodo.Web/Controllers/SyncController.cs src/OrganizaTodo.Web/Models/ConnectRequest.cs src/OrganizaTodo.Web/Models/ConnectResponse.cs src/OrganizaTodo.Web/Repositories/ISyncTokenRepository.cs src/OrganizaTodo.Web/Repositories/SyncTokenRepository.cs src/OrganizaTodo.Web/Program.cs tests/OrganizaTodo.Desktop.Tests/SyncConnectEndpointIntegrationTests.cs tests/OrganizaTodo.Desktop.Tests/OrganizaTodo.Desktop.Tests.csproj
git commit -m "add POST /api/sync/connect to issue sync tokens for existing accounts"
```

---

### Task 5: Sync table registry and generic pull query

**Files:**
- Create: `src/OrganizaTodo.Web/Sync/SyncTableDescriptor.cs`
- Create: `src/OrganizaTodo.Web/Sync/SyncTableRegistry.cs`
- Create: `src/OrganizaTodo.Web/Sync/SyncPullService.cs`
- Test: `tests/OrganizaTodo.Desktop.Tests/SyncTableRegistryTests.cs`
- Test: `tests/OrganizaTodo.Desktop.Tests/SyncPullServiceIntegrationTests.cs`

**Interfaces:**
- Produces:
  ```csharp
  namespace OrganizaTodo.Web.Sync;

  public sealed record SyncTableDescriptor(
      string TableName,
      string JsonKey,
      string OwnershipWhereClause,  // SQL fragment, e.g. "\"UserId\" = @userId" or a JOIN-based EXISTS clause
      IReadOnlyList<string> Columns // every column except "Id" (SyncId is the sync identity; local Id is per-database)
  );

  public static class SyncTableRegistry
  {
      public static IReadOnlyList<SyncTableDescriptor> Tables { get; }
  }

  public interface ISyncPullService
  {
      Task<Dictionary<string, IReadOnlyList<dynamic>>> PullChangesAsync(int userId, DateTimeOffset since, CancellationToken ct = default);
  }
  ```

- [ ] **Step 1: Write the failing registry test**

`tests/OrganizaTodo.Desktop.Tests/SyncTableRegistryTests.cs`:

```csharp
using OrganizaTodo.Web.Sync;

namespace OrganizaTodo.Desktop.Tests;

public class SyncTableRegistryTests
{
    [Fact]
    public void Tables_ContainsAllSixteenSyncedTables()
    {
        var tableNames = SyncTableRegistry.Tables.Select(t => t.TableName).ToHashSet();

        Assert.Equal(16, SyncTableRegistry.Tables.Count);
        Assert.Contains("HousingServices", tableNames);
        Assert.Contains("CreditCardPurchases", tableNames);
        Assert.Contains("FixedLiabilities", tableNames);
        Assert.Contains("Income", tableNames);
        Assert.Contains("OtherExpenses", tableNames);
        Assert.Contains("Savings", tableNames);
        Assert.Contains("ShoppingLists", tableNames);
        Assert.Contains("ShoppingListItems", tableNames);
        Assert.Contains("AssetPurchases", tableNames);
        Assert.Contains("Vehicles", tableNames);
        Assert.Contains("VehicleMonthlyPayments", tableNames);
        Assert.Contains("VehicleMaintenanceLogs", tableNames);
        Assert.Contains("RecurringIncomes", tableNames);
        Assert.Contains("LedgerTags", tableNames);
        Assert.Contains("LedgerTransactions", tableNames);
        Assert.Contains("LedgerTransactionTags", tableNames);
    }

    [Fact]
    public void ShoppingListItems_UsesJoinBasedOwnership_NotDirectUserId()
    {
        var descriptor = SyncTableRegistry.Tables.Single(t => t.TableName == "ShoppingListItems");

        Assert.False(descriptor.Columns.Contains("UserId"));
        Assert.Contains("ShoppingLists", descriptor.OwnershipWhereClause);
    }

    [Fact]
    public void VehicleMonthlyPayments_UsesJoinBasedOwnership_NotDirectUserId()
    {
        var descriptor = SyncTableRegistry.Tables.Single(t => t.TableName == "VehicleMonthlyPayments");

        Assert.False(descriptor.Columns.Contains("UserId"));
        Assert.Contains("Vehicles", descriptor.OwnershipWhereClause);
    }

    [Fact]
    public void EveryDescriptor_ColumnListExcludesId()
    {
        Assert.All(SyncTableRegistry.Tables, t => Assert.DoesNotContain("Id", t.Columns));
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/OrganizaTodo.Desktop.Tests --filter SyncTableRegistryTests`
Expected: FAIL (compile error)

- [ ] **Step 3: Implement the registry**

`src/OrganizaTodo.Web/Sync/SyncTableDescriptor.cs`:
```csharp
namespace OrganizaTodo.Web.Sync;

public sealed record SyncTableDescriptor(
    string TableName,
    string JsonKey,
    string OwnershipWhereClause,
    IReadOnlyList<string> Columns);
```

`src/OrganizaTodo.Web/Sync/SyncTableRegistry.cs`:
```csharp
namespace OrganizaTodo.Web.Sync;

public static class SyncTableRegistry
{
    public static IReadOnlyList<SyncTableDescriptor> Tables { get; } = new List<SyncTableDescriptor>
    {
        new("HousingServices", "housingServices", "\"UserId\" = @userId",
            new[] { "UserId", "Name", "Amount", "DueDay", "Periodicity", "IsPaid", "PaidDate", "IsActive", "CreatedAt" }),

        new("CreditCardPurchases", "creditCardPurchases", "\"UserId\" = @userId",
            new[] { "UserId", "Description", "TotalAmount", "Installments", "CurrentInstallment", "InterestRate", "PurchaseDate", "IsActive" }),

        new("FixedLiabilities", "fixedLiabilities", "\"UserId\" = @userId",
            new[] { "UserId", "Name", "MonthlyAmount", "DueDay", "IsActive", "IsPaid", "PaidDate", "CreatedAt" }),

        new("Income", "income", "\"UserId\" = @userId",
            new[] { "UserId", "Description", "Amount", "IncomeDate", "Category", "Month", "Year" }),

        new("OtherExpenses", "otherExpenses", "\"UserId\" = @userId",
            new[] { "UserId", "Description", "Amount", "ExpenseDate", "Month", "Year" }),

        new("Savings", "savings", "\"UserId\" = @userId",
            new[] { "UserId", "Amount", "Month", "Year", "Notes", "IsInitialBalance", "CreatedAt" }),

        new("ShoppingLists", "shoppingLists", "\"UserId\" = @userId",
            new[] { "UserId", "Name", "Month", "Year", "CreatedAt" }),

        new("ShoppingListItems", "shoppingListItems",
            "EXISTS (SELECT 1 FROM \"ShoppingLists\" sl WHERE sl.\"Id\" = \"ShoppingListItems\".\"ShoppingListId\" AND sl.\"UserId\" = @userId)",
            new[] { "ShoppingListId", "ProductName", "Quantity", "EstimatedPrice", "Supermarket", "Priority" }),

        new("AssetPurchases", "assetPurchases", "\"UserId\" = @userId",
            new[] { "UserId", "ExpenseId", "SavingId", "AssetType", "Quantity", "UnitPriceArs", "ExchangeType", "TotalArs", "Notes", "PurchaseDate", "CreatedAt" }),

        new("Vehicles", "vehicles", "\"UserId\" = @userId",
            new[] { "UserId", "Plate", "Make", "Model", "Year", "WeeklyKm", "FuelEfficiencyKmL", "FuelPricePerLiter", "InsuranceMonthly", "PatenteAnnual", "IsActive", "CreatedAt" }),

        new("VehicleMonthlyPayments", "vehicleMonthlyPayments",
            "EXISTS (SELECT 1 FROM \"Vehicles\" v WHERE v.\"Id\" = \"VehicleMonthlyPayments\".\"VehicleId\" AND v.\"UserId\" = @userId)",
            new[] { "VehicleId", "Month", "Year", "PaymentType", "PaidAt" }),

        new("VehicleMaintenanceLogs", "vehicleMaintenanceLogs", "\"UserId\" = @userId",
            new[] { "VehicleId", "UserId", "ServiceDate", "KmAtService", "Cost", "Notes", "CreatedAt" }),

        new("RecurringIncomes", "recurringIncomes", "\"UserId\" = @userId",
            new[] { "UserId", "Description", "Amount", "IsActive", "CreatedAt" }),

        new("LedgerTags", "ledgerTags", "\"UserId\" = @userId",
            new[] { "UserId", "Name", "CreatedAt" }),

        new("LedgerTransactions", "ledgerTransactions", "\"UserId\" = @userId",
            new[] { "UserId", "Amount", "Type", "Description", "TransactionDate", "BalanceAfter", "GeneratesInterest", "InterestRate", "ParentTransactionId", "CreatedAt" }),

        new("LedgerTransactionTags", "ledgerTransactionTags",
            "EXISTS (SELECT 1 FROM \"LedgerTransactions\" lt WHERE lt.\"Id\" = \"LedgerTransactionTags\".\"TransactionId\" AND lt.\"UserId\" = @userId)",
            new[] { "TransactionId", "TagId" }),
    };
}
```

- [ ] **Step 4: Run registry test to verify it passes**

Run: `dotnet test tests/OrganizaTodo.Desktop.Tests --filter SyncTableRegistryTests`
Expected: PASS (4 tests)

- [ ] **Step 5: Write the failing pull-service integration test**

`tests/OrganizaTodo.Desktop.Tests/SyncPullServiceIntegrationTests.cs`:

```csharp
using Dapper;
using Npgsql;
using OrganizaTodo.Desktop.Lifecycle;
using OrganizaTodo.Web.Sync;

namespace OrganizaTodo.Desktop.Tests;

public class SyncPullServiceIntegrationTests : IAsyncLifetime
{
    private string _pgBinDirectory = "";
    private string _tempDir = "";
    private PostgresManager _manager = null!;
    private int _port;
    private DbCredentials _credentials = null!;
    private int _userId;
    private int _otherUserId;

    public async Task InitializeAsync()
    {
        _pgBinDirectory = PostgresTestEnvironment.ResolvePgBinDirectory();
        _tempDir = Directory.CreateTempSubdirectory("organizatodo-syncpull-test-").FullName;
        var logsDir = Path.Combine(_tempDir, "logs");
        Directory.CreateDirectory(logsDir);
        _port = PortAllocator.FindAvailablePort();
        _credentials = new DbCredentials("organizatodo_app", "test-password-123");

        _manager = new PostgresManager(_pgBinDirectory, Path.Combine(_tempDir, "pgdata"), _port, _credentials, logsDir);
        await _manager.InitializeIfNeededAsync();
        await _manager.StartAsync();
        await _manager.WaitUntilReadyAsync(TimeSpan.FromSeconds(30));

        var adminConnectionString = PostgresConnectionStringBuilder.Build(_port, "postgres", _credentials.Username, _credentials.Password);
        await using var adminConnection = new NpgsqlConnection(adminConnectionString);
        await adminConnection.OpenAsync();
        var bootstrapper = new SchemaBootstrapper(Path.Combine(AppContext.BaseDirectory, "Database"));
        await bootstrapper.BootstrapAsync(adminConnection, "organizatodo", isShoppingDatabase: false);

        await using var conn = await OpenConnectionAsync();
        _userId = await conn.ExecuteScalarAsync<int>(
            "INSERT INTO \"Users\" (\"Email\", \"PasswordHash\", \"Role\") VALUES ('pull-a@t.com', 'x', 'User') RETURNING \"Id\"");
        _otherUserId = await conn.ExecuteScalarAsync<int>(
            "INSERT INTO \"Users\" (\"Email\", \"PasswordHash\", \"Role\") VALUES ('pull-b@t.com', 'x', 'User') RETURNING \"Id\"");
    }

    public async Task DisposeAsync()
    {
        await _manager.StopAsync();
        Directory.Delete(_tempDir, recursive: true);
    }

    private async Task<NpgsqlConnection> OpenConnectionAsync()
    {
        var connectionString = PostgresConnectionStringBuilder.Build(_port, "organizatodo", _credentials.Username, _credentials.Password);
        var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();
        return conn;
    }

    [Fact]
    public async Task PullChangesAsync_ReturnsOnlyCallingUsersRows_ForDirectUserIdTable()
    {
        await using var conn = await OpenConnectionAsync();
        await conn.ExecuteAsync(
            "INSERT INTO \"HousingServices\" (\"UserId\", \"Name\", \"Amount\") VALUES (@userId, 'Mine', 100)", new { userId = _userId });
        await conn.ExecuteAsync(
            "INSERT INTO \"HousingServices\" (\"UserId\", \"Name\", \"Amount\") VALUES (@otherUserId, 'NotMine', 200)", new { otherUserId = _otherUserId });

        var service = new SyncPullService(PostgresConnectionStringBuilder.Build(_port, "organizatodo", _credentials.Username, _credentials.Password));
        var result = await service.PullChangesAsync(_userId, DateTimeOffset.MinValue);

        var housingRows = result["housingServices"];
        Assert.Single(housingRows);
    }

    [Fact]
    public async Task PullChangesAsync_ReturnsOnlyCallingUsersRows_ForJoinBasedOwnershipTable()
    {
        await using var conn = await OpenConnectionAsync();
        var myListId = await conn.ExecuteScalarAsync<int>(
            "INSERT INTO \"ShoppingLists\" (\"UserId\", \"Month\", \"Year\") VALUES (@userId, 1, 2026) RETURNING \"Id\"", new { userId = _userId });
        var otherListId = await conn.ExecuteScalarAsync<int>(
            "INSERT INTO \"ShoppingLists\" (\"UserId\", \"Month\", \"Year\") VALUES (@otherUserId, 1, 2026) RETURNING \"Id\"", new { otherUserId = _otherUserId });
        await conn.ExecuteAsync(
            "INSERT INTO \"ShoppingListItems\" (\"ShoppingListId\", \"ProductName\") VALUES (@myListId, 'Milk')", new { myListId });
        await conn.ExecuteAsync(
            "INSERT INTO \"ShoppingListItems\" (\"ShoppingListId\", \"ProductName\") VALUES (@otherListId, 'Bread')", new { otherListId });

        var service = new SyncPullService(PostgresConnectionStringBuilder.Build(_port, "organizatodo", _credentials.Username, _credentials.Password));
        var result = await service.PullChangesAsync(_userId, DateTimeOffset.MinValue);

        var items = result["shoppingListItems"];
        Assert.Single(items);
    }

    [Fact]
    public async Task PullChangesAsync_ExcludesRowsOlderThanSinceWatermark()
    {
        await using var conn = await OpenConnectionAsync();
        await conn.ExecuteAsync(
            "INSERT INTO \"HousingServices\" (\"UserId\", \"Name\", \"Amount\") VALUES (@userId, 'Old', 100)", new { userId = _userId });

        var service = new SyncPullService(PostgresConnectionStringBuilder.Build(_port, "organizatodo", _credentials.Username, _credentials.Password));
        var result = await service.PullChangesAsync(_userId, DateTimeOffset.UtcNow.AddMinutes(1));

        Assert.Empty(result["housingServices"]);
    }
}
```

- [ ] **Step 6: Run test to verify it fails**

Run: `dotnet test tests/OrganizaTodo.Desktop.Tests --filter SyncPullServiceIntegrationTests`
Expected: FAIL (compile error — `SyncPullService` doesn't exist yet)

- [ ] **Step 7: Implement `SyncPullService`**

`src/OrganizaTodo.Web/Sync/SyncPullService.cs`:

```csharp
using Dapper;
using Npgsql;

namespace OrganizaTodo.Web.Sync;

public interface ISyncPullService
{
    Task<Dictionary<string, IReadOnlyList<dynamic>>> PullChangesAsync(int userId, DateTimeOffset since, CancellationToken ct = default);
}

public sealed class SyncPullService(string connectionString) : ISyncPullService
{
    public async Task<Dictionary<string, IReadOnlyList<dynamic>>> PullChangesAsync(int userId, DateTimeOffset since, CancellationToken ct = default)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);

        var result = new Dictionary<string, IReadOnlyList<dynamic>>();

        foreach (var table in SyncTableRegistry.Tables)
        {
            var sql = $"""
                SELECT "Id", "SyncId", "UpdatedAt", "IsDeleted", {string.Join(", ", table.Columns.Select(c => $"\"{c}\""))}
                FROM "{table.TableName}"
                WHERE {table.OwnershipWhereClause} AND "UpdatedAt" > @since
                """;

            var rows = await connection.QueryAsync(new CommandDefinition(sql, new { userId, since }, cancellationToken: ct));
            result[table.JsonKey] = rows.ToList();
        }

        return result;
    }
}
```

`SyncTableDescriptor.Columns` deliberately excludes `Id` (per Global Constraints) but the query above selects it anyway alongside `SyncId`/`UpdatedAt`/`IsDeleted` — `Id` is included in the result for local debugging/logging convenience only; the desktop client (a follow-up plan) must key all upserts by `SyncId`, never by `Id`, since `Id` values aren't stable across databases.

- [ ] **Step 8: Run test to verify it passes**

Run: `dotnet test tests/OrganizaTodo.Desktop.Tests --filter SyncPullServiceIntegrationTests`
Expected: PASS (3 tests)

- [ ] **Step 9: Commit**

```bash
git add src/OrganizaTodo.Web/Sync tests/OrganizaTodo.Desktop.Tests/SyncTableRegistryTests.cs tests/OrganizaTodo.Desktop.Tests/SyncPullServiceIntegrationTests.cs
git commit -m "add SyncTableRegistry and SyncPullService for generic table-driven pull"
```

---

### Task 6: Generic push (upsert with last-write-wins, cross-user isolation)

**Files:**
- Create: `src/OrganizaTodo.Web/Sync/SyncPushService.cs`
- Test: `tests/OrganizaTodo.Desktop.Tests/SyncPushServiceIntegrationTests.cs`

**Interfaces:**
- Consumes: `SyncTableRegistry` (Task 5)
- Produces:
  ```csharp
  namespace OrganizaTodo.Web.Sync;

  public sealed record SyncRowChange(Guid SyncId, DateTimeOffset UpdatedAt, bool IsDeleted, IReadOnlyDictionary<string, object?> Fields);

  public interface ISyncPushService
  {
      Task ApplyChangesAsync(int userId, IReadOnlyDictionary<string, IReadOnlyList<SyncRowChange>> changesByTable, CancellationToken ct = default);
  }
  ```

- [ ] **Step 1: Write the failing integration test**

`tests/OrganizaTodo.Desktop.Tests/SyncPushServiceIntegrationTests.cs`:

```csharp
using Dapper;
using Npgsql;
using OrganizaTodo.Desktop.Lifecycle;
using OrganizaTodo.Web.Sync;

namespace OrganizaTodo.Desktop.Tests;

public class SyncPushServiceIntegrationTests : IAsyncLifetime
{
    private string _pgBinDirectory = "";
    private string _tempDir = "";
    private PostgresManager _manager = null!;
    private int _port;
    private DbCredentials _credentials = null!;
    private int _userId;
    private int _otherUserId;
    private string _connectionString = "";

    public async Task InitializeAsync()
    {
        _pgBinDirectory = PostgresTestEnvironment.ResolvePgBinDirectory();
        _tempDir = Directory.CreateTempSubdirectory("organizatodo-syncpush-test-").FullName;
        var logsDir = Path.Combine(_tempDir, "logs");
        Directory.CreateDirectory(logsDir);
        _port = PortAllocator.FindAvailablePort();
        _credentials = new DbCredentials("organizatodo_app", "test-password-123");

        _manager = new PostgresManager(_pgBinDirectory, Path.Combine(_tempDir, "pgdata"), _port, _credentials, logsDir);
        await _manager.InitializeIfNeededAsync();
        await _manager.StartAsync();
        await _manager.WaitUntilReadyAsync(TimeSpan.FromSeconds(30));

        var adminConnectionString = PostgresConnectionStringBuilder.Build(_port, "postgres", _credentials.Username, _credentials.Password);
        await using var adminConnection = new NpgsqlConnection(adminConnectionString);
        await adminConnection.OpenAsync();
        var bootstrapper = new SchemaBootstrapper(Path.Combine(AppContext.BaseDirectory, "Database"));
        await bootstrapper.BootstrapAsync(adminConnection, "organizatodo", isShoppingDatabase: false);

        _connectionString = PostgresConnectionStringBuilder.Build(_port, "organizatodo", _credentials.Username, _credentials.Password);
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        _userId = await conn.ExecuteScalarAsync<int>(
            "INSERT INTO \"Users\" (\"Email\", \"PasswordHash\", \"Role\") VALUES ('push-a@t.com', 'x', 'User') RETURNING \"Id\"");
        _otherUserId = await conn.ExecuteScalarAsync<int>(
            "INSERT INTO \"Users\" (\"Email\", \"PasswordHash\", \"Role\") VALUES ('push-b@t.com', 'x', 'User') RETURNING \"Id\"");
    }

    public async Task DisposeAsync()
    {
        await _manager.StopAsync();
        Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public async Task ApplyChangesAsync_NewSyncId_InsertsRow()
    {
        var service = new SyncPushService(_connectionString);
        var syncId = Guid.NewGuid();

        await service.ApplyChangesAsync(_userId, new Dictionary<string, IReadOnlyList<SyncRowChange>>
        {
            ["housingServices"] = new List<SyncRowChange>
            {
                new(syncId, DateTimeOffset.UtcNow, false, new Dictionary<string, object?>
                {
                    ["UserId"] = _userId, ["Name"] = "Pushed", ["Amount"] = 150m,
                    ["DueDay"] = 1, ["Periodicity"] = "MONTHLY", ["IsPaid"] = false, ["IsActive"] = true
                })
            }
        });

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        var name = await conn.ExecuteScalarAsync<string>(
            "SELECT \"Name\" FROM \"HousingServices\" WHERE \"SyncId\" = @syncId", new { syncId });
        Assert.Equal("Pushed", name);
    }

    [Fact]
    public async Task ApplyChangesAsync_NewerIncomingUpdatedAt_OverwritesExistingRow()
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        var syncId = Guid.NewGuid();
        await conn.ExecuteAsync(
            "INSERT INTO \"HousingServices\" (\"UserId\", \"Name\", \"Amount\", \"SyncId\", \"UpdatedAt\") VALUES (@userId, 'Original', 100, @syncId, @updatedAt)",
            new { userId = _userId, syncId, updatedAt = DateTimeOffset.UtcNow.AddHours(-1) });

        // Newer than the original (-1h) but a distinct, fixed value rather than
        // "now" — lets the assertion below tell "preserved" apart from "clobbered
        // with the server's receive time," which DateTimeOffset.UtcNow couldn't.
        var incomingUpdatedAt = DateTimeOffset.UtcNow.AddMinutes(-30);

        var service = new SyncPushService(_connectionString);
        await service.ApplyChangesAsync(_userId, new Dictionary<string, IReadOnlyList<SyncRowChange>>
        {
            ["housingServices"] = new List<SyncRowChange>
            {
                new(syncId, incomingUpdatedAt, false, new Dictionary<string, object?>
                {
                    ["UserId"] = _userId, ["Name"] = "Updated", ["Amount"] = 999m,
                    ["DueDay"] = 1, ["Periodicity"] = "MONTHLY", ["IsPaid"] = false, ["IsActive"] = true
                })
            }
        });

        var name = await conn.ExecuteScalarAsync<string>(
            "SELECT \"Name\" FROM \"HousingServices\" WHERE \"SyncId\" = @syncId", new { syncId });
        Assert.Equal("Updated", name);

        // Proves the trigger from Task 1 didn't clobber the pushed UpdatedAt with NOW() —
        // this is the exact bug the Task 1 conditional-trigger fix exists to prevent.
        var storedUpdatedAt = await conn.ExecuteScalarAsync<DateTimeOffset>(
            "SELECT \"UpdatedAt\" FROM \"HousingServices\" WHERE \"SyncId\" = @syncId", new { syncId });
        Assert.Equal(incomingUpdatedAt, storedUpdatedAt, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task ApplyChangesAsync_OlderIncomingUpdatedAt_IsDiscarded()
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        var syncId = Guid.NewGuid();
        var remoteUpdatedAt = DateTimeOffset.UtcNow;
        await conn.ExecuteAsync(
            "INSERT INTO \"HousingServices\" (\"UserId\", \"Name\", \"Amount\", \"SyncId\", \"UpdatedAt\") VALUES (@userId, 'RemoteWins', 100, @syncId, @updatedAt)",
            new { userId = _userId, syncId, updatedAt = remoteUpdatedAt });

        var service = new SyncPushService(_connectionString);
        await service.ApplyChangesAsync(_userId, new Dictionary<string, IReadOnlyList<SyncRowChange>>
        {
            ["housingServices"] = new List<SyncRowChange>
            {
                new(syncId, remoteUpdatedAt.AddHours(-1), false, new Dictionary<string, object?>
                {
                    ["UserId"] = _userId, ["Name"] = "StaleIncoming", ["Amount"] = 1m,
                    ["DueDay"] = 1, ["Periodicity"] = "MONTHLY", ["IsPaid"] = false, ["IsActive"] = true
                })
            }
        });

        var name = await conn.ExecuteScalarAsync<string>(
            "SELECT \"Name\" FROM \"HousingServices\" WHERE \"SyncId\" = @syncId", new { syncId });
        Assert.Equal("RemoteWins", name);
    }

    [Fact]
    public async Task ApplyChangesAsync_IncomingRowClaimsAnotherUsersId_IsRejected()
    {
        var service = new SyncPushService(_connectionString);
        var syncId = Guid.NewGuid();

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ApplyChangesAsync(_userId, new Dictionary<string, IReadOnlyList<SyncRowChange>>
        {
            ["housingServices"] = new List<SyncRowChange>
            {
                new(syncId, DateTimeOffset.UtcNow, false, new Dictionary<string, object?>
                {
                    ["UserId"] = _otherUserId, ["Name"] = "ShouldBeRejected", ["Amount"] = 1m,
                    ["DueDay"] = 1, ["Periodicity"] = "MONTHLY", ["IsPaid"] = false, ["IsActive"] = true
                })
            }
        }));

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        var exists = await conn.ExecuteScalarAsync<bool>(
            "SELECT EXISTS (SELECT 1 FROM \"HousingServices\" WHERE \"SyncId\" = @syncId)", new { syncId });
        Assert.False(exists);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/OrganizaTodo.Desktop.Tests --filter SyncPushServiceIntegrationTests`
Expected: FAIL (compile error — `SyncPushService`/`SyncRowChange` don't exist yet)

- [ ] **Step 3: Implement `SyncPushService`**

`src/OrganizaTodo.Web/Sync/SyncPushService.cs`:

```csharp
using Dapper;
using Npgsql;

namespace OrganizaTodo.Web.Sync;

public sealed record SyncRowChange(Guid SyncId, DateTimeOffset UpdatedAt, bool IsDeleted, IReadOnlyDictionary<string, object?> Fields);

public interface ISyncPushService
{
    Task ApplyChangesAsync(int userId, IReadOnlyDictionary<string, IReadOnlyList<SyncRowChange>> changesByTable, CancellationToken ct = default);
}

public sealed class SyncPushService(string connectionString) : ISyncPushService
{
    private static readonly Dictionary<string, SyncTableDescriptor> TablesByJsonKey =
        SyncTableRegistry.Tables.ToDictionary(t => t.JsonKey);

    public async Task ApplyChangesAsync(int userId, IReadOnlyDictionary<string, IReadOnlyList<SyncRowChange>> changesByTable, CancellationToken ct = default)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);

        foreach (var (jsonKey, rows) in changesByTable)
        {
            var table = TablesByJsonKey[jsonKey];

            foreach (var row in rows)
            {
                if (table.Columns.Contains("UserId") && !Equals(row.Fields.GetValueOrDefault("UserId"), userId))
                {
                    throw new InvalidOperationException(
                        $"Rejected sync row for table {table.TableName}: UserId in payload does not match the authenticated caller.");
                }

                var columns = table.Columns.Where(c => c != "UserId").Prepend("UserId").ToList();
                var insertColumns = string.Join(", ", columns.Select(c => $"\"{c}\""));
                var insertParams = string.Join(", ", columns.Select(c => $"@{c}"));
                var updateAssignments = string.Join(", ", columns.Where(c => c != "UserId").Select(c => $"\"{c}\" = @{c}"));

                var parameters = new DynamicParameters();
                parameters.Add("SyncId", row.SyncId);
                parameters.Add("UpdatedAt", row.UpdatedAt);
                parameters.Add("IsDeleted", row.IsDeleted);
                foreach (var column in columns)
                {
                    parameters.Add(column, column == "UserId" ? userId : row.Fields.GetValueOrDefault(column));
                }

                var sql = $"""
                    INSERT INTO "{table.TableName}" ("SyncId", "UpdatedAt", "IsDeleted", {insertColumns})
                    VALUES (@SyncId, @UpdatedAt, @IsDeleted, {insertParams})
                    ON CONFLICT ("SyncId") DO UPDATE SET
                        "UpdatedAt" = EXCLUDED."UpdatedAt",
                        "IsDeleted" = EXCLUDED."IsDeleted",
                        {updateAssignments}
                    WHERE "{table.TableName}"."UpdatedAt" < EXCLUDED."UpdatedAt"
                    """;

                await connection.ExecuteAsync(new CommandDefinition(sql, parameters, transaction, cancellationToken: ct));
            }
        }

        await transaction.CommitAsync(ct);
    }
}
```

The `ON CONFLICT ("SyncId") DO UPDATE ... WHERE "<table>"."UpdatedAt" < EXCLUDED."UpdatedAt"` clause is what implements last-write-wins atomically: Postgres only applies the update if the existing row's `UpdatedAt` is older than the incoming one; otherwise the `INSERT ... ON CONFLICT` is a no-op for that row, leaving the existing (newer) data untouched. Note this bypasses the `trg_set_updated_at` trigger's own `NOW()` behavior for pushed rows — that's intentional: the incoming `UpdatedAt` must be preserved exactly as the pushing device recorded it, not overwritten with the server's receive time, or last-write-wins comparisons would be meaningless.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/OrganizaTodo.Desktop.Tests --filter SyncPushServiceIntegrationTests`
Expected: PASS (4 tests)

- [ ] **Step 5: Commit**

```bash
git add src/OrganizaTodo.Web/Sync/SyncPushService.cs tests/OrganizaTodo.Desktop.Tests/SyncPushServiceIntegrationTests.cs
git commit -m "add SyncPushService with atomic last-write-wins upsert and UserId isolation"
```

---

### Task 7: `POST /api/sync` — wire push + pull together, authenticated by token

**Files:**
- Modify: `src/OrganizaTodo.Web/Controllers/SyncController.cs`
- Create: `src/OrganizaTodo.Web/Models/SyncRequest.cs`
- Create: `src/OrganizaTodo.Web/Models/SyncResponse.cs`
- Modify: `src/OrganizaTodo.Web/Repositories/ISyncTokenRepository.cs`
- Modify: `src/OrganizaTodo.Web/Repositories/SyncTokenRepository.cs`
- Modify: `src/OrganizaTodo.Web/Program.cs` (register `ISyncPullService`, `ISyncPushService`)
- Test: `tests/OrganizaTodo.Desktop.Tests/SyncEndpointIntegrationTests.cs`

No custom ASP.NET Core `AuthenticationHandler` is created for this — the `/api/sync` action checks the bearer token inline (Step 3), simpler than the full authentication-scheme machinery for a single endpoint.

**Interfaces:**
- Consumes: `ISyncPullService`, `ISyncPushService` (Task 5, 6), `ISyncTokenService.HashToken` (Task 3)
- Produces: `POST /api/sync` — `Authorization: Bearer <token>` required. Request `{ "since": "...", "changes": { "<jsonKey>": [ { "syncId", "updatedAt", "isDeleted", ...fields } ] } }`. Response `{ "syncedAt": "...", "changes": { "<jsonKey>": [...] } }`.
- Modifies `ISyncTokenRepository`: add `Task<int?> ResolveUserIdAsync(string tokenHash)` and `Task TouchLastUsedAsync(string tokenHash)`.

- [ ] **Step 1: Write the failing end-to-end integration test**

`tests/OrganizaTodo.Desktop.Tests/SyncEndpointIntegrationTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Dapper;
using Npgsql;
using OrganizaTodo.Desktop.Lifecycle;
using OrganizaTodo.Web.Models;

namespace OrganizaTodo.Desktop.Tests;

public class SyncEndpointIntegrationTests : IAsyncLifetime
{
    private string _pgBinDirectory = "";
    private string _tempDir = "";
    private PostgresManager _manager = null!;
    private int _pgPort;
    private DbCredentials _credentials = null!;
    private OrganizaTodo.Desktop.Lifecycle.KestrelWebAppRunner _runner = null!;
    private string _baseUrl = "";
    private string _connectionString = "";
    private int _userId;

    public async Task InitializeAsync()
    {
        _pgBinDirectory = PostgresTestEnvironment.ResolvePgBinDirectory();
        _tempDir = Directory.CreateTempSubdirectory("organizatodo-syncendpoint-test-").FullName;
        var logsDir = Path.Combine(_tempDir, "logs");
        Directory.CreateDirectory(logsDir);
        _pgPort = PortAllocator.FindAvailablePort();
        _credentials = new DbCredentials("organizatodo_app", "test-password-123");

        _manager = new PostgresManager(_pgBinDirectory, Path.Combine(_tempDir, "pgdata"), _pgPort, _credentials, logsDir);
        await _manager.InitializeIfNeededAsync();
        await _manager.StartAsync();
        await _manager.WaitUntilReadyAsync(TimeSpan.FromSeconds(30));

        var adminConnectionString = PostgresConnectionStringBuilder.Build(_pgPort, "postgres", _credentials.Username, _credentials.Password);
        await using var adminConnection = new NpgsqlConnection(adminConnectionString);
        await adminConnection.OpenAsync();
        var bootstrapper = new SchemaBootstrapper(Path.Combine(AppContext.BaseDirectory, "Database"));
        await bootstrapper.BootstrapAsync(adminConnection, "organizatodo", isShoppingDatabase: false);
        await bootstrapper.BootstrapAsync(adminConnection, "organizatodo_shopping", isShoppingDatabase: true);

        _connectionString = PostgresConnectionStringBuilder.Build(_pgPort, "organizatodo", _credentials.Username, _credentials.Password);

        _runner = new OrganizaTodo.Desktop.Lifecycle.KestrelWebAppRunner();
        _baseUrl = await _runner.StartAsync(
            _connectionString,
            PostgresConnectionStringBuilder.Build(_pgPort, "organizatodo_shopping", _credentials.Username, _credentials.Password));

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        var passwordHash = BCrypt.Net.BCrypt.HashPassword("correct-password");
        _userId = await conn.ExecuteScalarAsync<int>(
            "SELECT sp_users_create(@email, @passwordHash, 'User')",
            new { email = "sync-e2e@t.com", passwordHash });
    }

    public async Task DisposeAsync()
    {
        await _runner.StopAsync();
        await _manager.StopAsync();
        Directory.Delete(_tempDir, recursive: true);
    }

    private async Task<string> ConnectAndGetTokenAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync($"{_baseUrl}/api/sync/connect",
            new ConnectRequest("sync-e2e@t.com", "correct-password"));
        var body = await response.Content.ReadFromJsonAsync<ConnectResponse>();
        return body!.Token;
    }

    [Fact]
    public async Task Sync_WithoutToken_ReturnsUnauthorized()
    {
        using var client = new HttpClient();

        var response = await client.PostAsJsonAsync($"{_baseUrl}/api/sync",
            new SyncRequest(DateTimeOffset.MinValue, new Dictionary<string, List<SyncRowChangeDto>>()));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Sync_PushThenPull_RoundTripsAChange()
    {
        using var client = new HttpClient();
        var token = await ConnectAndGetTokenAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var syncId = Guid.NewGuid();
        var request = new SyncRequest(
            DateTimeOffset.MinValue,
            new Dictionary<string, List<SyncRowChangeDto>>
            {
                ["housingServices"] = new List<SyncRowChangeDto>
                {
                    new(syncId, DateTimeOffset.UtcNow, false, new Dictionary<string, object?>
                    {
                        ["UserId"] = _userId, ["Name"] = "RoundTrip", ["Amount"] = 42m,
                        ["DueDay"] = 1, ["Periodicity"] = "MONTHLY", ["IsPaid"] = false, ["IsActive"] = true
                    })
                }
            });

        var response = await client.PostAsJsonAsync($"{_baseUrl}/api/sync", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<SyncResponse>();
        Assert.NotNull(body);
        Assert.Contains(body!.Changes["housingServices"], row => (string)row["Name"] == "RoundTrip");
    }
}
```

`SyncRequest`/`SyncResponse`/`SyncRowChangeDto` are new DTOs this task defines (Step 3) — request/response JSON shapes distinct from `SyncPushService`'s internal `SyncRowChange` record (which uses `Guid`/`DateTimeOffset` typed fields; the DTOs are what actually (de)serializes over the wire and the controller maps between them).

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/OrganizaTodo.Desktop.Tests --filter SyncEndpointIntegrationTests`
Expected: FAIL (compile error — `SyncRequest`/`SyncResponse`/`SyncRowChangeDto` and the `/api/sync` action don't exist yet)

- [ ] **Step 3: Implement the DTOs, token authentication, and the endpoint**

`src/OrganizaTodo.Web/Models/SyncRequest.cs`:
```csharp
namespace OrganizaTodo.Web.Models;

public sealed record SyncRowChangeDto(Guid SyncId, DateTimeOffset UpdatedAt, bool IsDeleted, Dictionary<string, object?> Fields);

public sealed record SyncRequest(DateTimeOffset Since, Dictionary<string, List<SyncRowChangeDto>> Changes);
```

`src/OrganizaTodo.Web/Models/SyncResponse.cs`:
```csharp
namespace OrganizaTodo.Web.Models;

public sealed record SyncResponse(DateTimeOffset SyncedAt, Dictionary<string, IReadOnlyList<dynamic>> Changes);
```

Extend `src/OrganizaTodo.Web/Repositories/ISyncTokenRepository.cs`:
```csharp
namespace OrganizaTodo.Web.Repositories;

public interface ISyncTokenRepository
{
    Task StoreTokenAsync(int userId, string tokenHash);
    Task<int?> ResolveUserIdAsync(string tokenHash);
    Task TouchLastUsedAsync(string tokenHash);
}
```

Extend `src/OrganizaTodo.Web/Repositories/SyncTokenRepository.cs`:
```csharp
using Dapper;
using OrganizaTodo.Web.Data;

namespace OrganizaTodo.Web.Repositories;

public sealed class SyncTokenRepository(IDbConnectionFactory connectionFactory) : ISyncTokenRepository
{
    public async Task StoreTokenAsync(int userId, string tokenHash)
    {
        using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync(
            "INSERT INTO \"SyncTokens\" (\"UserId\", \"TokenHash\") VALUES (@userId, @tokenHash)",
            new { userId, tokenHash });
    }

    public async Task<int?> ResolveUserIdAsync(string tokenHash)
    {
        using var connection = connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<int?>(
            "SELECT \"UserId\" FROM \"SyncTokens\" WHERE \"TokenHash\" = @tokenHash",
            new { tokenHash });
    }

    public async Task TouchLastUsedAsync(string tokenHash)
    {
        using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync(
            "UPDATE \"SyncTokens\" SET \"LastUsedAt\" = NOW() WHERE \"TokenHash\" = @tokenHash",
            new { tokenHash });
    }
}
```

Replace `src/OrganizaTodo.Web/Controllers/SyncController.cs` with the full controller (connect endpoint unchanged from Task 4, plus the new `/api/sync` action doing its own lightweight bearer-token check inline rather than a full ASP.NET Core authentication scheme — simpler and sufficient for a single-purpose token, avoids pulling in the authentication-handler machinery for one endpoint):

```csharp
using Microsoft.AspNetCore.Mvc;
using OrganizaTodo.Web.Models;
using OrganizaTodo.Web.Repositories;
using OrganizaTodo.Web.Services;
using OrganizaTodo.Web.Sync;

namespace OrganizaTodo.Web.Controllers;

[ApiController]
[Route("api/sync")]
public sealed class SyncController(
    IAuthService authService,
    ISyncTokenService syncTokenService,
    ISyncTokenRepository syncTokenRepository,
    ISyncPullService syncPullService,
    ISyncPushService syncPushService) : ControllerBase
{
    [HttpPost("connect")]
    public async Task<IActionResult> Connect([FromBody] ConnectRequest request)
    {
        var user = await authService.VerifyCredentialsAsync(request.Email, request.Password);
        if (user is null)
        {
            return Unauthorized();
        }

        var (plaintextToken, tokenHash) = syncTokenService.GenerateToken();
        await syncTokenRepository.StoreTokenAsync(user.Id, tokenHash);

        return Ok(new ConnectResponse(plaintextToken, user.Id));
    }

    [HttpPost("")]
    public async Task<IActionResult> Sync([FromBody] SyncRequest request)
    {
        var authHeader = Request.Headers.Authorization.ToString();
        if (!authHeader.StartsWith("Bearer ", StringComparison.Ordinal))
        {
            return Unauthorized();
        }

        var plaintextToken = authHeader["Bearer ".Length..];
        var tokenHash = syncTokenService.HashToken(plaintextToken);
        var userId = await syncTokenRepository.ResolveUserIdAsync(tokenHash);
        if (userId is null)
        {
            return Unauthorized();
        }

        await syncTokenRepository.TouchLastUsedAsync(tokenHash);

        var changes = request.Changes.ToDictionary(
            kvp => kvp.Key,
            kvp => (IReadOnlyList<SyncRowChange>)kvp.Value
                .Select(dto => new SyncRowChange(dto.SyncId, dto.UpdatedAt, dto.IsDeleted, dto.Fields))
                .ToList());

        await syncPushService.ApplyChangesAsync(userId.Value, changes);
        var pulledChanges = await syncPullService.PullChangesAsync(userId.Value, request.Since);

        return Ok(new SyncResponse(DateTimeOffset.UtcNow, pulledChanges));
    }
}
```

Register the two new services in `Program.cs`'s `BuildApp`, alongside `ISyncTokenRepository`:
```csharp
builder.Services.AddScoped<ISyncPullService>(sp => new SyncPullService(builder.Configuration.GetConnectionString("DefaultConnection")!));
builder.Services.AddScoped<ISyncPushService>(sp => new SyncPushService(builder.Configuration.GetConnectionString("DefaultConnection")!));
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/OrganizaTodo.Desktop.Tests --filter SyncEndpointIntegrationTests`
Expected: PASS (2 tests)

- [ ] **Step 5: Run the full test suite**

Run: `export ORGANIZATODO_PG_BIN_DIR=<your pg bin dir>; dotnet test`
Expected: PASS (all tests across the solution, including every test from this plan and the desktop-app-runtime plan)

- [ ] **Step 6: Commit**

```bash
git add src/OrganizaTodo.Web/Controllers/SyncController.cs src/OrganizaTodo.Web/Models/SyncRequest.cs src/OrganizaTodo.Web/Models/SyncResponse.cs src/OrganizaTodo.Web/Repositories/ISyncTokenRepository.cs src/OrganizaTodo.Web/Repositories/SyncTokenRepository.cs src/OrganizaTodo.Web/Program.cs tests/OrganizaTodo.Desktop.Tests/SyncEndpointIntegrationTests.cs
git commit -m "add POST /api/sync wiring push and pull behind bearer-token auth"
```

---

## Follow-up (separate plan, not part of this one)

Once this plan is verified working, a follow-up plan covers the desktop-side sync client: the "Connect to remote server" UI flow, calling `/api/sync/connect` and `/api/sync`, storing the token and `LastSyncedAt` watermark locally (reusing `DbCredentialStore`'s protected-file pattern), the "Sync now" button, and applying pulled changes to the local database by `SyncId` upsert.

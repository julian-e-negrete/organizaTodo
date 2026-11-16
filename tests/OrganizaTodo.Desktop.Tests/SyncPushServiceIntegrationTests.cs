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
            "INSERT INTO \"Users\" (\"Email\", \"PasswordHash\", \"FullName\", \"Role\") VALUES ('push-a@t.com', 'x', 'Push A', 'User') RETURNING \"Id\"");
        _otherUserId = await conn.ExecuteScalarAsync<int>(
            "INSERT INTO \"Users\" (\"Email\", \"PasswordHash\", \"FullName\", \"Role\") VALUES ('push-b@t.com', 'x', 'Push B', 'User') RETURNING \"Id\"");
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
        // Dapper's ExecuteScalarAsync<DateTimeOffset> can't read a "timestamptz" column
        // directly — Npgsql's default CLR type for it is DateTime, and Dapper's scalar
        // path uses Convert.ChangeType, which throws converting DateTime -> DateTimeOffset.
        // Npgsql itself does support reading timestamptz as DateTimeOffset explicitly via
        // GetFieldValue<T>, so read through a raw reader instead of Dapper here.
        await using var cmd = new NpgsqlCommand(
            "SELECT \"UpdatedAt\" FROM \"HousingServices\" WHERE \"SyncId\" = @syncId", conn);
        cmd.Parameters.AddWithValue("syncId", syncId);
        await using var reader = await cmd.ExecuteReaderAsync();
        await reader.ReadAsync();
        var storedUpdatedAt = reader.GetFieldValue<DateTimeOffset>(0);

        // xunit 2.4.2 (this project's version) has no Assert.Equal(DateTimeOffset, DateTimeOffset, TimeSpan)
        // precision overload, so compare within tolerance manually instead.
        Assert.True((storedUpdatedAt - incomingUpdatedAt).Duration() < TimeSpan.FromSeconds(1),
            $"Expected {incomingUpdatedAt} within 1s, got {storedUpdatedAt}");
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

    [Fact]
    public async Task ApplyChangesAsync_JoinBasedOwnershipTable_RowPointingAtAnotherUsersParent_IsRejected()
    {
        // ShoppingListItems has no UserId column of its own — ownership is only
        // knowable by checking whether ShoppingListId belongs to the caller. This
        // proves that check actually runs, not just the direct-UserId path above.
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        var otherUsersListId = await conn.ExecuteScalarAsync<int>(
            "INSERT INTO \"ShoppingLists\" (\"UserId\", \"Month\", \"Year\") VALUES (@otherUserId, 1, 2026) RETURNING \"Id\"",
            new { otherUserId = _otherUserId });

        var service = new SyncPushService(_connectionString);
        var syncId = Guid.NewGuid();

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ApplyChangesAsync(_userId, new Dictionary<string, IReadOnlyList<SyncRowChange>>
        {
            ["shoppingListItems"] = new List<SyncRowChange>
            {
                new(syncId, DateTimeOffset.UtcNow, false, new Dictionary<string, object?>
                {
                    ["ShoppingListId"] = otherUsersListId, ["ProductName"] = "ShouldBeRejected",
                    ["Quantity"] = 1, ["Priority"] = 2
                })
            }
        }));

        var exists = await conn.ExecuteScalarAsync<bool>(
            "SELECT EXISTS (SELECT 1 FROM \"ShoppingListItems\" WHERE \"SyncId\" = @syncId)", new { syncId });
        Assert.False(exists);
    }

    [Fact]
    public async Task ApplyChangesAsync_SyncIdBelongsToAnotherUsersExistingRow_IsRejectedEvenWithOwnValidUserId()
    {
        // The payload's own UserId is valid for the caller — the payload-side check alone
        // would pass. The attack is reusing a SyncId that already belongs to a DIFFERENT
        // user's existing row: ON CONFLICT ("SyncId") is global, not scoped per user, so
        // without an existing-row ownership check this would silently overwrite the
        // victim's row's business columns while leaving it owned (UserId) by the victim.
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        var syncId = Guid.NewGuid();
        await conn.ExecuteAsync(
            "INSERT INTO \"HousingServices\" (\"UserId\", \"Name\", \"Amount\", \"SyncId\") VALUES (@otherUserId, 'VictimRow', 100, @syncId)",
            new { otherUserId = _otherUserId, syncId });

        var service = new SyncPushService(_connectionString);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ApplyChangesAsync(_userId, new Dictionary<string, IReadOnlyList<SyncRowChange>>
        {
            ["housingServices"] = new List<SyncRowChange>
            {
                new(syncId, DateTimeOffset.UtcNow, false, new Dictionary<string, object?>
                {
                    ["UserId"] = _userId, ["Name"] = "AttackerPushed", ["Amount"] = 1m,
                    ["DueDay"] = 1, ["Periodicity"] = "MONTHLY", ["IsPaid"] = false, ["IsActive"] = true
                })
            }
        }));

        var name = await conn.ExecuteScalarAsync<string>(
            "SELECT \"Name\" FROM \"HousingServices\" WHERE \"SyncId\" = @syncId", new { syncId });
        Assert.Equal("VictimRow", name);
        var ownerUserId = await conn.ExecuteScalarAsync<int>(
            "SELECT \"UserId\" FROM \"HousingServices\" WHERE \"SyncId\" = @syncId", new { syncId });
        Assert.Equal(_otherUserId, ownerUserId);
    }

    [Fact]
    public async Task ApplyChangesAsync_JoinBasedTable_SyncIdBelongsToAnotherUsersExistingRow_IsRejectedEvenWithOwnValidParent()
    {
        // Same attack as above, but for a join-based-ownership table: the payload's FK
        // (ShoppingListId) points at the caller's OWN list, so the payload-side check alone
        // would pass. The reused SyncId belongs to a row under a DIFFERENT user's list. If
        // unchecked, ON CONFLICT would reassign that row's ShoppingListId to the caller's
        // own list — moving the victim's data into the attacker's account.
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        var otherUsersListId = await conn.ExecuteScalarAsync<int>(
            "INSERT INTO \"ShoppingLists\" (\"UserId\", \"Month\", \"Year\") VALUES (@otherUserId, 1, 2026) RETURNING \"Id\"",
            new { otherUserId = _otherUserId });
        var callersOwnListId = await conn.ExecuteScalarAsync<int>(
            "INSERT INTO \"ShoppingLists\" (\"UserId\", \"Month\", \"Year\") VALUES (@userId, 2, 2026) RETURNING \"Id\"",
            new { userId = _userId });
        var syncId = Guid.NewGuid();
        await conn.ExecuteAsync(
            "INSERT INTO \"ShoppingListItems\" (\"ShoppingListId\", \"ProductName\", \"SyncId\") VALUES (@otherUsersListId, 'VictimItem', @syncId)",
            new { otherUsersListId, syncId });

        var service = new SyncPushService(_connectionString);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ApplyChangesAsync(_userId, new Dictionary<string, IReadOnlyList<SyncRowChange>>
        {
            ["shoppingListItems"] = new List<SyncRowChange>
            {
                new(syncId, DateTimeOffset.UtcNow, false, new Dictionary<string, object?>
                {
                    ["ShoppingListId"] = callersOwnListId, ["ProductName"] = "AttackerPushed",
                    ["Quantity"] = 1, ["Priority"] = 2
                })
            }
        }));

        var storedListId = await conn.ExecuteScalarAsync<int>(
            "SELECT \"ShoppingListId\" FROM \"ShoppingListItems\" WHERE \"SyncId\" = @syncId", new { syncId });
        Assert.Equal(otherUsersListId, storedListId);
        var storedName = await conn.ExecuteScalarAsync<string>(
            "SELECT \"ProductName\" FROM \"ShoppingListItems\" WHERE \"SyncId\" = @syncId", new { syncId });
        Assert.Equal("VictimItem", storedName);
    }

    // ------------------------------------------------------------------
    // Finding 1: secondary foreign keys embedded in a row's own business
    // data (distinct from the table's own ownership check) must also be
    // ownership-validated, or an attacker can push a row they legitimately
    // own while pointing e.g. ParentTransactionId at a victim's row.
    // ------------------------------------------------------------------

    [Fact]
    public async Task ApplyChangesAsync_SecondaryForeignKeyPointsAtAnotherUsersRow_IsRejected()
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        // Victim's own transaction — attacker will try to reference this by Id.
        var victimTransactionId = await conn.ExecuteScalarAsync<int>(
            """
            INSERT INTO "LedgerTransactions" ("UserId", "Amount", "Type", "Description", "TransactionDate", "BalanceAfter")
            VALUES (@otherUserId, 100, 'expense', 'Victim tx', NOW(), 100)
            RETURNING "Id"
            """,
            new { otherUserId = _otherUserId });

        var service = new SyncPushService(_connectionString);
        var syncId = Guid.NewGuid();

        // The pushed row's OWN UserId is the caller's — it would pass the row's own
        // ownership check. Only ParentTransactionId is attacker-controlled, pointing at
        // the victim's transaction (a sequential, trivially-enumerable Id).
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ApplyChangesAsync(_userId, new Dictionary<string, IReadOnlyList<SyncRowChange>>
        {
            ["ledgerTransactions"] = new List<SyncRowChange>
            {
                new(syncId, DateTimeOffset.UtcNow, false, new Dictionary<string, object?>
                {
                    ["UserId"] = _userId, ["Amount"] = 50m, ["Type"] = "expense",
                    ["Description"] = "AttackerTx", ["TransactionDate"] = "2026-01-15T00:00:00",
                    ["BalanceAfter"] = 50m, ["GeneratesInterest"] = false,
                    ["ParentTransactionId"] = victimTransactionId,
                })
            }
        }));

        var exists = await conn.ExecuteScalarAsync<bool>(
            "SELECT EXISTS (SELECT 1 FROM \"LedgerTransactions\" WHERE \"SyncId\" = @syncId)", new { syncId });
        Assert.False(exists);
    }

    [Fact]
    public async Task ApplyChangesAsync_SecondaryForeignKeyPointsAtCallersOwnRow_Succeeds()
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        var ownParentTransactionId = await conn.ExecuteScalarAsync<int>(
            """
            INSERT INTO "LedgerTransactions" ("UserId", "Amount", "Type", "Description", "TransactionDate", "BalanceAfter")
            VALUES (@userId, 100, 'expense', 'Own parent tx', NOW(), 100)
            RETURNING "Id"
            """,
            new { userId = _userId });

        var service = new SyncPushService(_connectionString);
        var syncId = Guid.NewGuid();

        await service.ApplyChangesAsync(_userId, new Dictionary<string, IReadOnlyList<SyncRowChange>>
        {
            ["ledgerTransactions"] = new List<SyncRowChange>
            {
                new(syncId, DateTimeOffset.UtcNow, false, new Dictionary<string, object?>
                {
                    ["UserId"] = _userId, ["Amount"] = 50m, ["Type"] = "expense",
                    ["Description"] = "ChildTx", ["TransactionDate"] = "2026-01-15T00:00:00",
                    ["BalanceAfter"] = 50m, ["GeneratesInterest"] = false,
                    ["ParentTransactionId"] = ownParentTransactionId,
                })
            }
        });

        var storedParentId = await conn.ExecuteScalarAsync<int?>(
            "SELECT \"ParentTransactionId\" FROM \"LedgerTransactions\" WHERE \"SyncId\" = @syncId", new { syncId });
        Assert.Equal(ownParentTransactionId, storedParentId);
    }

    [Fact]
    public async Task ApplyChangesAsync_LedgerTransactionTags_TagIdPointsAtAnotherUsersTag_IsRejected()
    {
        // LedgerTransactionTags is a join-based-ownership table (its own ownership is
        // validated via TransactionId -> LedgerTransactions in Check 2), but TagId is a
        // SECOND foreign key on the same row that references LedgerTags — a table with its
        // own direct UserId column. Without a secondary-FK check on TagId, an attacker can
        // push a row whose TransactionId is their own (passing Check 2) while TagId points
        // at another user's existing tag (a valid LedgerTags row, so the DB's own FK
        // constraint doesn't block the insert) — leaking the victim's tag name back to the
        // attacker via sp_ledger_history_get / sp_ledger_transaction_get_detail, which join
        // LedgerTransactionTags -> LedgerTags on TagId with no UserId re-check.
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        var ownTransactionId = await conn.ExecuteScalarAsync<int>(
            """
            INSERT INTO "LedgerTransactions" ("UserId", "Amount", "Type", "Description", "TransactionDate", "BalanceAfter")
            VALUES (@userId, 100, 'expense', 'Caller tx', NOW(), 100)
            RETURNING "Id"
            """,
            new { userId = _userId });

        var victimTagId = await conn.ExecuteScalarAsync<int>(
            "INSERT INTO \"LedgerTags\" (\"UserId\", \"Name\") VALUES (@otherUserId, 'VictimTag') RETURNING \"Id\"",
            new { otherUserId = _otherUserId });

        var service = new SyncPushService(_connectionString);
        var syncId = Guid.NewGuid();

        // The pushed row's TransactionId is the caller's own — it would pass the row's own
        // (join-based) ownership check. Only TagId is attacker-controlled, pointing at the
        // victim's tag (a sequential, trivially-enumerable Id).
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ApplyChangesAsync(_userId, new Dictionary<string, IReadOnlyList<SyncRowChange>>
        {
            ["ledgerTransactionTags"] = new List<SyncRowChange>
            {
                new(syncId, DateTimeOffset.UtcNow, false, new Dictionary<string, object?>
                {
                    ["TransactionId"] = ownTransactionId, ["TagId"] = victimTagId,
                })
            }
        }));

        var exists = await conn.ExecuteScalarAsync<bool>(
            "SELECT EXISTS (SELECT 1 FROM \"LedgerTransactionTags\" WHERE \"SyncId\" = @syncId)", new { syncId });
        Assert.False(exists);
    }

    [Fact]
    public async Task ApplyChangesAsync_LedgerTransactionTags_OwnTransactionAndOwnTag_Succeeds()
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        var ownTransactionId = await conn.ExecuteScalarAsync<int>(
            """
            INSERT INTO "LedgerTransactions" ("UserId", "Amount", "Type", "Description", "TransactionDate", "BalanceAfter")
            VALUES (@userId, 100, 'expense', 'Caller tx', NOW(), 100)
            RETURNING "Id"
            """,
            new { userId = _userId });

        var ownTagId = await conn.ExecuteScalarAsync<int>(
            "INSERT INTO \"LedgerTags\" (\"UserId\", \"Name\") VALUES (@userId, 'OwnTag') RETURNING \"Id\"",
            new { userId = _userId });

        var service = new SyncPushService(_connectionString);
        var syncId = Guid.NewGuid();

        await service.ApplyChangesAsync(_userId, new Dictionary<string, IReadOnlyList<SyncRowChange>>
        {
            ["ledgerTransactionTags"] = new List<SyncRowChange>
            {
                new(syncId, DateTimeOffset.UtcNow, false, new Dictionary<string, object?>
                {
                    ["TransactionId"] = ownTransactionId, ["TagId"] = ownTagId,
                })
            }
        });

        var storedTagId = await conn.ExecuteScalarAsync<int?>(
            "SELECT \"TagId\" FROM \"LedgerTransactionTags\" WHERE \"SyncId\" = @syncId", new { syncId });
        Assert.Equal(ownTagId, storedTagId);
    }

    // ------------------------------------------------------------------
    // Finding 2: date/timestamp fields arrive from JSON as plain strings.
    // Npgsql sends a C# string as SQL `text`, and Postgres rejects binding
    // `text` to a `timestamp`/`date` column (42804) unless it's parsed into
    // a proper DateTime/DateOnly first. Income.IncomeDate is NOT NULL, so
    // this table can't be pushed at all without the fix.
    // ------------------------------------------------------------------

    [Fact]
    public async Task ApplyChangesAsync_NotNullTimestampColumn_RoundTripsCorrectly()
    {
        var service = new SyncPushService(_connectionString);
        var syncId = Guid.NewGuid();

        await service.ApplyChangesAsync(_userId, new Dictionary<string, IReadOnlyList<SyncRowChange>>
        {
            ["income"] = new List<SyncRowChange>
            {
                new(syncId, DateTimeOffset.UtcNow, false, new Dictionary<string, object?>
                {
                    ["UserId"] = _userId, ["Description"] = "Salary", ["Amount"] = 1000m,
                    ["IncomeDate"] = "2026-03-10T00:00:00", ["Category"] = "Salary",
                    ["Month"] = 3, ["Year"] = 2026,
                })
            }
        });

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        var incomeDate = await conn.ExecuteScalarAsync<DateTime>(
            "SELECT \"IncomeDate\" FROM \"Income\" WHERE \"SyncId\" = @syncId", new { syncId });
        Assert.Equal(new DateTime(2026, 3, 10), incomeDate);
    }

    [Fact]
    public async Task ApplyChangesAsync_DateOnlyColumn_RoundTripsCorrectly()
    {
        // AssetPurchases.PurchaseDate is a Postgres DATE column (not TIMESTAMP) — this
        // proves DATE vs TIMESTAMP columns are parsed distinctly (DateOnly vs DateTime).
        var service = new SyncPushService(_connectionString);
        var syncId = Guid.NewGuid();

        await service.ApplyChangesAsync(_userId, new Dictionary<string, IReadOnlyList<SyncRowChange>>
        {
            ["assetPurchases"] = new List<SyncRowChange>
            {
                new(syncId, DateTimeOffset.UtcNow, false, new Dictionary<string, object?>
                {
                    ["UserId"] = _userId, ["AssetType"] = "USD", ["TotalArs"] = 5000m,
                    ["PurchaseDate"] = "2026-05-20",
                })
            }
        });

        // Read back via a raw NpgsqlCommand rather than Dapper's scalar path: Npgsql's default
        // CLR type for a `date` column is System.DateOnly, and Dapper's ExecuteScalarAsync<T>
        // uses Convert.ChangeType under the hood, which throws for DateOnly (it doesn't
        // implement IConvertible) unless the app-wide DateOnlyTypeHandler from Program.cs is
        // registered — which this service-level test intentionally doesn't depend on.
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            "SELECT \"PurchaseDate\" FROM \"AssetPurchases\" WHERE \"SyncId\" = @syncId", conn);
        cmd.Parameters.AddWithValue("syncId", syncId);
        var purchaseDate = (DateOnly)(await cmd.ExecuteScalarAsync())!;
        Assert.Equal(new DateOnly(2026, 5, 20), purchaseDate);
    }

    // ------------------------------------------------------------------
    // Finding 4: a payload supplying only "UserId" (no other business
    // columns) must not generate SQL with a trailing comma before WHERE.
    // ------------------------------------------------------------------

    [Fact]
    public async Task ApplyChangesAsync_PayloadWithOnlyUserIdColumn_IsRejectedCleanly_NotAsSqlSyntaxError()
    {
        // A degenerate push: only UserId in Fields, no other business columns. Before the fix
        // this generated "..."IsDeleted" = EXCLUDED."IsDeleted",\nWHERE ..." — a trailing-comma
        // SQL syntax error Postgres rejects at parse time (PostgresException, SqlState 42601).
        // Every currently-registered table has at least one required column with no DEFAULT, so
        // this payload could never legitimately succeed regardless — the fix rejects it early,
        // deterministically, and distinguishably from that raw Postgres syntax error.
        var service = new SyncPushService(_connectionString);
        var syncId = Guid.NewGuid();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.ApplyChangesAsync(_userId, new Dictionary<string, IReadOnlyList<SyncRowChange>>
        {
            ["housingServices"] = new List<SyncRowChange>
            {
                new(syncId, DateTimeOffset.UtcNow, false, new Dictionary<string, object?>
                {
                    ["UserId"] = _userId,
                })
            }
        }));

        Assert.Contains("no business columns", ex.Message);

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        var exists = await conn.ExecuteScalarAsync<bool>(
            "SELECT EXISTS (SELECT 1 FROM \"HousingServices\" WHERE \"SyncId\" = @syncId)", new { syncId });
        Assert.False(exists);
    }
}

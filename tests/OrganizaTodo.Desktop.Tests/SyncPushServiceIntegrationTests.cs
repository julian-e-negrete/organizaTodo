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
}

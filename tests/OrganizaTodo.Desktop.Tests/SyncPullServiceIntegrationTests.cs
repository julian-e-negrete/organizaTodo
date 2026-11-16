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
            "INSERT INTO \"Users\" (\"Email\", \"PasswordHash\", \"FullName\", \"Role\") VALUES ('pull-a@t.com', 'x', 'Pull A', 'User') RETURNING \"Id\"");
        _otherUserId = await conn.ExecuteScalarAsync<int>(
            "INSERT INTO \"Users\" (\"Email\", \"PasswordHash\", \"FullName\", \"Role\") VALUES ('pull-b@t.com', 'x', 'Pull B', 'User') RETURNING \"Id\"");
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

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
            "INSERT INTO \"Users\" (\"Email\", \"PasswordHash\", \"FullName\", \"Role\") VALUES ('t@t.com', 'x', 'Test User', 'User') RETURNING \"Id\"");
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
            "INSERT INTO \"Users\" (\"Email\", \"PasswordHash\", \"FullName\", \"Role\") VALUES ('trigger-test@t.com', 'x', 'Test User', 'User') RETURNING \"Id\"");
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

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
            "INSERT INTO \"Users\" (\"Email\", \"PasswordHash\", \"FullName\", \"Role\") VALUES ('softdelete@t.com', 'x', 'Soft Delete Test', 'User') RETURNING \"Id\"");
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

        // sp_housing_services_create returns void (not the new Id), per the Step 1
        // \sf dump — so the created row is looked up by UserId afterward instead of
        // reading the function's return value.
        await conn.ExecuteAsync(
            "SELECT sp_housing_services_create(@userId, 'Electricity', 100, 10, 'MONTHLY')",
            new { userId = _userId });
        var id = await conn.ExecuteScalarAsync<int>(
            "SELECT \"Id\" FROM \"HousingServices\" WHERE \"UserId\" = @userId", new { userId = _userId });

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

        // sp_vehicles_create takes 14 params and returns void, per the Step 1 \sf
        // dump — the brief's guessed 10-arg, Id-returning signature didn't match, so
        // the call below uses the real (user_id, plate, make, model, year, weekly_km,
        // fuel_efficiency_kml, fuel_price, insurance_monthly, patente_annual,
        // oil_change_interval_km, oil_change_cost, last_oil_change_date,
        // last_oil_change_km) signature, and the Id is looked up afterward.
        await conn.ExecuteAsync(
            "SELECT sp_vehicles_create(@userId, 'ABC123', 'Toyota', 'Corolla', 2020, 350, 10, 500, 1000, 5000, NULL, NULL, NULL, NULL)",
            new { userId = _userId });
        var id = await conn.ExecuteScalarAsync<int>(
            "SELECT \"Id\" FROM \"Vehicles\" WHERE \"UserId\" = @userId", new { userId = _userId });

        await conn.ExecuteAsync("SELECT sp_vehicles_delete(@id, @userId)", new { id, userId = _userId });

        var isDeletedFlagSet = await conn.ExecuteScalarAsync<bool>(
            "SELECT \"IsDeleted\" FROM \"Vehicles\" WHERE \"Id\" = @id", new { id });
        Assert.True(isDeletedFlagSet);

        var visibleRows = await conn.QueryAsync(
            "SELECT * FROM sp_vehicles_get_by_user_id(@userId, 1, 2026)", new { userId = _userId });
        Assert.DoesNotContain(visibleRows, row => (int)row.Id == id);
    }

    [Fact]
    public async Task IncomeDelete_HardDeleteConvertedToSoft_RowStaysInTableAndHiddenFromGetById()
    {
        await using var conn = await OpenConnectionAsync();

        var id = await conn.ExecuteScalarAsync<int>(
            "INSERT INTO \"Income\" (\"UserId\", \"Description\", \"Amount\", \"IncomeDate\", \"Category\", \"Month\", \"Year\") " +
            "VALUES (@userId, 'Salary', 1000, NOW(), 'Salary', 1, 2026) RETURNING \"Id\"",
            new { userId = _userId });

        await conn.ExecuteAsync("SELECT sp_income_delete(@id, @userId)", new { id, userId = _userId });

        var stillInTable = await conn.ExecuteScalarAsync<bool>(
            "SELECT EXISTS (SELECT 1 FROM \"Income\" WHERE \"Id\" = @id)", new { id });
        Assert.True(stillInTable, "delete must be soft (row stays), not a hard DELETE");

        var isDeletedFlagSet = await conn.ExecuteScalarAsync<bool>(
            "SELECT \"IsDeleted\" FROM \"Income\" WHERE \"Id\" = @id", new { id });
        Assert.True(isDeletedFlagSet);

        var visibleRows = await conn.QueryAsync(
            "SELECT * FROM sp_income_get_by_id(@id, @userId)", new { id, userId = _userId });
        Assert.Empty(visibleRows);
    }

    [Fact]
    public async Task SavingsGetByUserId_HidesSoftDeletedRow()
    {
        await using var conn = await OpenConnectionAsync();

        var id = await conn.ExecuteScalarAsync<int>(
            "INSERT INTO \"Savings\" (\"UserId\", \"Amount\", \"Month\", \"Year\", \"Notes\") " +
            "VALUES (@userId, 500, 1, 2026, 'Test saving') RETURNING \"Id\"",
            new { userId = _userId });

        await conn.ExecuteAsync(
            "UPDATE \"Savings\" SET \"IsDeleted\" = TRUE WHERE \"Id\" = @id", new { id });

        var visibleRows = await conn.QueryAsync(
            "SELECT * FROM sp_savings_get_by_user_id(@userId)", new { userId = _userId });
        Assert.DoesNotContain(visibleRows, row => (int)row.Id == id);
    }

    [Fact]
    public async Task ShoppingGetUserLists_ExcludesSoftDeletedItemsFromCountAndTotal()
    {
        // Post-review fix: before this migration, sp_shopping_delete_item hard-deleted
        // the row, so it simply vanished from sp_shopping_get_user_lists' LEFT JOIN
        // aggregate. Now that the delete is soft, a deleted item must still be
        // excluded from ItemCount/TotalEstimated, or those numbers would silently
        // keep counting items the user believes they removed.
        await using var conn = await OpenConnectionAsync();

        var listId = await conn.ExecuteScalarAsync<int>(
            "SELECT \"Id\" FROM sp_shopping_get_or_create_list(@userId, 1, 2026)", new { userId = _userId });

        await conn.ExecuteAsync(
            "SELECT sp_shopping_add_item(@listId, @userId, 'Milk', 2, 100, 'Coto', 1)",
            new { listId, userId = _userId });
        await conn.ExecuteAsync(
            "SELECT sp_shopping_add_item(@listId, @userId, 'Bread', 1, 50, 'Coto', 2)",
            new { listId, userId = _userId });

        var itemToDeleteId = await conn.ExecuteScalarAsync<int>(
            "SELECT \"Id\" FROM \"ShoppingListItems\" WHERE \"ShoppingListId\" = @listId AND \"ProductName\" = 'Milk'",
            new { listId });
        await conn.ExecuteAsync("SELECT sp_shopping_delete_item(@itemToDeleteId, @userId)",
            new { itemToDeleteId, userId = _userId });

        var list = (await conn.QuerySingleAsync(
            "SELECT * FROM sp_shopping_get_user_lists(@userId)", new { userId = _userId }));

        Assert.Equal(1L, (long)list.ItemCount);
        Assert.Equal(50m, (decimal)list.TotalEstimated);
    }

    [Fact]
    public async Task LedgerTagsGetByUserId_ExcludesSoftDeletedTransactionsFromUsageCount()
    {
        // Post-review fix: sp_savings_delete/sp_other_expenses_delete/
        // sp_asset_purchases_delete cascade-soft-delete linked "LedgerTransactions"
        // rows but never touch "LedgerTransactionTags" itself, so a tag's UsageCount
        // must exclude transactions whose "LedgerTransactions" row is now IsDeleted,
        // or the count would silently keep growing stale.
        await using var conn = await OpenConnectionAsync();

        var tagId = await conn.ExecuteScalarAsync<int>(
            "SELECT \"Id\" FROM sp_ledger_tags_get_or_create(@userId, 'groceries')", new { userId = _userId });

        var transactionId = await conn.ExecuteScalarAsync<int>(
            "INSERT INTO \"LedgerTransactions\" (\"UserId\", \"Amount\", \"Type\", \"Description\", \"TransactionDate\", \"BalanceAfter\") " +
            "VALUES (@userId, -100, 'expense', 'Test expense', NOW(), 900) RETURNING \"Id\"",
            new { userId = _userId });
        await conn.ExecuteAsync("SELECT sp_ledger_tags_link(@transactionId, @tagId)", new { transactionId, tagId });

        var usageBeforeDelete = await conn.ExecuteScalarAsync<long>(
            "SELECT \"UsageCount\" FROM sp_ledger_tags_get_by_user_id(@userId) WHERE \"Id\" = @tagId",
            new { userId = _userId, tagId });
        Assert.Equal(1L, usageBeforeDelete);

        await conn.ExecuteAsync(
            "UPDATE \"LedgerTransactions\" SET \"IsDeleted\" = TRUE WHERE \"Id\" = @transactionId", new { transactionId });

        var usageAfterDelete = await conn.ExecuteScalarAsync<long>(
            "SELECT \"UsageCount\" FROM sp_ledger_tags_get_by_user_id(@userId) WHERE \"Id\" = @tagId",
            new { userId = _userId, tagId });
        Assert.Equal(0L, usageAfterDelete);
    }
}

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
        Assert.Equal("ShoppingListId", descriptor.OwnershipForeignKeyColumn);
        Assert.Equal("ShoppingLists", descriptor.OwnershipParentTable);
    }

    [Fact]
    public void VehicleMonthlyPayments_UsesJoinBasedOwnership_NotDirectUserId()
    {
        var descriptor = SyncTableRegistry.Tables.Single(t => t.TableName == "VehicleMonthlyPayments");

        Assert.False(descriptor.Columns.Contains("UserId"));
        Assert.Contains("Vehicles", descriptor.OwnershipWhereClause);
        Assert.Equal("VehicleId", descriptor.OwnershipForeignKeyColumn);
        Assert.Equal("Vehicles", descriptor.OwnershipParentTable);
    }

    [Fact]
    public void LedgerTransactionTags_UsesJoinBasedOwnership_NotDirectUserId()
    {
        var descriptor = SyncTableRegistry.Tables.Single(t => t.TableName == "LedgerTransactionTags");

        Assert.False(descriptor.Columns.Contains("UserId"));
        Assert.Equal("TransactionId", descriptor.OwnershipForeignKeyColumn);
        Assert.Equal("LedgerTransactions", descriptor.OwnershipParentTable);
    }

    [Fact]
    public void EveryDescriptor_ColumnListExcludesId()
    {
        Assert.All(SyncTableRegistry.Tables, t => Assert.DoesNotContain("Id", t.Columns));
    }

    [Fact]
    public void EveryDirectUserIdTable_HasNoOwnershipForeignKeyColumn()
    {
        var joinBasedTables = new[] { "ShoppingListItems", "VehicleMonthlyPayments", "LedgerTransactionTags" };
        var directTables = SyncTableRegistry.Tables.Where(t => !joinBasedTables.Contains(t.TableName));

        Assert.All(directTables, t =>
        {
            Assert.True(t.Columns.Contains("UserId"), $"{t.TableName} was expected to have a direct UserId column");
            Assert.Null(t.OwnershipForeignKeyColumn);
            Assert.Null(t.OwnershipParentTable);
        });
    }

    // Finding 1: every secondary FK column embedded in a row's own business data (distinct
    // from the table's own ownership check) must be registered so SyncPushService actually
    // validates it. This locks in the six relationships the finding called out by name.
    [Theory]
    [InlineData("OtherExpenses", "VehicleId", "Vehicles")]
    [InlineData("Savings", "LedgerTransactionId", "LedgerTransactions")]
    [InlineData("AssetPurchases", "ExpenseId", "OtherExpenses")]
    [InlineData("AssetPurchases", "SavingId", "Savings")]
    [InlineData("VehicleMaintenanceLogs", "VehicleId", "Vehicles")]
    [InlineData("LedgerTransactions", "ParentTransactionId", "LedgerTransactions")]
    public void SecondaryForeignKeys_ContainsEveryKnownCrossTableReference(string tableName, string column, string parentTable)
    {
        var descriptor = SyncTableRegistry.Tables.Single(t => t.TableName == tableName);

        Assert.Contains(descriptor.SecondaryForeignKeys, fk => fk.Column == column && fk.ParentTable == parentTable);
    }

    // Finding 2: every column whose Postgres type is a date/timestamp must be registered as
    // such, or SyncPushService will try to bind a raw JSON string straight into a typed
    // column and Postgres will reject it (42804). Spot-checks the NOT NULL date columns that
    // block a push entirely today, plus the DATE-vs-TIMESTAMP distinction.
    [Theory]
    [InlineData("Income", "IncomeDate")]
    [InlineData("OtherExpenses", "ExpenseDate")]
    [InlineData("LedgerTransactions", "TransactionDate")]
    public void TimestampColumns_ContainsEveryNotNullDateColumn(string tableName, string column)
    {
        var descriptor = SyncTableRegistry.Tables.Single(t => t.TableName == tableName);

        Assert.Contains(column, descriptor.TimestampColumns);
    }

    [Theory]
    [InlineData("AssetPurchases", "PurchaseDate")]
    [InlineData("Vehicles", "LastOilChangeDate")]
    [InlineData("VehicleMaintenanceLogs", "ServiceDate")]
    public void DateOnlyColumns_ContainsEveryPostgresDateTypedColumn(string tableName, string column)
    {
        var descriptor = SyncTableRegistry.Tables.Single(t => t.TableName == tableName);

        Assert.Contains(column, descriptor.DateOnlyColumns);
        // A DATE column must never also be listed as a TIMESTAMP column — that would make the
        // parsing branch in SyncPushService ambiguous (DateOnly.Parse vs DateTime.Parse).
        Assert.DoesNotContain(column, descriptor.TimestampColumns);
    }
}

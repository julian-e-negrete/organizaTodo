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
}

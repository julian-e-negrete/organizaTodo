namespace OrganizaTodo.Web.Sync;

public static class SyncTableRegistry
{
    // Column lists were verified against Database/init.sql and every Database/migrate_*.sql
    // file (not just the table's original CREATE TABLE), since several tables gained columns
    // via later ALTER TABLE migrations:
    //   - CreditCardPurchases: + DueMonth, DueYear (migrate_006_credit_card_due_date.sql)
    //   - Vehicles: + OilChangeIntervalKm, OilChangeCostEstimate, LastOilChangeDate,
    //     LastOilChangeKm (migrate_008_vehicle_maintenance.sql)
    //   - OtherExpenses: + VehicleId (migrate_011_expense_vehicle_link.sql)
    //   - Savings: + LedgerTransactionId (migrate_014_link_savings_expenses_ledger.sql)
    //
    // TimestampColumns/DateOnlyColumns were verified the same way, against each column's
    // actual Postgres type (TIMESTAMP vs DATE) in its CREATE TABLE / ALTER TABLE statement —
    // not guessed from the column name. SecondaryForeignKeys covers every FK column embedded
    // in a row's own business data (as opposed to the table's own OwnershipForeignKeyColumn)
    // that references another row in a table with a direct UserId column.
    public static IReadOnlyList<SyncTableDescriptor> Tables { get; } = new List<SyncTableDescriptor>
    {
        new("HousingServices", "housingServices", "\"UserId\" = @userId",
            new[] { "UserId", "Name", "Amount", "DueDay", "Periodicity", "IsPaid", "PaidDate", "IsActive", "CreatedAt" })
        {
            TimestampColumns = ["PaidDate", "CreatedAt"],
        },

        new("CreditCardPurchases", "creditCardPurchases", "\"UserId\" = @userId",
            new[] { "UserId", "Description", "TotalAmount", "Installments", "CurrentInstallment", "InterestRate", "PurchaseDate", "IsActive", "DueMonth", "DueYear" })
        {
            TimestampColumns = ["PurchaseDate"],
        },

        new("FixedLiabilities", "fixedLiabilities", "\"UserId\" = @userId",
            new[] { "UserId", "Name", "MonthlyAmount", "DueDay", "IsActive", "IsPaid", "PaidDate", "CreatedAt" })
        {
            TimestampColumns = ["PaidDate", "CreatedAt"],
        },

        new("Income", "income", "\"UserId\" = @userId",
            new[] { "UserId", "Description", "Amount", "IncomeDate", "Category", "Month", "Year" })
        {
            TimestampColumns = ["IncomeDate"],
        },

        new("OtherExpenses", "otherExpenses", "\"UserId\" = @userId",
            new[] { "UserId", "Description", "Amount", "ExpenseDate", "Month", "Year", "VehicleId" })
        {
            TimestampColumns = ["ExpenseDate"],
            SecondaryForeignKeys = [new SyncSecondaryForeignKey("VehicleId", "Vehicles")],
        },

        new("Savings", "savings", "\"UserId\" = @userId",
            new[] { "UserId", "Amount", "Month", "Year", "Notes", "IsInitialBalance", "CreatedAt", "LedgerTransactionId" })
        {
            TimestampColumns = ["CreatedAt"],
            SecondaryForeignKeys = [new SyncSecondaryForeignKey("LedgerTransactionId", "LedgerTransactions")],
        },

        new("ShoppingLists", "shoppingLists", "\"UserId\" = @userId",
            new[] { "UserId", "Name", "Month", "Year", "CreatedAt" })
        {
            TimestampColumns = ["CreatedAt"],
        },

        new("ShoppingListItems", "shoppingListItems",
            "EXISTS (SELECT 1 FROM \"ShoppingLists\" sl WHERE sl.\"Id\" = \"ShoppingListItems\".\"ShoppingListId\" AND sl.\"UserId\" = @userId)",
            new[] { "ShoppingListId", "ProductName", "Quantity", "EstimatedPrice", "Supermarket", "Priority" },
            OwnershipForeignKeyColumn: "ShoppingListId", OwnershipParentTable: "ShoppingLists"),

        new("AssetPurchases", "assetPurchases", "\"UserId\" = @userId",
            new[] { "UserId", "ExpenseId", "SavingId", "AssetType", "Quantity", "UnitPriceArs", "ExchangeType", "TotalArs", "Notes", "PurchaseDate", "CreatedAt" })
        {
            TimestampColumns = ["CreatedAt"],
            DateOnlyColumns = ["PurchaseDate"],
            SecondaryForeignKeys =
            [
                new SyncSecondaryForeignKey("ExpenseId", "OtherExpenses"),
                new SyncSecondaryForeignKey("SavingId", "Savings"),
            ],
        },

        new("Vehicles", "vehicles", "\"UserId\" = @userId",
            new[] { "UserId", "Plate", "Make", "Model", "Year", "WeeklyKm", "FuelEfficiencyKmL", "FuelPricePerLiter", "InsuranceMonthly", "PatenteAnnual", "IsActive", "CreatedAt", "OilChangeIntervalKm", "OilChangeCostEstimate", "LastOilChangeDate", "LastOilChangeKm" })
        {
            TimestampColumns = ["CreatedAt"],
            DateOnlyColumns = ["LastOilChangeDate"],
        },

        new("VehicleMonthlyPayments", "vehicleMonthlyPayments",
            "EXISTS (SELECT 1 FROM \"Vehicles\" v WHERE v.\"Id\" = \"VehicleMonthlyPayments\".\"VehicleId\" AND v.\"UserId\" = @userId)",
            new[] { "VehicleId", "Month", "Year", "PaymentType", "PaidAt" },
            OwnershipForeignKeyColumn: "VehicleId", OwnershipParentTable: "Vehicles")
        {
            TimestampColumns = ["PaidAt"],
        },

        new("VehicleMaintenanceLogs", "vehicleMaintenanceLogs", "\"UserId\" = @userId",
            new[] { "VehicleId", "UserId", "ServiceDate", "KmAtService", "Cost", "Notes", "CreatedAt" })
        {
            TimestampColumns = ["CreatedAt"],
            DateOnlyColumns = ["ServiceDate"],
            // VehicleMaintenanceLogs has its own direct UserId, but VehicleId is a
            // separate FK that could still point at someone else's vehicle even while
            // the row's own UserId matches the caller.
            SecondaryForeignKeys = [new SyncSecondaryForeignKey("VehicleId", "Vehicles")],
        },

        new("RecurringIncomes", "recurringIncomes", "\"UserId\" = @userId",
            new[] { "UserId", "Description", "Amount", "IsActive", "CreatedAt" })
        {
            TimestampColumns = ["CreatedAt"],
        },

        new("LedgerTags", "ledgerTags", "\"UserId\" = @userId",
            new[] { "UserId", "Name", "CreatedAt" })
        {
            TimestampColumns = ["CreatedAt"],
        },

        new("LedgerTransactions", "ledgerTransactions", "\"UserId\" = @userId",
            new[] { "UserId", "Amount", "Type", "Description", "TransactionDate", "BalanceAfter", "GeneratesInterest", "InterestRate", "ParentTransactionId", "CreatedAt" })
        {
            TimestampColumns = ["TransactionDate", "CreatedAt"],
            SecondaryForeignKeys = [new SyncSecondaryForeignKey("ParentTransactionId", "LedgerTransactions")],
        },

        new("LedgerTransactionTags", "ledgerTransactionTags",
            "EXISTS (SELECT 1 FROM \"LedgerTransactions\" lt WHERE lt.\"Id\" = \"LedgerTransactionTags\".\"TransactionId\" AND lt.\"UserId\" = @userId)",
            new[] { "TransactionId", "TagId" },
            OwnershipForeignKeyColumn: "TransactionId", OwnershipParentTable: "LedgerTransactions"),
    };
}

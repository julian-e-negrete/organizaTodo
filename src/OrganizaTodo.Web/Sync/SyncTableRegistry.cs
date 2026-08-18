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
    public static IReadOnlyList<SyncTableDescriptor> Tables { get; } = new List<SyncTableDescriptor>
    {
        new("HousingServices", "housingServices", "\"UserId\" = @userId",
            new[] { "UserId", "Name", "Amount", "DueDay", "Periodicity", "IsPaid", "PaidDate", "IsActive", "CreatedAt" }),

        new("CreditCardPurchases", "creditCardPurchases", "\"UserId\" = @userId",
            new[] { "UserId", "Description", "TotalAmount", "Installments", "CurrentInstallment", "InterestRate", "PurchaseDate", "IsActive", "DueMonth", "DueYear" }),

        new("FixedLiabilities", "fixedLiabilities", "\"UserId\" = @userId",
            new[] { "UserId", "Name", "MonthlyAmount", "DueDay", "IsActive", "IsPaid", "PaidDate", "CreatedAt" }),

        new("Income", "income", "\"UserId\" = @userId",
            new[] { "UserId", "Description", "Amount", "IncomeDate", "Category", "Month", "Year" }),

        new("OtherExpenses", "otherExpenses", "\"UserId\" = @userId",
            new[] { "UserId", "Description", "Amount", "ExpenseDate", "Month", "Year", "VehicleId" }),

        new("Savings", "savings", "\"UserId\" = @userId",
            new[] { "UserId", "Amount", "Month", "Year", "Notes", "IsInitialBalance", "CreatedAt", "LedgerTransactionId" }),

        new("ShoppingLists", "shoppingLists", "\"UserId\" = @userId",
            new[] { "UserId", "Name", "Month", "Year", "CreatedAt" }),

        new("ShoppingListItems", "shoppingListItems",
            "EXISTS (SELECT 1 FROM \"ShoppingLists\" sl WHERE sl.\"Id\" = \"ShoppingListItems\".\"ShoppingListId\" AND sl.\"UserId\" = @userId)",
            new[] { "ShoppingListId", "ProductName", "Quantity", "EstimatedPrice", "Supermarket", "Priority" },
            OwnershipForeignKeyColumn: "ShoppingListId", OwnershipParentTable: "ShoppingLists"),

        new("AssetPurchases", "assetPurchases", "\"UserId\" = @userId",
            new[] { "UserId", "ExpenseId", "SavingId", "AssetType", "Quantity", "UnitPriceArs", "ExchangeType", "TotalArs", "Notes", "PurchaseDate", "CreatedAt" }),

        new("Vehicles", "vehicles", "\"UserId\" = @userId",
            new[] { "UserId", "Plate", "Make", "Model", "Year", "WeeklyKm", "FuelEfficiencyKmL", "FuelPricePerLiter", "InsuranceMonthly", "PatenteAnnual", "IsActive", "CreatedAt", "OilChangeIntervalKm", "OilChangeCostEstimate", "LastOilChangeDate", "LastOilChangeKm" }),

        new("VehicleMonthlyPayments", "vehicleMonthlyPayments",
            "EXISTS (SELECT 1 FROM \"Vehicles\" v WHERE v.\"Id\" = \"VehicleMonthlyPayments\".\"VehicleId\" AND v.\"UserId\" = @userId)",
            new[] { "VehicleId", "Month", "Year", "PaymentType", "PaidAt" },
            OwnershipForeignKeyColumn: "VehicleId", OwnershipParentTable: "Vehicles"),

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
            new[] { "TransactionId", "TagId" },
            OwnershipForeignKeyColumn: "TransactionId", OwnershipParentTable: "LedgerTransactions"),
    };
}

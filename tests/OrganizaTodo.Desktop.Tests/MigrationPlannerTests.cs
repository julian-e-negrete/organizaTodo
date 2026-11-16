using OrganizaTodo.Desktop.Lifecycle;

namespace OrganizaTodo.Desktop.Tests;

public class MigrationPlannerTests
{
    [Fact]
    public void PendingMainMigrations_ReturnsUnappliedFilesInNumericOrder()
    {
        var dir = Directory.CreateTempSubdirectory("organizatodo-planner-test-").FullName;
        try
        {
            File.WriteAllText(Path.Combine(dir, "migrate_002_savings.sql"), "-- noop");
            File.WriteAllText(Path.Combine(dir, "migrate_001_fixed_liabilities_paid.sql"), "-- noop");
            File.WriteAllText(Path.Combine(dir, "migrate_010_recurring_income.sql"), "-- noop");
            File.WriteAllText(Path.Combine(dir, "migrate_shopping_001_product_functions.sql"), "-- noop");

            var pending = MigrationPlanner.PendingMainMigrations(dir, new HashSet<string> { "migrate_001_fixed_liabilities_paid.sql" });

            Assert.Equal(
                new[] { "migrate_002_savings.sql", "migrate_010_recurring_income.sql" },
                pending);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void PendingShoppingMigrations_OnlyMatchesShoppingPrefixedFiles()
    {
        var dir = Directory.CreateTempSubdirectory("organizatodo-planner-test-").FullName;
        try
        {
            File.WriteAllText(Path.Combine(dir, "migrate_001_fixed_liabilities_paid.sql"), "-- noop");
            File.WriteAllText(Path.Combine(dir, "migrate_shopping_002_fix_function_types.sql"), "-- noop");
            File.WriteAllText(Path.Combine(dir, "migrate_shopping_001_product_functions.sql"), "-- noop");

            var pending = MigrationPlanner.PendingShoppingMigrations(dir, new HashSet<string>());

            Assert.Equal(
                new[] { "migrate_shopping_001_product_functions.sql", "migrate_shopping_002_fix_function_types.sql" },
                pending);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}

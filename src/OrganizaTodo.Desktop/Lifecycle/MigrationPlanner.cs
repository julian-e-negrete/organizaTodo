using System.Text.RegularExpressions;

namespace OrganizaTodo.Desktop.Lifecycle;

public static partial class MigrationPlanner
{
    [GeneratedRegex(@"^migrate_(\d+)_.*\.sql$")]
    private static partial Regex MainMigrationPattern();

    [GeneratedRegex(@"^migrate_shopping_(\d+)_.*\.sql$")]
    private static partial Regex ShoppingMigrationPattern();

    public static IReadOnlyList<string> PendingMainMigrations(string databaseDir, IReadOnlySet<string> alreadyApplied) =>
        PendingMatching(databaseDir, MainMigrationPattern(), alreadyApplied);

    public static IReadOnlyList<string> PendingShoppingMigrations(string databaseDir, IReadOnlySet<string> alreadyApplied) =>
        PendingMatching(databaseDir, ShoppingMigrationPattern(), alreadyApplied);

    private static IReadOnlyList<string> PendingMatching(string dir, Regex pattern, IReadOnlySet<string> alreadyApplied)
    {
        return Directory.GetFiles(dir)
            .Select(Path.GetFileName)
            .Where(name => name is not null && pattern.IsMatch(name) && !alreadyApplied.Contains(name))
            .Select(name => name!)
            .OrderBy(name => int.Parse(pattern.Match(name).Groups[1].Value))
            .ToList();
    }
}

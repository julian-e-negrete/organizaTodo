using Dapper;
using Npgsql;

namespace OrganizaTodo.Desktop.Lifecycle;

public interface ISchemaBootstrapper
{
    Task BootstrapAsync(NpgsqlConnection adminConnection, string targetDatabase, bool isShoppingDatabase, CancellationToken ct = default);
}

public sealed class SchemaBootstrapper(string databaseSqlDir) : ISchemaBootstrapper
{
    public async Task BootstrapAsync(NpgsqlConnection adminConnection, string targetDatabase, bool isShoppingDatabase, CancellationToken ct = default)
    {
        await EnsureDatabaseExistsAsync(adminConnection, targetDatabase);

        var targetConnectionString = new NpgsqlConnectionStringBuilder(adminConnection.ConnectionString)
        {
            Database = targetDatabase
        }.ConnectionString;

        // adminConnection.ConnectionString omits the password once the connection has been
        // opened (Npgsql strips it unless Persist Security Info=true). CloneWith reuses the
        // original connection's security info (password, SSL callbacks) while swapping in the
        // new connection-string parameters (here, just the database name), so the clone can
        // still authenticate.
        await using var dbConnection = adminConnection.CloneWith(targetConnectionString);
        await dbConnection.OpenAsync(ct);

        await EnsureMigrationsTableAsync(dbConnection);
        var applied = await LoadAppliedMigrationsAsync(dbConnection);

        if (!applied.Contains("init.sql"))
        {
            await ApplyFileAsync(dbConnection, Path.Combine(databaseSqlDir, "init.sql"));
            await RecordAppliedAsync(dbConnection, "init.sql");
        }

        var pending = isShoppingDatabase
            ? MigrationPlanner.PendingShoppingMigrations(databaseSqlDir, applied)
            : MigrationPlanner.PendingMainMigrations(databaseSqlDir, applied);

        foreach (var file in pending)
        {
            await ApplyFileAsync(dbConnection, Path.Combine(databaseSqlDir, file));
            await RecordAppliedAsync(dbConnection, file);
        }
    }

    private static async Task EnsureDatabaseExistsAsync(NpgsqlConnection adminConnection, string database)
    {
        var exists = await adminConnection.ExecuteScalarAsync<bool>(
            "SELECT EXISTS (SELECT 1 FROM pg_database WHERE datname = @database)",
            new { database });
        if (!exists)
        {
            await adminConnection.ExecuteAsync($"CREATE DATABASE \"{database}\"");
        }
    }

    private static Task EnsureMigrationsTableAsync(NpgsqlConnection connection) =>
        connection.ExecuteAsync(
            "CREATE TABLE IF NOT EXISTS schema_migrations (filename TEXT PRIMARY KEY, applied_at TIMESTAMPTZ NOT NULL DEFAULT now())");

    private static async Task<HashSet<string>> LoadAppliedMigrationsAsync(NpgsqlConnection connection)
    {
        var rows = await connection.QueryAsync<string>("SELECT filename FROM schema_migrations");
        return rows.ToHashSet();
    }

    private static async Task ApplyFileAsync(NpgsqlConnection connection, string path)
    {
        var sql = await File.ReadAllTextAsync(path);
        await connection.ExecuteAsync(sql);
    }

    private static Task RecordAppliedAsync(NpgsqlConnection connection, string filename) =>
        connection.ExecuteAsync("INSERT INTO schema_migrations (filename) VALUES (@filename)", new { filename });
}

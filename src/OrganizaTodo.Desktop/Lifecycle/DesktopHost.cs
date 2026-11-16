using Npgsql;

namespace OrganizaTodo.Desktop.Lifecycle;

public sealed class DesktopHost(
    IPostgresManager postgresManager,
    ISchemaBootstrapper schemaBootstrapper,
    IWebAppRunner webAppRunner,
    int postgresPort,
    DbCredentials credentials,
    string defaultDatabase,
    string shoppingDatabase)
{
    public async Task<string> StartAsync(CancellationToken ct = default)
    {
        await postgresManager.InitializeIfNeededAsync(ct);

        try
        {
            await postgresManager.StartAsync(ct);
            await postgresManager.WaitUntilReadyAsync(TimeSpan.FromSeconds(30), ct);

            // The admin connection is intentionally left unopened here: ISchemaBootstrapper
            // implementations use Dapper, which opens (and closes) a closed connection
            // automatically per call, so an explicit OpenAsync isn't required for the real
            // implementation and would otherwise force a live Postgres connection inside this
            // orchestration method, making it unnecessarily hard to unit test with fakes.
            var adminConnectionString = PostgresConnectionStringBuilder.Build(postgresPort, "postgres", credentials.Username, credentials.Password);
            await using (var adminConnection = new NpgsqlConnection(adminConnectionString))
            {
                await schemaBootstrapper.BootstrapAsync(adminConnection, defaultDatabase, isShoppingDatabase: false, ct);
                await schemaBootstrapper.BootstrapAsync(adminConnection, shoppingDatabase, isShoppingDatabase: true, ct);
            }

            var defaultConnectionString = PostgresConnectionStringBuilder.Build(postgresPort, defaultDatabase, credentials.Username, credentials.Password);
            var shoppingConnectionString = PostgresConnectionStringBuilder.Build(postgresPort, shoppingDatabase, credentials.Username, credentials.Password);

            return await webAppRunner.StartAsync(defaultConnectionString, shoppingConnectionString, ct);
        }
        catch
        {
            // webAppRunner.StartAsync may have already started Kestrel internally before
            // throwing (e.g. it faulted after ApplicationStarted). Stop it best-effort so
            // Kestrel isn't left running once Postgres is torn down; never let a failure
            // here mask the original exception or prevent Postgres cleanup from running.
            try { await webAppRunner.StopAsync(ct); } catch { /* best-effort cleanup */ }
            await postgresManager.StopAsync(ct);
            throw;
        }
    }

    public async Task ShutdownAsync(CancellationToken ct = default)
    {
        await webAppRunner.StopAsync(ct);
        await postgresManager.StopAsync(ct);
    }
}

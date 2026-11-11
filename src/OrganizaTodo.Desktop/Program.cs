using OrganizaTodo.Desktop.Lifecycle;
using Photino.NET;

using var guard = new SingleInstanceGuard("OrganizaTodo.Desktop.SingleInstance");
if (!guard.TryAcquire())
{
    Console.Error.WriteLine("OrganizaTodo is already running.");
    return;
}

var paths = AppPaths.From(AppDataRoot.ResolveForCurrentOS());
paths.EnsureDirectoriesExist();

var webAppRunner = new KestrelWebAppRunner();
Func<Task> shutdown;
string url;

var remote = RemoteDbConfig.TryLoad(paths.ConfigDir);
if (remote is not null)
{
    // Remote mode: no bundled Postgres, no schema bootstrap — the server owns its schema.
    try
    {
        url = await webAppRunner.StartAsync(remote.DefaultConnection, remote.ShoppingConnection);
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Failed to start OrganizaTodo (remote database): {ex}");
        return;
    }
    shutdown = () => webAppRunner.StopAsync();
}
else
{
    var credentials = new DbCredentialStore(paths.ConfigDir).LoadOrCreate();
    var pgPort = PortAllocator.FindAvailablePort(preferredPort: 55432);
    var pgBinDirectory = ResolvePgBinDirectory();

    var postgresManager = new PostgresManager(pgBinDirectory, paths.PgDataDir, pgPort, credentials, paths.LogsDir);
    var schemaBootstrapper = new SchemaBootstrapper(Path.Combine(AppContext.BaseDirectory, "Database"));
    var host = new DesktopHost(postgresManager, schemaBootstrapper, webAppRunner, pgPort, credentials, "organizatodo", "organizatodo_shopping");

    try
    {
        url = await host.StartAsync();
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Failed to start OrganizaTodo: {ex}");
        return;
    }
    shutdown = () => host.ShutdownAsync();
}

var window = new PhotinoWindow()
    .SetTitle("OrganizaTodo")
    .SetUseOsDefaultSize(false)
    .SetSize(1280, 800)
    .Load(url);

window.WindowClosing += (sender, args) =>
{
    shutdown().GetAwaiter().GetResult();
    return false;
};

window.WaitForClose();

static string ResolvePgBinDirectory()
{
    var overridePath = Environment.GetEnvironmentVariable("ORGANIZATODO_PG_BIN_DIR");
    if (!string.IsNullOrWhiteSpace(overridePath)) return overridePath;
    return Path.Combine(AppContext.BaseDirectory, "pgbin", "bin");
}

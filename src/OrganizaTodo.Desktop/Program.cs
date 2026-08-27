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

var credentials = new DbCredentialStore(paths.ConfigDir).LoadOrCreate();
var pgPort = PortAllocator.FindAvailablePort(preferredPort: 55432);
var pgBinDirectory = ResolvePgBinDirectory();

var postgresManager = new PostgresManager(pgBinDirectory, paths.PgDataDir, pgPort, credentials, paths.LogsDir);
var schemaBootstrapper = new SchemaBootstrapper(Path.Combine(AppContext.BaseDirectory, "Database"));
var webAppRunner = new KestrelWebAppRunner();
var host = new DesktopHost(postgresManager, schemaBootstrapper, webAppRunner, pgPort, credentials, "organizatodo", "organizatodo_shopping");

string url;
try
{
    url = await host.StartAsync();
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Failed to start OrganizaTodo: {ex}");
    return;
}

var window = new PhotinoWindow()
    .SetTitle("OrganizaTodo")
    .SetUseOsDefaultSize(false)
    .SetSize(1280, 800)
    .Load(url);

window.WindowClosing += (sender, args) =>
{
    host.ShutdownAsync().GetAwaiter().GetResult();
    return false;
};

window.WaitForClose();

static string ResolvePgBinDirectory()
{
    var overridePath = Environment.GetEnvironmentVariable("ORGANIZATODO_PG_BIN_DIR");
    if (!string.IsNullOrWhiteSpace(overridePath)) return overridePath;

    // The vendored Linux tree under pgbin/ preserves the original Debian package
    // nesting (usr/lib/postgresql/16/bin) rather than a flattened pgbin/bin — Debian's
    // postgres binaries have fixed bindir/datadir paths baked in and only tolerate
    // relocation when that nesting is kept intact (see scripts/vendor-postgres-linux.sh
    // for the functional-test evidence). The vendored Windows tree (EDB's zip) already
    // ships a clean bin/lib/share layout, so it keeps the simple pgbin/bin path.
    return OperatingSystem.IsWindows()
        ? Path.Combine(AppContext.BaseDirectory, "pgbin", "bin")
        : Path.Combine(AppContext.BaseDirectory, "pgbin", "usr", "lib", "postgresql", "16", "bin");
}

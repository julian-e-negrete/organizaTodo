using System.Runtime.InteropServices;
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

// Points Photino's Windows host at the Fixed Version WebView2 runtime bundled
// alongside the app (see OrganizaTodo.Desktop.csproj's win-x64-only
// WebView2.Runtime.X64 PackageReference, which copies a "WebView2" folder next
// to the published exe). Without this call, Photino falls back to the
// system-wide Evergreen WebView2 Runtime, which defeats the point of bundling
// it for an offline installer. Win32SetWebView2Path no-ops on non-Windows, so
// this is safe to call unconditionally.
var webView2Path = Path.Combine(AppContext.BaseDirectory, "WebView2");
var window = new PhotinoWindow()
    .Win32SetWebView2Path(webView2Path)
    .SetTitle("OrganizaTodo")
    .SetUseOsDefaultSize(false)
    .SetSize(1280, 800)
    .Load(url);

var shutdownGuard = 0;
async Task ShutdownOnceAsync()
{
    if (Interlocked.Exchange(ref shutdownGuard, 1) != 0) return;
    await host.ShutdownAsync();
}

window.WindowClosing += (sender, args) =>
{
    ShutdownOnceAsync().GetAwaiter().GetResult();
    return false;
};

// Closing the native window is the only path wired to a clean shutdown above.
// A direct process termination (SIGTERM from `kill`, desktop-session logoff,
// a task manager "End Task") bypasses WindowClosing entirely and previously
// left Postgres running forever, orphaned, holding the data directory locked
// until force-killed (reproduced for real in Task 6's end-to-end
// verification). PosixSignalRegistration also covers Windows: SIGTERM there
// maps to the console being closed / logoff / system shutdown.
using var sigtermRegistration = PosixSignalRegistration.Create(PosixSignal.SIGTERM, context =>
{
    context.Cancel = true;
    ShutdownOnceAsync().GetAwaiter().GetResult();
    Environment.Exit(0);
});
using var sigintRegistration = PosixSignalRegistration.Create(PosixSignal.SIGINT, context =>
{
    context.Cancel = true;
    ShutdownOnceAsync().GetAwaiter().GetResult();
    Environment.Exit(0);
});

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

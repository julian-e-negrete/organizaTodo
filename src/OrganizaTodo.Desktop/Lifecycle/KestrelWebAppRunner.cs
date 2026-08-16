using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

namespace OrganizaTodo.Desktop.Lifecycle;

public sealed class KestrelWebAppRunner : IWebAppRunner
{
    private WebApplication? _app;
    private Task? _runTask;

    public async Task<string> StartAsync(string defaultConnectionString, string shoppingConnectionString, CancellationToken ct = default)
    {
        var port = PortAllocator.FindAvailablePort(preferredPort: 5157);

        // WebApplication.CreateBuilder(args) — called inside BuildApp — defaults
        // ContentRootPath (and therefore WebRootPath, i.e. where wwwroot is resolved
        // from) to Directory.GetCurrentDirectory() at that moment. WebApplicationBuilder
        // then throws NotSupportedException if content root is changed afterward via
        // builder.WebHost, so it must be pinned before BuildApp runs. A desktop app can
        // be launched with an arbitrary working directory (shortcut, launcher, PATH
        // invocation), so pin the process's current directory to this executable's own
        // base directory — where the .csproj copies OrganizaTodo.Web's wwwroot alongside
        // this executable — regardless of the caller's CWD.
        Directory.SetCurrentDirectory(AppContext.BaseDirectory);

        _app = OrganizaTodo.Web.Program.BuildApp(Array.Empty<string>(), builder =>
        {
            builder.Configuration["ConnectionStrings:DefaultConnection"] = defaultConnectionString;
            builder.Configuration["ConnectionStrings:ShoppingConnection"] = shoppingConnectionString;
            builder.Configuration["Desktop:IsHosted"] = "true";
            builder.WebHost.UseUrls($"http://127.0.0.1:{port}");
        });

        var started = new TaskCompletionSource();
        _app.Lifetime.ApplicationStarted.Register(() => started.TrySetResult());

        // WebApplication declares its own RunAsync(string? url) instance method, which
        // shadows the IHost.RunAsync(CancellationToken) extension method for overload
        // resolution purposes. Casting to IHost restores access to the cancellable overload.
        _runTask = ((IHost)_app).RunAsync(ct);

        // Race ApplicationStarted against the run task itself: if the host faults (or
        // exits) before starting, _runTask completes first and awaiting it below surfaces
        // the real exception immediately instead of masking it behind a 30s timeout. The
        // outer WaitAsync remains the fallback for a genuinely hung startup, where neither
        // task completes in time.
        var completed = await Task.WhenAny(started.Task, _runTask).WaitAsync(TimeSpan.FromSeconds(30), ct);
        if (completed == _runTask)
        {
            // Observe the fault so it never becomes an unobserved task exception, and
            // rethrow it (via await) so the caller sees the real startup failure.
            await _runTask;
        }

        if (!started.Task.IsCompleted)
        {
            throw new TimeoutException("Kestrel did not signal ApplicationStarted within the timeout.");
        }

        return $"http://127.0.0.1:{port}";
    }

    public async Task StopAsync(CancellationToken ct = default)
    {
        if (_app is null) return;
        await _app.StopAsync(ct);
        if (_runTask is not null) await _runTask;
    }
}

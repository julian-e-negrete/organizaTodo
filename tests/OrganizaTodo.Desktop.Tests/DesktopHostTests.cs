using Npgsql;
using OrganizaTodo.Desktop.Lifecycle;

namespace OrganizaTodo.Desktop.Tests;

public class DesktopHostTests
{
    private sealed class FakePostgresManager : IPostgresManager
    {
        public List<string> Calls { get; } = [];
        public Task InitializeIfNeededAsync(CancellationToken ct = default) { Calls.Add("Initialize"); return Task.CompletedTask; }
        public Task StartAsync(CancellationToken ct = default) { Calls.Add("Start"); return Task.CompletedTask; }
        public Task WaitUntilReadyAsync(TimeSpan timeout, CancellationToken ct = default) { Calls.Add("WaitUntilReady"); return Task.CompletedTask; }
        public Task StopAsync(CancellationToken ct = default) { Calls.Add("Stop"); return Task.CompletedTask; }
    }

    private sealed class FakeSchemaBootstrapper : ISchemaBootstrapper
    {
        public List<string> BootstrappedDatabases { get; } = [];
        public Task BootstrapAsync(NpgsqlConnection adminConnection, string targetDatabase, bool isShoppingDatabase, CancellationToken ct = default)
        {
            BootstrappedDatabases.Add(targetDatabase);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeWebAppRunner(bool throwOnStart = false) : IWebAppRunner
    {
        public bool Started { get; private set; }
        public bool Stopped { get; private set; }

        public Task<string> StartAsync(string defaultConnectionString, string shoppingConnectionString, CancellationToken ct = default)
        {
            if (throwOnStart) throw new InvalidOperationException("simulated Kestrel bind failure");
            Started = true;
            return Task.FromResult("http://127.0.0.1:5157");
        }

        public Task StopAsync(CancellationToken ct = default) { Stopped = true; return Task.CompletedTask; }
    }

    private static DbCredentials TestCredentials => new("organizatodo_app", "test-password");

    [Fact]
    public async Task StartAsync_HappyPath_RunsStepsInOrderAndReturnsUrl()
    {
        var postgres = new FakePostgresManager();
        var schema = new FakeSchemaBootstrapper();
        var webApp = new FakeWebAppRunner();
        var host = new DesktopHost(postgres, schema, webApp, 55432, TestCredentials, "organizatodo", "organizatodo_shopping");

        var url = await host.StartAsync();

        Assert.Equal("http://127.0.0.1:5157", url);
        Assert.Equal(["Initialize", "Start", "WaitUntilReady"], postgres.Calls);
        Assert.Equal(["organizatodo", "organizatodo_shopping"], schema.BootstrappedDatabases);
        Assert.True(webApp.Started);
    }

    [Fact]
    public async Task StartAsync_WebAppFailsToStart_StopsPostgresBeforeRethrowing()
    {
        var postgres = new FakePostgresManager();
        var schema = new FakeSchemaBootstrapper();
        var webApp = new FakeWebAppRunner(throwOnStart: true);
        var host = new DesktopHost(postgres, schema, webApp, 55432, TestCredentials, "organizatodo", "organizatodo_shopping");

        await Assert.ThrowsAsync<InvalidOperationException>(() => host.StartAsync());

        Assert.Contains("Stop", postgres.Calls);
        Assert.True(webApp.Stopped);
    }

    [Fact]
    public async Task ShutdownAsync_StopsWebAppThenPostgres()
    {
        var postgres = new FakePostgresManager();
        var schema = new FakeSchemaBootstrapper();
        var webApp = new FakeWebAppRunner();
        var host = new DesktopHost(postgres, schema, webApp, 55432, TestCredentials, "organizatodo", "organizatodo_shopping");
        await host.StartAsync();
        postgres.Calls.Clear();

        await host.ShutdownAsync();

        Assert.True(webApp.Stopped);
        Assert.Equal(["Stop"], postgres.Calls);
    }
}

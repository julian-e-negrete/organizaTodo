using Npgsql;
using OrganizaTodo.Desktop.Lifecycle;

namespace OrganizaTodo.Desktop.Tests;

public class KestrelWebAppRunnerIntegrationTests : IAsyncLifetime
{
    private string _tempDir = "";
    private PostgresManager _postgres = null!;
    private int _pgPort;
    private DbCredentials _credentials = null!;

    public async Task InitializeAsync()
    {
        var pgBinDirectory = PostgresTestEnvironment.ResolvePgBinDirectory();
        _tempDir = Directory.CreateTempSubdirectory("organizatodo-e2e-test-").FullName;
        var logsDir = Path.Combine(_tempDir, "logs");
        Directory.CreateDirectory(logsDir);
        _pgPort = PortAllocator.FindAvailablePort();
        _credentials = new DbCredentials("organizatodo_app", "test-password-123");

        _postgres = new PostgresManager(pgBinDirectory, Path.Combine(_tempDir, "pgdata"), _pgPort, _credentials, logsDir);
        await _postgres.InitializeIfNeededAsync();
        await _postgres.StartAsync();
        await _postgres.WaitUntilReadyAsync(TimeSpan.FromSeconds(30));

        var sqlDir = Path.Combine(AppContext.BaseDirectory, "Database");
        var bootstrapper = new SchemaBootstrapper(sqlDir);
        await using var adminConnection = new NpgsqlConnection(
            PostgresConnectionStringBuilder.Build(_pgPort, "postgres", _credentials.Username, _credentials.Password));
        await adminConnection.OpenAsync();
        await bootstrapper.BootstrapAsync(adminConnection, "organizatodo", isShoppingDatabase: false);
        await bootstrapper.BootstrapAsync(adminConnection, "organizatodo_shopping", isShoppingDatabase: true);
    }

    public async Task DisposeAsync()
    {
        await _postgres.StopAsync();
        Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public async Task StartAsync_ServesLoginPageOverHttp_ThenStopsCleanly()
    {
        var defaultConnectionString = PostgresConnectionStringBuilder.Build(_pgPort, "organizatodo", _credentials.Username, _credentials.Password);
        var shoppingConnectionString = PostgresConnectionStringBuilder.Build(_pgPort, "organizatodo_shopping", _credentials.Username, _credentials.Password);

        var runner = new KestrelWebAppRunner();
        var url = await runner.StartAsync(defaultConnectionString, shoppingConnectionString);

        using var httpClient = new HttpClient();
        var response = await httpClient.GetAsync($"{url}/Account/Login");
        Assert.True(response.IsSuccessStatusCode);

        await runner.StopAsync();
    }
}

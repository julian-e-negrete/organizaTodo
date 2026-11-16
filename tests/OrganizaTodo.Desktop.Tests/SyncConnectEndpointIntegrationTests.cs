using System.Net;
using System.Net.Http.Json;
using Dapper;
using Npgsql;
using OrganizaTodo.Desktop.Lifecycle;
using OrganizaTodo.Web.Models;

namespace OrganizaTodo.Desktop.Tests;

public class SyncConnectEndpointIntegrationTests : IAsyncLifetime
{
    private string _pgBinDirectory = "";
    private string _tempDir = "";
    private PostgresManager _manager = null!;
    private int _pgPort;
    private DbCredentials _credentials = null!;
    private OrganizaTodo.Desktop.Lifecycle.KestrelWebAppRunner _runner = null!;
    private string _baseUrl = "";

    public async Task InitializeAsync()
    {
        _pgBinDirectory = PostgresTestEnvironment.ResolvePgBinDirectory();
        _tempDir = Directory.CreateTempSubdirectory("organizatodo-syncconnect-test-").FullName;
        var logsDir = Path.Combine(_tempDir, "logs");
        Directory.CreateDirectory(logsDir);
        _pgPort = PortAllocator.FindAvailablePort();
        _credentials = new DbCredentials("organizatodo_app", "test-password-123");

        _manager = new PostgresManager(_pgBinDirectory, Path.Combine(_tempDir, "pgdata"), _pgPort, _credentials, logsDir);
        await _manager.InitializeIfNeededAsync();
        await _manager.StartAsync();
        await _manager.WaitUntilReadyAsync(TimeSpan.FromSeconds(30));

        var adminConnectionString = PostgresConnectionStringBuilder.Build(_pgPort, "postgres", _credentials.Username, _credentials.Password);
        await using var adminConnection = new NpgsqlConnection(adminConnectionString);
        await adminConnection.OpenAsync();
        var bootstrapper = new SchemaBootstrapper(Path.Combine(AppContext.BaseDirectory, "Database"));
        await bootstrapper.BootstrapAsync(adminConnection, "organizatodo", isShoppingDatabase: false);
        await bootstrapper.BootstrapAsync(adminConnection, "organizatodo_shopping", isShoppingDatabase: true);

        // Seed a real user via the app's own signup path (sp_users_create + a real BCrypt hash)
        // rather than hand-rolling a PasswordHash here.
        await using (var conn = new NpgsqlConnection(PostgresConnectionStringBuilder.Build(_pgPort, "organizatodo", _credentials.Username, _credentials.Password)))
        {
            await conn.OpenAsync();
            var passwordHash = BCrypt.Net.BCrypt.HashPassword("correct-password");
            await conn.ExecuteAsync(
                "SELECT sp_users_create(@email, @passwordHash, 'User')",
                new { email = "sync-test@t.com", passwordHash });
        }

        _runner = new OrganizaTodo.Desktop.Lifecycle.KestrelWebAppRunner();
        _baseUrl = await _runner.StartAsync(
            PostgresConnectionStringBuilder.Build(_pgPort, "organizatodo", _credentials.Username, _credentials.Password),
            PostgresConnectionStringBuilder.Build(_pgPort, "organizatodo_shopping", _credentials.Username, _credentials.Password));
    }

    public async Task DisposeAsync()
    {
        await _runner.StopAsync();
        await _manager.StopAsync();
        Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public async Task Connect_WithCorrectCredentials_ReturnsToken()
    {
        using var client = new HttpClient();

        var response = await client.PostAsJsonAsync($"{_baseUrl}/api/sync/connect",
            new ConnectRequest("sync-test@t.com", "correct-password"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ConnectResponse>();
        Assert.NotNull(body);
        Assert.False(string.IsNullOrWhiteSpace(body!.Token));
        Assert.True(body.UserId > 0);
    }

    [Fact]
    public async Task Connect_WithWrongPassword_ReturnsUnauthorized()
    {
        using var client = new HttpClient();

        var response = await client.PostAsJsonAsync($"{_baseUrl}/api/sync/connect",
            new ConnectRequest("sync-test@t.com", "wrong-password"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}

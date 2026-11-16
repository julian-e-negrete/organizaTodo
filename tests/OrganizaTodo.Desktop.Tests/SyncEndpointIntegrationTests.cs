using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dapper;
using Npgsql;
using OrganizaTodo.Desktop.Lifecycle;
using OrganizaTodo.Web.Models;

namespace OrganizaTodo.Desktop.Tests;

public class SyncEndpointIntegrationTests : IAsyncLifetime
{
    // Deserializing SyncResponse.Changes (Dictionary<string, IReadOnlyList<dynamic>>) with the
    // default HttpClient JSON options produces boxed System.Text.Json.JsonElement instances for
    // the dynamic-typed rows, and JsonElement has no string indexer (only an int indexer for
    // array elements) — a dynamic `row["Name"]` lookup against it throws a RuntimeBinderException
    // at runtime, not a compile error. Opting into JsonUnknownTypeHandling.JsonNode instead
    // produces JsonObject/JsonNode instances, which do support `row["Name"]` and explicit casts
    // like `(string)row["Name"]`, matching how the assertion below is written.
    private static readonly JsonSerializerOptions ResponseJsonOptions = new(JsonSerializerDefaults.Web)
    {
        UnknownTypeHandling = JsonUnknownTypeHandling.JsonNode
    };

    private string _pgBinDirectory = "";
    private string _tempDir = "";
    private PostgresManager _manager = null!;
    private int _pgPort;
    private DbCredentials _credentials = null!;
    private OrganizaTodo.Desktop.Lifecycle.KestrelWebAppRunner _runner = null!;
    private string _baseUrl = "";
    private string _connectionString = "";
    private int _userId;

    public async Task InitializeAsync()
    {
        _pgBinDirectory = PostgresTestEnvironment.ResolvePgBinDirectory();
        _tempDir = Directory.CreateTempSubdirectory("organizatodo-syncendpoint-test-").FullName;
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

        _connectionString = PostgresConnectionStringBuilder.Build(_pgPort, "organizatodo", _credentials.Username, _credentials.Password);

        _runner = new OrganizaTodo.Desktop.Lifecycle.KestrelWebAppRunner();
        _baseUrl = await _runner.StartAsync(
            _connectionString,
            PostgresConnectionStringBuilder.Build(_pgPort, "organizatodo_shopping", _credentials.Username, _credentials.Password));

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        var passwordHash = BCrypt.Net.BCrypt.HashPassword("correct-password");
        _userId = await conn.ExecuteScalarAsync<int>(
            "SELECT sp_users_create(@email, @passwordHash, 'User')",
            new { email = "sync-e2e@t.com", passwordHash });
    }

    public async Task DisposeAsync()
    {
        await _runner.StopAsync();
        await _manager.StopAsync();
        Directory.Delete(_tempDir, recursive: true);
    }

    private async Task<string> ConnectAndGetTokenAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync($"{_baseUrl}/api/sync/connect",
            new ConnectRequest("sync-e2e@t.com", "correct-password"));
        var body = await response.Content.ReadFromJsonAsync<ConnectResponse>();
        return body!.Token;
    }

    [Fact]
    public async Task Sync_WithoutToken_ReturnsUnauthorized()
    {
        using var client = new HttpClient();

        var response = await client.PostAsJsonAsync($"{_baseUrl}/api/sync",
            new SyncRequest(DateTimeOffset.MinValue, new Dictionary<string, List<SyncRowChangeDto>>()));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Sync_PushThenPull_RoundTripsAChange()
    {
        using var client = new HttpClient();
        var token = await ConnectAndGetTokenAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var syncId = Guid.NewGuid();
        var request = new SyncRequest(
            DateTimeOffset.MinValue,
            new Dictionary<string, List<SyncRowChangeDto>>
            {
                ["housingServices"] = new List<SyncRowChangeDto>
                {
                    new(syncId, DateTimeOffset.UtcNow, false, new Dictionary<string, object?>
                    {
                        ["UserId"] = _userId, ["Name"] = "RoundTrip", ["Amount"] = 42m,
                        ["DueDay"] = 1, ["Periodicity"] = "MONTHLY", ["IsPaid"] = false, ["IsActive"] = true
                    })
                }
            });

        var response = await client.PostAsJsonAsync($"{_baseUrl}/api/sync", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<SyncResponse>(ResponseJsonOptions);
        Assert.NotNull(body);
        Assert.Contains(body!.Changes["housingServices"], row => (string)row["Name"] == "RoundTrip");
    }

    // Finding 3: a rejected push used to fall through to app.UseExceptionHandler("/Home/Error")
    // (an MVC HTML error page) since SyncPushService's ownership rejections are plain
    // InvalidOperationExceptions. This proves the real HTTP pipeline — not just the controller's
    // own return value — actually gives back JSON (Content-Type application/problem+json), not
    // an HTML error page, for a genuine ownership rejection.
    [Fact]
    public async Task Sync_PushWithOwnershipViolation_Returns409ConflictAsJson_NotHtml()
    {
        using var client = new HttpClient();
        var token = await ConnectAndGetTokenAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var request = new SyncRequest(
            DateTimeOffset.MinValue,
            new Dictionary<string, List<SyncRowChangeDto>>
            {
                ["housingServices"] = new List<SyncRowChangeDto>
                {
                    new(Guid.NewGuid(), DateTimeOffset.UtcNow, false, new Dictionary<string, object?>
                    {
                        // A UserId that isn't the authenticated caller's — the push service's
                        // Check 2 rejects this with InvalidOperationException.
                        ["UserId"] = _userId + 999_999, ["Name"] = "ShouldBeRejected", ["Amount"] = 1m,
                        ["DueDay"] = 1, ["Periodicity"] = "MONTHLY", ["IsPaid"] = false, ["IsActive"] = true
                    })
                }
            });

        var response = await client.PostAsJsonAsync($"{_baseUrl}/api/sync", request);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }
}

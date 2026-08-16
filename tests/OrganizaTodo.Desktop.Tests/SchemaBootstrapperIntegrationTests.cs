using Dapper;
using Npgsql;
using OrganizaTodo.Desktop.Lifecycle;

namespace OrganizaTodo.Desktop.Tests;

public class SchemaBootstrapperIntegrationTests : IAsyncLifetime
{
    private string _pgBinDirectory = "";
    private string _tempDir = "";
    private PostgresManager _manager = null!;
    private int _port;
    private DbCredentials _credentials = null!;

    public async Task InitializeAsync()
    {
        _pgBinDirectory = PostgresTestEnvironment.ResolvePgBinDirectory();
        _tempDir = Directory.CreateTempSubdirectory("organizatodo-bootstrap-test-").FullName;
        var logsDir = Path.Combine(_tempDir, "logs");
        Directory.CreateDirectory(logsDir);
        _port = PortAllocator.FindAvailablePort();
        _credentials = new DbCredentials("organizatodo_app", "test-password-123");

        _manager = new PostgresManager(_pgBinDirectory, Path.Combine(_tempDir, "pgdata"), _port, _credentials, logsDir);
        await _manager.InitializeIfNeededAsync();
        await _manager.StartAsync();
        await _manager.WaitUntilReadyAsync(TimeSpan.FromSeconds(30));
    }

    public async Task DisposeAsync()
    {
        await _manager.StopAsync();
        Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public async Task BootstrapAsync_FreshDatabase_CreatesSchemaAndIsIdempotent()
    {
        var sqlDir = Path.Combine(AppContext.BaseDirectory, "Database");
        var bootstrapper = new SchemaBootstrapper(sqlDir);

        await using var adminConnection = new NpgsqlConnection(
            PostgresConnectionStringBuilder.Build(_port, "postgres", _credentials.Username, _credentials.Password));
        await adminConnection.OpenAsync();

        await bootstrapper.BootstrapAsync(adminConnection, "organizatodo", isShoppingDatabase: false);
        // Re-running must not fail or re-apply already-applied files.
        await bootstrapper.BootstrapAsync(adminConnection, "organizatodo", isShoppingDatabase: false);

        await using var appConnection = new NpgsqlConnection(
            PostgresConnectionStringBuilder.Build(_port, "organizatodo", _credentials.Username, _credentials.Password));
        await appConnection.OpenAsync();

        var usersTableExists = await appConnection.ExecuteScalarAsync<bool>(
            "SELECT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'Users')");
        Assert.True(usersTableExists);
    }
}

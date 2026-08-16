using Npgsql;
using OrganizaTodo.Desktop.Lifecycle;

namespace OrganizaTodo.Desktop.Tests;

public class PostgresManagerIntegrationTests : IAsyncLifetime
{
    private string _pgBinDirectory = "";
    private string _tempDir = "";

    public Task InitializeAsync()
    {
        _pgBinDirectory = PostgresTestEnvironment.ResolvePgBinDirectory();
        _tempDir = Directory.CreateTempSubdirectory("organizatodo-pgmanager-test-").FullName;
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        Directory.Delete(_tempDir, recursive: true);
        return Task.CompletedTask;
    }

    [Fact]
    public async Task InitializeStartWaitStop_FullLifecycle_Succeeds()
    {
        var pgDataDir = Path.Combine(_tempDir, "pgdata");
        var logsDir = Path.Combine(_tempDir, "logs");
        Directory.CreateDirectory(logsDir);
        var port = PortAllocator.FindAvailablePort();
        var credentials = new DbCredentials("organizatodo_app", "test-password-123");

        var manager = new PostgresManager(_pgBinDirectory, pgDataDir, port, credentials, logsDir);

        await manager.InitializeIfNeededAsync();
        await manager.StartAsync();
        await manager.WaitUntilReadyAsync(TimeSpan.FromSeconds(30));

        await using var conn = new NpgsqlConnection(
            PostgresConnectionStringBuilder.Build(port, "postgres", credentials.Username, credentials.Password));
        await conn.OpenAsync();
        Assert.Equal(System.Data.ConnectionState.Open, conn.State);
        await conn.CloseAsync();

        await manager.StopAsync();
        Assert.True(PortAllocator.IsPortAvailable(port));
    }
}

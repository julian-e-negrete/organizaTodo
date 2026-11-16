using System.Diagnostics;
using Npgsql;

namespace OrganizaTodo.Desktop.Lifecycle;

public interface IPostgresManager
{
    Task InitializeIfNeededAsync(CancellationToken ct = default);
    Task StartAsync(CancellationToken ct = default);
    Task WaitUntilReadyAsync(TimeSpan timeout, CancellationToken ct = default);
    Task StopAsync(CancellationToken ct = default);
}

public sealed class PostgresManager(
    string pgBinDirectory,
    string pgDataDir,
    int port,
    DbCredentials credentials,
    string logsDir) : IPostgresManager
{
    private Process? _process;
    private Task? _stdoutPipeTask;
    private Task? _stderrPipeTask;

    public Task InitializeIfNeededAsync(CancellationToken ct = default)
    {
        if (File.Exists(Path.Combine(pgDataDir, "PG_VERSION")))
            return Task.CompletedTask;

        Directory.CreateDirectory(pgDataDir);

        var pwFile = Path.GetTempFileName();
        File.WriteAllText(pwFile, credentials.Password);
        try
        {
            RunToCompletion(
                Path.Combine(pgBinDirectory, "initdb"),
                $"-D \"{pgDataDir}\" -U {credentials.Username} --pwfile=\"{pwFile}\" --auth=scram-sha-256");
        }
        finally
        {
            File.Delete(pwFile);
        }

        // unix_socket_directories defaults to /var/run/postgresql, which a non-root,
        // unprivileged bundled-Postgres process cannot create or write to. Point it at
        // the data directory instead, which this process already owns. The app itself
        // connects over TCP to 127.0.0.1 (see PostgresConnectionStringBuilder), so this
        // only affects whether postgres can start its lock file at all.
        File.AppendAllText(
            Path.Combine(pgDataDir, "postgresql.conf"),
            $"\nlisten_addresses = '127.0.0.1'\nport = {port}\nunix_socket_directories = '{pgDataDir}'\n");

        return Task.CompletedTask;
    }

    private const long MaxLogFileSizeBytes = 10 * 1024 * 1024;

    public Task StartAsync(CancellationToken ct = default)
    {
        _process = new Process
        {
            StartInfo = new ProcessStartInfo(Path.Combine(pgBinDirectory, "postgres"), $"-D \"{pgDataDir}\"")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            }
        };
        _process.Start();

        var logPath = Path.Combine(logsDir, "postgres.log");
        TruncateLogIfOversized(logPath);
        _stdoutPipeTask = PipeStreamToFileAsync(_process.StandardOutput, logPath, ct);
        _stderrPipeTask = PipeStreamToFileAsync(_process.StandardError, logPath, ct);

        return Task.CompletedTask;
    }

    private static void TruncateLogIfOversized(string logPath)
    {
        var info = new FileInfo(logPath);
        if (info.Exists && info.Length > MaxLogFileSizeBytes)
        {
            File.Delete(logPath);
        }
    }

    public async Task WaitUntilReadyAsync(TimeSpan timeout, CancellationToken ct = default)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (_process is { HasExited: true })
                throw new InvalidOperationException($"postgres exited early with code {_process.ExitCode}");

            try
            {
                await using var conn = new NpgsqlConnection(
                    PostgresConnectionStringBuilder.Build(port, "postgres", credentials.Username, credentials.Password));
                await conn.OpenAsync(ct);
                return;
            }
            catch
            {
                await Task.Delay(200, ct);
            }
        }

        throw new TimeoutException($"postgres did not become ready within {timeout}");
    }

    public async Task StopAsync(CancellationToken ct = default)
    {
        if (_process is null) return;

        if (!_process.HasExited)
        {
            RunToCompletion(Path.Combine(pgBinDirectory, "pg_ctl"), $"stop -D \"{pgDataDir}\" -m fast -w");
            await _process.WaitForExitAsync(ct);
        }

        // The process has exited, so its ends of the stdout/stderr pipes are closed and
        // these will drain and complete quickly. Awaiting (rather than discarding) them
        // surfaces any exception from PipeStreamToFileAsync instead of losing it silently.
        if (_stdoutPipeTask is not null) await _stdoutPipeTask;
        if (_stderrPipeTask is not null) await _stderrPipeTask;

        _process.Dispose();
    }

    private static void RunToCompletion(string fileName, string arguments)
    {
        using var process = Process.Start(new ProcessStartInfo(fileName, arguments)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        })!;
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            var stderr = process.StandardError.ReadToEnd();
            throw new InvalidOperationException($"{fileName} exited with {process.ExitCode}: {stderr}");
        }
    }

    private static async Task PipeStreamToFileAsync(StreamReader reader, string path, CancellationToken ct)
    {
        await using var writer = new StreamWriter(path, append: true) { AutoFlush = true };
        string? line;
        while ((line = await reader.ReadLineAsync(ct)) != null)
        {
            await writer.WriteLineAsync(line);
        }
    }
}

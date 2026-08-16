using System.Diagnostics;

namespace OrganizaTodo.Desktop.Tests;

internal static class PostgresTestEnvironment
{
    public static string ResolvePgBinDirectory()
    {
        var overridePath = Environment.GetEnvironmentVariable("ORGANIZATODO_PG_BIN_DIR");
        if (!string.IsNullOrWhiteSpace(overridePath)) return overridePath;

        using var process = Process.Start(new ProcessStartInfo("pg_config", "--bindir")
        {
            RedirectStandardOutput = true,
            UseShellExecute = false
        })!;
        var output = process.StandardOutput.ReadToEnd().Trim();
        process.WaitForExit();

        if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(output))
            throw new InvalidOperationException(
                "pg_config not found on PATH and ORGANIZATODO_PG_BIN_DIR not set — install PostgreSQL client tools to run this test.");

        return output;
    }
}

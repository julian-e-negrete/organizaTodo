namespace OrganizaTodo.Desktop.Lifecycle;

public sealed record AppPaths(string DataDir, string PgDataDir, string LogsDir, string ConfigDir)
{
    public static AppPaths From(string appDataRoot)
    {
        var dataDir = Path.Combine(appDataRoot, "organizatodo");
        return new AppPaths(
            DataDir: dataDir,
            PgDataDir: Path.Combine(dataDir, "pgdata"),
            LogsDir: Path.Combine(dataDir, "logs"),
            ConfigDir: Path.Combine(dataDir, "config"));
    }

    public void EnsureDirectoriesExist()
    {
        Directory.CreateDirectory(DataDir);
        Directory.CreateDirectory(PgDataDir);
        Directory.CreateDirectory(LogsDir);
        Directory.CreateDirectory(ConfigDir);
    }
}

public static class AppDataRoot
{
    public static string ResolveForCurrentOS()
    {
        if (OperatingSystem.IsWindows())
            return Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".local", "share");
    }
}

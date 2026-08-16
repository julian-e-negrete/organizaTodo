using System.Security.Cryptography;
using System.Text.Json;

namespace OrganizaTodo.Desktop.Lifecycle;

public sealed record DbCredentials(string Username, string Password);

public sealed class DbCredentialStore(string configDir)
{
    private string CredentialsPath => Path.Combine(configDir, "db-credentials.json");

    public DbCredentials LoadOrCreate()
    {
        if (File.Exists(CredentialsPath))
        {
            var json = File.ReadAllText(CredentialsPath);
            return JsonSerializer.Deserialize<DbCredentials>(json)
                   ?? throw new InvalidOperationException($"{CredentialsPath} is corrupt");
        }

        Directory.CreateDirectory(configDir);
        var credentials = new DbCredentials("organizatodo_app", GenerateRandomPassword());
        File.WriteAllText(CredentialsPath, JsonSerializer.Serialize(credentials));
        RestrictToCurrentUser(CredentialsPath);
        return credentials;
    }

    private static string GenerateRandomPassword() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(24));

    private static void RestrictToCurrentUser(string path)
    {
        if (OperatingSystem.IsWindows()) return;
        File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }
}

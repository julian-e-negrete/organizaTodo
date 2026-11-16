using System.Text.Json;

namespace OrganizaTodo.Desktop.Lifecycle;

/// <summary>
/// Optional "remote database" mode. When <c>remote-db.json</c> exists in the app's config
/// directory, the desktop app skips its bundled PostgreSQL and the schema bootstrapper
/// entirely and points the in-process web app straight at an existing server database.
/// The bootstrapper must never run against a shared server: it would re-apply init.sql.
/// </summary>
public sealed record RemoteDbConfig(string DefaultConnection, string ShoppingConnection)
{
    public const string FileName = "remote-db.json";

    public static RemoteDbConfig? TryLoad(string configDir)
    {
        var path = Path.Combine(configDir, FileName);
        if (!File.Exists(path)) return null;

        var config = JsonSerializer.Deserialize<RemoteDbConfig>(File.ReadAllText(path));
        if (config is null || string.IsNullOrWhiteSpace(config.DefaultConnection))
            throw new InvalidOperationException($"{path} is missing DefaultConnection.");

        return config with
        {
            ShoppingConnection = string.IsNullOrWhiteSpace(config.ShoppingConnection)
                ? config.DefaultConnection
                : config.ShoppingConnection,
        };
    }
}

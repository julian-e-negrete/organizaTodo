namespace OrganizaTodo.Desktop.Lifecycle;

public static class PostgresConnectionStringBuilder
{
    public static string Build(int port, string database, string username, string password) =>
        $"Host=127.0.0.1;Port={port};Database={database};Username={username};Password={password};Ssl Mode=Disable";
}

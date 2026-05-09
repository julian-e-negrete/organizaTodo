using System.Data;
using Npgsql;

namespace OrganizaTodo.Web.Data;

public sealed class SqlConnectionFactory(IConfiguration configuration) : IDbConnectionFactory
{
    private readonly string _connectionString = configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");

    public IDbConnection CreateConnection() => new NpgsqlConnection(_connectionString);
}

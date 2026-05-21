using System.Data;
using Npgsql;

namespace OrganizaTodo.Web.Data;

public sealed class ShoppingDbConnectionFactory(IConfiguration configuration) : IShoppingDbConnectionFactory
{
    private readonly string _connectionString = configuration.GetConnectionString("ShoppingConnection")
        ?? throw new InvalidOperationException("Connection string 'ShoppingConnection' is not configured.");

    public IDbConnection CreateConnection() => new NpgsqlConnection(_connectionString);
}

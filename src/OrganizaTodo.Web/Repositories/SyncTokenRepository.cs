using Dapper;
using OrganizaTodo.Web.Data;

namespace OrganizaTodo.Web.Repositories;

public sealed class SyncTokenRepository(IDbConnectionFactory connectionFactory) : ISyncTokenRepository
{
    public async Task StoreTokenAsync(int userId, string tokenHash)
    {
        using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync(
            "INSERT INTO \"SyncTokens\" (\"UserId\", \"TokenHash\") VALUES (@userId, @tokenHash)",
            new { userId, tokenHash });
    }
}

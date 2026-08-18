using System.Data;
using Dapper;
using OrganizaTodo.Web.Data;

namespace OrganizaTodo.Web.Repositories;

public sealed class SyncTokenRepository(IDbConnectionFactory connectionFactory) : ISyncTokenRepository
{
    public async Task StoreTokenAsync(int userId, string tokenHash)
    {
        using var connection = connectionFactory.CreateConnection();
        await connection.QueryFirstOrDefaultAsync<object>(
            "SELECT sp_sync_tokens_store(@userId, @tokenHash)",
            new { userId, tokenHash },
            commandType: CommandType.Text);
    }
}

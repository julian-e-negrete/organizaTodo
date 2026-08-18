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

    public async Task<int?> ResolveUserIdAsync(string tokenHash)
    {
        using var connection = connectionFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<int?>(
            "SELECT sp_sync_tokens_resolve_user_id(@tokenHash)",
            new { tokenHash },
            commandType: CommandType.Text);
    }

    public async Task TouchLastUsedAsync(string tokenHash)
    {
        using var connection = connectionFactory.CreateConnection();
        await connection.QueryFirstOrDefaultAsync<object>(
            "SELECT sp_sync_tokens_touch_last_used(@tokenHash)",
            new { tokenHash },
            commandType: CommandType.Text);
    }
}

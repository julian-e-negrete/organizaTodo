using Dapper;
using OrganizaTodo.Web.Data;
using OrganizaTodo.Web.Models.Domain;

namespace OrganizaTodo.Web.Services;

public sealed class AdminService(IDbConnectionFactory connectionFactory) : IAdminService
{
    public async Task<AdminStats> GetStatsAsync()
    {
        using var conn = connectionFactory.CreateConnection();
        return await conn.QuerySingleAsync<AdminStats>("SELECT * FROM sp_admin_get_stats()");
    }
}

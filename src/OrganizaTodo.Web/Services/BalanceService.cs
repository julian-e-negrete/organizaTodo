using Dapper;
using OrganizaTodo.Web.Data;
using OrganizaTodo.Web.Models.Domain;

namespace OrganizaTodo.Web.Services;

public sealed class BalanceService(IDbConnectionFactory connectionFactory) : IBalanceService
{
    public async Task<MonthlyBalance> GetMonthlyBalanceAsync(int userId, int month, int year)
    {
        using var conn = connectionFactory.CreateConnection();
        return await conn.QuerySingleAsync<MonthlyBalance>(
            "SELECT * FROM sp_balance_get_monthly(@userId, @month, @year)",
            new { userId, month, year });
    }
}

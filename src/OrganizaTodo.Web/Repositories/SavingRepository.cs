using Dapper;
using OrganizaTodo.Web.Data;
using OrganizaTodo.Web.Models.Domain;

namespace OrganizaTodo.Web.Repositories;

public sealed class SavingRepository(IDbConnectionFactory connectionFactory) : ISavingRepository
{
    public async Task<IEnumerable<Saving>> GetByUserIdAsync(int userId)
    {
        using var conn = connectionFactory.CreateConnection();
        return await conn.QueryAsync<Saving>(
            "SELECT * FROM sp_savings_get_by_user_id(@userId)",
            new { userId });
    }

    public async Task<Saving?> GetByIdAsync(int id, int userId)
    {
        using var conn = connectionFactory.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<Saving>(
            "SELECT * FROM sp_savings_get_by_id(@id, @userId)",
            new { id, userId });
    }

    public async Task CreateAsync(int userId, decimal amount, int month, int year, string notes, bool isInitialBalance)
    {
        var p = new DynamicParameters();
        p.Add("userId",           userId,           System.Data.DbType.Int32);
        p.Add("amount",           amount,           System.Data.DbType.Decimal);
        p.Add("month",            month,            System.Data.DbType.Int32);
        p.Add("year",             year,             System.Data.DbType.Int32);
        p.Add("notes",            notes,            System.Data.DbType.String);
        p.Add("isInitialBalance", isInitialBalance, System.Data.DbType.Boolean);
        using var conn = connectionFactory.CreateConnection();
        await conn.QueryFirstOrDefaultAsync<object>(
            "SELECT sp_savings_create(@userId, @amount, @month, @year, @notes, @isInitialBalance)", p);
    }

    public async Task UpdateAsync(int id, int userId, decimal amount, string notes)
    {
        using var conn = connectionFactory.CreateConnection();
        await conn.QueryFirstOrDefaultAsync<object>(
            "SELECT sp_savings_update(@id, @userId, @amount, @notes)",
            new { id, userId, amount, notes });
    }

    public async Task DeleteAsync(int id, int userId)
    {
        using var conn = connectionFactory.CreateConnection();
        await conn.QueryFirstOrDefaultAsync<object>(
            "SELECT sp_savings_delete(@id, @userId)",
            new { id, userId });
    }

    public async Task<decimal> GetCumulativeTotalAsync(int userId)
    {
        using var conn = connectionFactory.CreateConnection();
        return await conn.ExecuteScalarAsync<decimal>(
            "SELECT sp_savings_get_cumulative_total(@userId)",
            new { userId });
    }

    public async Task<decimal> GetMonthlyAverageAsync(int userId)
    {
        using var conn = connectionFactory.CreateConnection();
        return await conn.ExecuteScalarAsync<decimal>(
            "SELECT sp_savings_get_monthly_average(@userId)",
            new { userId });
    }
}

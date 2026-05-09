using Dapper;
using OrganizaTodo.Web.Data;
using OrganizaTodo.Web.Models.Domain;

namespace OrganizaTodo.Web.Repositories;

public sealed class FixedLiabilityRepository(IDbConnectionFactory connectionFactory) : IFixedLiabilityRepository
{
    public async Task<IEnumerable<FixedLiability>> GetByUserIdAsync(int userId)
    {
        using var conn = connectionFactory.CreateConnection();
        return await conn.QueryAsync<FixedLiability>(
            "SELECT * FROM sp_fixed_liabilities_get_by_user_id(@userId)",
            new { userId });
    }

    public async Task<FixedLiability?> GetByIdAsync(int id, int userId)
    {
        using var conn = connectionFactory.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<FixedLiability>(
            "SELECT * FROM sp_fixed_liabilities_get_by_id(@id, @userId)",
            new { id, userId });
    }

    public async Task CreateAsync(int userId, string name, decimal monthlyAmount, int? dueDay)
    {
        using var conn = connectionFactory.CreateConnection();
        await conn.QueryFirstOrDefaultAsync<object>(
            "SELECT sp_fixed_liabilities_create(@userId, @name, @monthlyAmount, @dueDay)",
            new { userId, name, monthlyAmount, dueDay });
    }

    public async Task UpdateAsync(int id, int userId, string name, decimal monthlyAmount, int? dueDay)
    {
        using var conn = connectionFactory.CreateConnection();
        await conn.QueryFirstOrDefaultAsync<object>(
            "SELECT sp_fixed_liabilities_update(@id, @userId, @name, @monthlyAmount, @dueDay)",
            new { id, userId, name, monthlyAmount, dueDay });
    }

    public async Task DeleteAsync(int id, int userId)
    {
        using var conn = connectionFactory.CreateConnection();
        await conn.QueryFirstOrDefaultAsync<object>(
            "SELECT sp_fixed_liabilities_delete(@id, @userId)",
            new { id, userId });
    }

    public async Task<decimal> GetMonthlyTotalAsync(int userId)
    {
        using var conn = connectionFactory.CreateConnection();
        return await conn.ExecuteScalarAsync<decimal>(
            "SELECT sp_fixed_liabilities_get_monthly_total(@userId)",
            new { userId });
    }

    public async Task MarkPaidAsync(int id, int userId, bool isPaid)
    {
        var p = new DynamicParameters();
        p.Add("id",     id,     System.Data.DbType.Int32);
        p.Add("userId", userId, System.Data.DbType.Int32);
        p.Add("isPaid", isPaid, System.Data.DbType.Boolean);
        using var conn = connectionFactory.CreateConnection();
        await conn.QueryFirstOrDefaultAsync<object>(
            "SELECT sp_fixed_liabilities_mark_paid(@id, @userId, @isPaid)", p);
    }
}

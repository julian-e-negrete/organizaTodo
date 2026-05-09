using Dapper;
using OrganizaTodo.Web.Data;
using OrganizaTodo.Web.Models.Domain;

namespace OrganizaTodo.Web.Repositories;

public sealed class IncomeRepository(IDbConnectionFactory connectionFactory) : IIncomeRepository
{
    public async Task<IEnumerable<Income>> GetByUserIdAndPeriodAsync(int userId, int month, int year)
    {
        using var conn = connectionFactory.CreateConnection();
        return await conn.QueryAsync<Income>(
            "SELECT * FROM sp_income_get_by_user_id_and_period(@userId, @month, @year)",
            new { userId, month, year });
    }

    public async Task<Income?> GetByIdAsync(int id, int userId)
    {
        using var conn = connectionFactory.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<Income>(
            "SELECT * FROM sp_income_get_by_id(@id, @userId)",
            new { id, userId });
    }

    public async Task CreateAsync(int userId, string description, decimal amount, string? category, int month, int year)
    {
        using var conn = connectionFactory.CreateConnection();
        await conn.QueryFirstOrDefaultAsync<object>(
            "SELECT sp_income_create(@userId, @description, @amount, @category, @month, @year)",
            new { userId, description, amount, category, month, year });
    }

    public async Task UpdateAsync(int id, int userId, string description, decimal amount, string? category)
    {
        using var conn = connectionFactory.CreateConnection();
        await conn.QueryFirstOrDefaultAsync<object>(
            "SELECT sp_income_update(@id, @userId, @description, @amount, @category)",
            new { id, userId, description, amount, category });
    }

    public async Task DeleteAsync(int id, int userId)
    {
        using var conn = connectionFactory.CreateConnection();
        await conn.QueryFirstOrDefaultAsync<object>(
            "SELECT sp_income_delete(@id, @userId)",
            new { id, userId });
    }

    public async Task<decimal> GetMonthlyTotalAsync(int userId, int month, int year)
    {
        using var conn = connectionFactory.CreateConnection();
        return await conn.ExecuteScalarAsync<decimal>(
            "SELECT sp_income_get_monthly_total(@userId, @month, @year)",
            new { userId, month, year });
    }
}

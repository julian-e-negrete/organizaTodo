using Dapper;
using OrganizaTodo.Web.Data;
using OrganizaTodo.Web.Models.Domain;

namespace OrganizaTodo.Web.Repositories;

public sealed class OtherExpenseRepository(IDbConnectionFactory connectionFactory) : IOtherExpenseRepository
{
    public async Task<IEnumerable<OtherExpense>> GetByUserIdAndPeriodAsync(int userId, int month, int year)
    {
        using var conn = connectionFactory.CreateConnection();
        return await conn.QueryAsync<OtherExpense>(
            "SELECT * FROM sp_other_expenses_get_by_user_id_and_period(@userId, @month, @year)",
            new { userId, month, year });
    }

    public async Task<OtherExpense?> GetByIdAsync(int id, int userId)
    {
        using var conn = connectionFactory.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<OtherExpense>(
            "SELECT * FROM sp_other_expenses_get_by_id(@id, @userId)",
            new { id, userId });
    }

    public async Task CreateAsync(int userId, string description, decimal amount, int month, int year)
    {
        using var conn = connectionFactory.CreateConnection();
        await conn.QueryFirstOrDefaultAsync<object>(
            "SELECT sp_other_expenses_create(@userId, @description, @amount, @month, @year)",
            new { userId, description, amount, month, year });
    }

    public async Task UpdateAsync(int id, int userId, string description, decimal amount)
    {
        using var conn = connectionFactory.CreateConnection();
        await conn.QueryFirstOrDefaultAsync<object>(
            "SELECT sp_other_expenses_update(@id, @userId, @description, @amount)",
            new { id, userId, description, amount });
    }

    public async Task DeleteAsync(int id, int userId)
    {
        using var conn = connectionFactory.CreateConnection();
        await conn.QueryFirstOrDefaultAsync<object>(
            "SELECT sp_other_expenses_delete(@id, @userId)",
            new { id, userId });
    }

    public async Task<decimal> GetMonthlyTotalAsync(int userId, int month, int year)
    {
        using var conn = connectionFactory.CreateConnection();
        return await conn.ExecuteScalarAsync<decimal>(
            "SELECT sp_other_expenses_get_monthly_total(@userId, @month, @year)",
            new { userId, month, year });
    }
}

using Dapper;
using OrganizaTodo.Web.Data;
using OrganizaTodo.Web.Models.Domain;

namespace OrganizaTodo.Web.Repositories;

public sealed class RecurringIncomeRepository(IDbConnectionFactory connectionFactory) : IRecurringIncomeRepository
{
    public async Task<IEnumerable<RecurringIncome>> GetByUserIdAsync(int userId)
    {
        using var conn = connectionFactory.CreateConnection();
        return await conn.QueryAsync<RecurringIncome>(
            "SELECT * FROM sp_recurring_income_get_by_user_id(@userId)",
            new { userId });
    }

    public async Task<RecurringIncome?> GetByIdAsync(int id, int userId)
    {
        using var conn = connectionFactory.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<RecurringIncome>(
            "SELECT * FROM sp_recurring_income_get_by_id(@id, @userId)",
            new { id, userId });
    }

    public async Task CreateAsync(int userId, string description, decimal amount)
    {
        using var conn = connectionFactory.CreateConnection();
        await conn.QueryFirstOrDefaultAsync<object>(
            "SELECT sp_recurring_income_create(@userId, @description, @amount)",
            new { userId, description, amount });
    }

    public async Task UpdateAsync(int id, int userId, string description, decimal amount)
    {
        using var conn = connectionFactory.CreateConnection();
        await conn.QueryFirstOrDefaultAsync<object>(
            "SELECT sp_recurring_income_update(@id, @userId, @description, @amount)",
            new { id, userId, description, amount });
    }

    public async Task ToggleActiveAsync(int id, int userId)
    {
        using var conn = connectionFactory.CreateConnection();
        await conn.QueryFirstOrDefaultAsync<object>(
            "SELECT sp_recurring_income_toggle_active(@id, @userId)",
            new { id, userId });
    }

    public async Task DeleteAsync(int id, int userId)
    {
        using var conn = connectionFactory.CreateConnection();
        await conn.QueryFirstOrDefaultAsync<object>(
            "SELECT sp_recurring_income_delete(@id, @userId)",
            new { id, userId });
    }
}

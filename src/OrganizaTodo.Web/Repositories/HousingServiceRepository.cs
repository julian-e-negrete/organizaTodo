using Dapper;
using System.Data;
using Npgsql;
using OrganizaTodo.Web.Data;
using OrganizaTodo.Web.Models.Domain;

namespace OrganizaTodo.Web.Repositories;

public sealed class HousingServiceRepository(IDbConnectionFactory connectionFactory) : IHousingServiceRepository
{
    public async Task<IEnumerable<HousingService>> GetByUserIdAsync(int userId)
    {
        using var conn = connectionFactory.CreateConnection();
        return await conn.QueryAsync<HousingService>(
            "SELECT * FROM sp_housing_services_get_by_user_id(@userId)",
            new { userId });
    }

    public async Task<HousingService?> GetByIdAsync(int id, int userId)
    {
        using var conn = connectionFactory.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<HousingService>(
            "SELECT * FROM sp_housing_services_get_by_id(@id, @userId)",
            new { id, userId });
    }

    public async Task CreateAsync(int userId, string name, decimal amount, int dueDay, string periodicity)
    {
        using var conn = connectionFactory.CreateConnection();
        await conn.QueryFirstOrDefaultAsync<object>(
            "SELECT sp_housing_services_create(@userId, @name, @amount, @dueDay, @periodicity)",
            new { userId, name, amount, dueDay, periodicity });
    }

    public async Task UpdateAsync(int id, int userId, string name, decimal amount, int dueDay, string periodicity)
    {
        using var conn = connectionFactory.CreateConnection();
        await conn.QueryFirstOrDefaultAsync<object>(
            "SELECT sp_housing_services_update(@id, @userId, @name, @amount, @dueDay, @periodicity)",
            new { id, userId, name, amount, dueDay, periodicity });
    }

    public async Task DeleteAsync(int id, int userId)
    {
        using var conn = connectionFactory.CreateConnection();
        await conn.QueryFirstOrDefaultAsync<object>(
            "SELECT sp_housing_services_delete(@id, @userId)",
            new { id, userId });
    }

public async Task MarkPaidAsync(int id, int userId, bool isPaid)
{
    using var conn = connectionFactory.CreateConnection();
    await conn.ExecuteAsync(
        "SELECT sp_housing_services_mark_paid(@id, @user_id, @is_paid)",
        new { id, user_id = userId, is_paid = isPaid });
}

    public async Task<decimal> GetMonthlyTotalAsync(int userId)
    {
        using var conn = connectionFactory.CreateConnection();
        return await conn.ExecuteScalarAsync<decimal>(
            "SELECT sp_housing_services_get_monthly_total(@userId)",
            new { userId });
    }
}

using Dapper;
using OrganizaTodo.Web.Data;
using OrganizaTodo.Web.Models.Domain;

namespace OrganizaTodo.Web.Repositories;

public sealed class UserRepository(IDbConnectionFactory connectionFactory) : IUserRepository
{
    public async Task<User?> GetByEmailAsync(string email)
    {
        using var conn = connectionFactory.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<User>(
            "SELECT * FROM sp_users_get_by_email(@email)",
            new { email });
    }

    public async Task<User?> GetByIdAsync(int id)
    {
        using var conn = connectionFactory.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<User>(
            "SELECT * FROM sp_users_get_by_id(@userId)",
            new { userId = id });
    }

    public async Task<int> CreateAsync(string email, string passwordHash, string fullName, string currency)
    {
        using var conn = connectionFactory.CreateConnection();
        return await conn.ExecuteScalarAsync<int>(
            "SELECT sp_users_create(@email, @passwordHash, @fullName, @currency)",
            new { email, passwordHash, fullName, currency });
    }

    public async Task<bool> EmailExistsAsync(string email)
    {
        using var conn = connectionFactory.CreateConnection();
        return await conn.ExecuteScalarAsync<bool>(
            "SELECT sp_users_email_exists(@email)",
            new { email });
    }

    public async Task UpdateLastAccessAsync(int userId)
    {
        using var conn = connectionFactory.CreateConnection();
        await conn.QueryFirstOrDefaultAsync<object>(
            "SELECT sp_users_update_last_access(@userId)",
            new { userId });
    }

    public async Task UpdateProfileAsync(int userId, string fullName, string currency)
    {
        using var conn = connectionFactory.CreateConnection();
        await conn.QueryFirstOrDefaultAsync<object>(
            "SELECT sp_users_update_profile(@userId, @fullName, @currency)",
            new { userId, fullName, currency });
    }

    public async Task SetActiveAsync(int userId, bool isActive)
    {
        using var conn = connectionFactory.CreateConnection();
        await conn.QueryFirstOrDefaultAsync<object>(
            "SELECT sp_users_set_active(@userId, @isActive)",
            new { userId, isActive });
    }

    public async Task SetRoleAsync(int userId, string role)
    {
        using var conn = connectionFactory.CreateConnection();
        await conn.QueryFirstOrDefaultAsync<object>(
            "SELECT sp_users_set_role(@userId, @role)",
            new { userId, role });
    }

    public async Task DeleteAsync(int userId)
    {
        using var conn = connectionFactory.CreateConnection();
        await conn.QueryFirstOrDefaultAsync<object>(
            "SELECT sp_users_delete(@userId)",
            new { userId });
    }

    public async Task<IEnumerable<User>> GetAllAsync()
    {
        using var conn = connectionFactory.CreateConnection();
        return await conn.QueryAsync<User>("SELECT * FROM sp_users_get_all()");
    }
}

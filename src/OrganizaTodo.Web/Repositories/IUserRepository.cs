using OrganizaTodo.Web.Models.Domain;

namespace OrganizaTodo.Web.Repositories;

public interface IUserRepository
{
    Task<User?> GetByEmailAsync(string email);
    Task<User?> GetByIdAsync(int id);
    Task<int> CreateAsync(string email, string passwordHash, string fullName, string currency);
    Task<bool> EmailExistsAsync(string email);
    Task UpdateLastAccessAsync(int userId);
    Task UpdateProfileAsync(int userId, string fullName, string currency);
    Task SetActiveAsync(int userId, bool isActive);
    Task SetRoleAsync(int userId, string role);
    Task DeleteAsync(int userId);
    Task<IEnumerable<User>> GetAllAsync();
}

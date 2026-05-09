using OrganizaTodo.Web.Models.Domain;

namespace OrganizaTodo.Web.Repositories;

public interface IIncomeRepository
{
    Task<IEnumerable<Income>> GetByUserIdAndPeriodAsync(int userId, int month, int year);
    Task<Income?> GetByIdAsync(int id, int userId);
    Task CreateAsync(int userId, string description, decimal amount, string? category, int month, int year);
    Task UpdateAsync(int id, int userId, string description, decimal amount, string? category);
    Task DeleteAsync(int id, int userId);
    Task<decimal> GetMonthlyTotalAsync(int userId, int month, int year);
}

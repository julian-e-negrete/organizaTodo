using OrganizaTodo.Web.Models.Domain;

namespace OrganizaTodo.Web.Repositories;

public interface IRecurringIncomeRepository
{
    Task<IEnumerable<RecurringIncome>> GetByUserIdAsync(int userId);
    Task<RecurringIncome?> GetByIdAsync(int id, int userId);
    Task CreateAsync(int userId, string description, decimal amount);
    Task UpdateAsync(int id, int userId, string description, decimal amount);
    Task ToggleActiveAsync(int id, int userId);
    Task DeleteAsync(int id, int userId);
}

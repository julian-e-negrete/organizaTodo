using OrganizaTodo.Web.Models.Domain;

namespace OrganizaTodo.Web.Repositories;

public interface ISavingRepository
{
    Task<IEnumerable<Saving>> GetByUserIdAsync(int userId);
    Task<Saving?> GetByIdAsync(int id, int userId);
    Task CreateAsync(int userId, decimal amount, int month, int year, string notes, bool isInitialBalance);
    Task UpdateAsync(int id, int userId, decimal amount, string notes);
    Task DeleteAsync(int id, int userId);
    Task<decimal> GetCumulativeTotalAsync(int userId);
    Task<decimal> GetMonthlyAverageAsync(int userId);
}

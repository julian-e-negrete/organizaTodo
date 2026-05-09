using OrganizaTodo.Web.Models.Domain;

namespace OrganizaTodo.Web.Repositories;

public interface IHousingServiceRepository
{
    Task<IEnumerable<HousingService>> GetByUserIdAsync(int userId);
    Task<HousingService?> GetByIdAsync(int id, int userId);
    Task CreateAsync(int userId, string name, decimal amount, int dueDay, string periodicity);
    Task UpdateAsync(int id, int userId, string name, decimal amount, int dueDay, string periodicity);
    Task DeleteAsync(int id, int userId);
    Task MarkPaidAsync(int id, int userId, bool isPaid);
    Task<decimal> GetMonthlyTotalAsync(int userId);
}

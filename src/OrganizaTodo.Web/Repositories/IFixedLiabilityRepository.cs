using OrganizaTodo.Web.Models.Domain;

namespace OrganizaTodo.Web.Repositories;

public interface IFixedLiabilityRepository
{
    Task<IEnumerable<FixedLiability>> GetByUserIdAsync(int userId);
    Task<FixedLiability?> GetByIdAsync(int id, int userId);
    Task CreateAsync(int userId, string name, decimal monthlyAmount, int? dueDay);
    Task UpdateAsync(int id, int userId, string name, decimal monthlyAmount, int? dueDay);
    Task DeleteAsync(int id, int userId);
    Task<decimal> GetMonthlyTotalAsync(int userId);
    Task MarkPaidAsync(int id, int userId, bool isPaid);
}

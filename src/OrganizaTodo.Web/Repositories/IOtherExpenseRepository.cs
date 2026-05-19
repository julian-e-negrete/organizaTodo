using OrganizaTodo.Web.Models.Domain;

namespace OrganizaTodo.Web.Repositories;

public interface IOtherExpenseRepository
{
    Task<IEnumerable<OtherExpense>> GetByUserIdAndPeriodAsync(int userId, int month, int year);
    Task<OtherExpense?> GetByIdAsync(int id, int userId);
    Task CreateAsync(int userId, string description, decimal amount, int month, int year, int? vehicleId = null);
    Task UpdateAsync(int id, int userId, string description, decimal amount, int? vehicleId = null);
    Task DeleteAsync(int id, int userId);
    Task<decimal> GetMonthlyTotalAsync(int userId, int month, int year);
}

using OrganizaTodo.Web.Models.Domain;

namespace OrganizaTodo.Web.Services;

public interface IBalanceService
{
    Task<MonthlyBalance> GetMonthlyBalanceAsync(int userId, int month, int year);
}

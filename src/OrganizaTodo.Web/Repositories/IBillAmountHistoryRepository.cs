using OrganizaTodo.Web.Models.Domain;

namespace OrganizaTodo.Web.Repositories;

public interface IBillAmountHistoryRepository
{
    /// <summary>Amount changes of one bill, newest first. <paramref name="billType"/> is BillAmountChange.Housing or .Fixed.</summary>
    Task<IReadOnlyList<BillAmountChange>> GetForBillAsync(int userId, string billType, int billId);
}

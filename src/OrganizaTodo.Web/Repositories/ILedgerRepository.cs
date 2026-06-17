using OrganizaTodo.Web.Models.Domain;

namespace OrganizaTodo.Web.Repositories;

public interface ILedgerRepository
{
    Task<LedgerTag> GetOrCreateTagAsync(int userId, string name);
    Task<IEnumerable<LedgerTag>> GetTagsByUserIdAsync(int userId);
    Task<decimal> GetCurrentBalanceAsync(int userId);
    Task<decimal> GetBalanceAsOfAsync(int userId, DateTime asOf);
    Task<long> GetTransactionCountAsync(int userId);
    Task<int> CreateExpenseAsync(int userId, decimal amount, string description, DateTime transactionDate, bool generatesInterest, decimal? interestRate);
    Task<int> CreateIncomeAsync(int userId, decimal amount, string description, DateTime transactionDate);
    Task<int> CreateInitialAsync(int userId, decimal amount, DateTime transactionDate);
    Task<int> CreateInterestAsync(int userId, decimal amount, DateTime transactionDate, decimal interestRate, int parentTransactionId, decimal balanceBefore);
    Task LinkTagAsync(int transactionId, int tagId);
    Task<(IEnumerable<LedgerTransaction> Items, long TotalCount)> GetHistoryAsync(int userId, LedgerHistoryFilter filter, int page, int pageSize);
    Task<IEnumerable<LedgerBalancePoint>> GetBalanceSeriesAsync(int userId);
    Task<LedgerTransaction?> GetDetailAsync(int id, int userId);
    Task<IEnumerable<LedgerTransactionChild>> GetChildrenAsync(int id, int userId);
    Task<(int Id, decimal InterestRate, DateTime TransactionDate)?> GetLatestAnchorExpenseAsync(int userId, DateTime monthEnd);
    Task<(DateTime? LastInterestDate, DateTime? FirstAnchorExpenseDate)> GetInterestPointersAsync(int userId);
}

using OrganizaTodo.Web.Models.Domain;

namespace OrganizaTodo.Web.Models.ViewModels;

public sealed class LedgerIndexViewModel
{
    public decimal CurrentBalance { get; init; }
    public bool IsNegative => CurrentBalance < 0;
    public IEnumerable<LedgerTransaction> RecentTransactions { get; init; } = [];
    public IEnumerable<LedgerBalancePoint> BalanceSeries { get; init; } = [];
}

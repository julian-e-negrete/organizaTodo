using OrganizaTodo.Web.Models.Domain;

namespace OrganizaTodo.Web.Models.ViewModels;

public sealed class LedgerTransactionDetailViewModel
{
    public required LedgerTransaction Transaction { get; set; }
    public IEnumerable<LedgerTransactionChild> ChildInterestTransactions { get; set; } = [];
}

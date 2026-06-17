namespace OrganizaTodo.Web.Models.Domain;

public sealed class LedgerTransactionChild
{
    public int Id { get; set; }
    public decimal Amount { get; set; }
    public DateTime TransactionDate { get; set; }
    public decimal? InterestRate { get; set; }
    public decimal BalanceAfter { get; set; }
}

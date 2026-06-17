namespace OrganizaTodo.Web.Models.Domain;

public sealed class LedgerBalancePoint
{
    public int Id { get; set; }
    public DateTime TransactionDate { get; set; }
    public decimal BalanceAfter { get; set; }
}

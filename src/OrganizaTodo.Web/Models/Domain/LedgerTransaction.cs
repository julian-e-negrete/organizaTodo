namespace OrganizaTodo.Web.Models.Domain;

public class LedgerTransaction
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public decimal Amount { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime TransactionDate { get; set; }
    public decimal BalanceAfter { get; set; }
    public bool GeneratesInterest { get; set; }
    public decimal? InterestRate { get; set; }
    public int? ParentTransactionId { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? Tags { get; set; }
    public string? ParentDescription { get; set; }
    public DateTime? ParentTransactionDate { get; set; }

    public IEnumerable<string> TagList =>
        string.IsNullOrEmpty(Tags) ? [] : Tags.Split(',', StringSplitOptions.RemoveEmptyEntries);
}

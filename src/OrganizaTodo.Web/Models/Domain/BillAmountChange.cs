namespace OrganizaTodo.Web.Models.Domain;

/// <summary>One logged change of a bill's amount (migration 019, table BillAmountHistory).</summary>
public sealed class BillAmountChange
{
    public const string Housing = "HOUSING";
    public const string Fixed = "FIXED";

    public int Id { get; set; }
    public string BillType { get; set; } = Housing;
    public int BillId { get; set; }
    public string BillName { get; set; } = string.Empty;
    public decimal? OldAmount { get; set; }
    public decimal NewAmount { get; set; }
    public string Source { get; set; } = "app";
    public string? Note { get; set; }
    public DateTime ChangedAt { get; set; }
}

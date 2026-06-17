namespace OrganizaTodo.Web.Models.Domain;

public sealed class LedgerHistoryFilter
{
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }
    public string? Type { get; set; }
    public int[]? TagIds { get; set; }
    public decimal? AmountMin { get; set; }
    public decimal? AmountMax { get; set; }
    public bool? OnlyWithInterest { get; set; }
    public bool? OnlyNegativeBalance { get; set; }
    public string? Search { get; set; }
}

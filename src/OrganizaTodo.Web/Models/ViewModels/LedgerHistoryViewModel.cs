using OrganizaTodo.Web.Models.Domain;

namespace OrganizaTodo.Web.Models.ViewModels;

public sealed class LedgerHistoryViewModel
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
    public string? GroupBy { get; set; }

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
    public long TotalCount { get; set; }
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling(TotalCount / (double)PageSize) : 0;

    public IEnumerable<LedgerTransaction> Items { get; set; } = [];
    public IEnumerable<LedgerHistoryGroup> Groups { get; set; } = [];
    public IEnumerable<LedgerTag> AvailableTags { get; set; } = [];
}

public sealed class LedgerHistoryGroup
{
    public required string Label { get; set; }
    public DateTime PeriodStart { get; set; }
    public IEnumerable<LedgerTransaction> Items { get; set; } = [];
    public decimal Subtotal { get; set; }
    public int Count { get; set; }
}

using OrganizaTodo.Web.Models.Domain;

namespace OrganizaTodo.Web.Models.ViewModels;

public sealed class SavingsIndexViewModel
{
    public IEnumerable<Saving> Items { get; init; } = [];
    public decimal CumulativeTotal { get; init; }
    public decimal MonthlyAverage { get; init; }
    public decimal Projection6Months => CumulativeTotal + MonthlyAverage * 6;
    public decimal Projection12Months => CumulativeTotal + MonthlyAverage * 12;
}

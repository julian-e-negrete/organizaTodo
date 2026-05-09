using OrganizaTodo.Web.Models.Domain;

namespace OrganizaTodo.Web.Models.ViewModels;

public sealed class IncomeIndexViewModel
{
    public IEnumerable<Income> Items { get; init; } = [];
    public int Month { get; init; }
    public int Year { get; init; }
}

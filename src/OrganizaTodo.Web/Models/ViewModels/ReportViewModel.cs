using OrganizaTodo.Web.Models.Domain;

namespace OrganizaTodo.Web.Models.ViewModels;

public sealed class ReportViewModel
{
    public IEnumerable<MonthlyBalanceSummary> History { get; set; } = [];
    public int SelectedYear { get; set; } = DateTime.Today.Year;
}

public sealed class MonthlyBalanceSummary
{
    public int Month { get; set; }
    public int Year { get; set; }
    public MonthlyBalance Balance { get; set; } = new();
    public string MonthName => new DateTime(Year, Month, 1).ToString("MMMM",
        new System.Globalization.CultureInfo("es-AR"));
}

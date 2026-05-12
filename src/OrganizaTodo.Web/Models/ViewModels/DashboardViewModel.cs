using OrganizaTodo.Web.Models.Domain;

namespace OrganizaTodo.Web.Models.ViewModels;

public sealed class DashboardViewModel
{
    public MonthlyBalance Balance { get; set; } = new();
    public int Month { get; set; }
    public int Year { get; set; }
    public string MonthName => new DateTime(Year, Month, 1).ToString("MMMM yyyy",
        new System.Globalization.CultureInfo("es-AR"));
    public IEnumerable<HousingService> UnpaidServices { get; set; } = [];
    public IEnumerable<FixedLiability> UnpaidLiabilities { get; set; } = [];
}

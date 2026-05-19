using OrganizaTodo.Web.Models.Domain;

namespace OrganizaTodo.Web.Models.ViewModels;

public sealed class CreditCardIndexViewModel
{
    public IEnumerable<CreditCardPurchase> Items { get; init; } = [];
    public int Month { get; init; }
    public int Year { get; init; }

    private static readonly string[] MonthNames =
        ["", "Enero", "Febrero", "Marzo", "Abril", "Mayo", "Junio",
         "Julio", "Agosto", "Septiembre", "Octubre", "Noviembre", "Diciembre"];

    public string MonthName => MonthNames[Month];

    public (int month, int year) Prev => Month == 1 ? (12, Year - 1) : (Month - 1, Year);
    public (int month, int year) Next => Month == 12 ? (1, Year + 1) : (Month + 1, Year);
}

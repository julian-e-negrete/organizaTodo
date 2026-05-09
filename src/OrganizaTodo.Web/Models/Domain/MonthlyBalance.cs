namespace OrganizaTodo.Web.Models.Domain;

public sealed class MonthlyBalance
{
    public decimal TotalIncome { get; set; }
    public decimal TotalServices { get; set; }
    public decimal TotalCreditCard { get; set; }
    public decimal TotalFixedLiabilities { get; set; }
    public decimal TotalLiabilities { get; set; }
    public decimal TotalOtherExpenses { get; set; }
    public decimal RemainingBalance { get; set; }
}

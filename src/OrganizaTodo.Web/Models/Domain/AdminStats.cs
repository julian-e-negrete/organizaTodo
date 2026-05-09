namespace OrganizaTodo.Web.Models.Domain;

public sealed class AdminStats
{
    public int TotalUsers { get; set; }
    public int ActiveUsers { get; set; }
    public int AdminCount { get; set; }
    public decimal TotalIncomeAllUsers { get; set; }
    public int TotalIncomeRecords { get; set; }
}

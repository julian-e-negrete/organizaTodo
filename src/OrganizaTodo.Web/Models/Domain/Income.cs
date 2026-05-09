namespace OrganizaTodo.Web.Models.Domain;

public sealed class Income
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime IncomeDate { get; set; }
    public string? Category { get; set; }
    public int Month { get; set; }
    public int Year { get; set; }
}

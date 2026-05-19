namespace OrganizaTodo.Web.Models.Domain;

public sealed class OtherExpense
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime ExpenseDate { get; set; }
    public int Month { get; set; }
    public int Year { get; set; }
    public int? VehicleId { get; set; }
    public string? VehicleName { get; set; }
}

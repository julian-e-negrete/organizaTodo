namespace OrganizaTodo.Web.Models.Domain;

public sealed class HousingService
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public int DueDay { get; set; }
    public string Periodicity { get; set; } = "MONTHLY";
    public bool IsPaid { get; set; }
    public DateTime? PaidDate { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
}

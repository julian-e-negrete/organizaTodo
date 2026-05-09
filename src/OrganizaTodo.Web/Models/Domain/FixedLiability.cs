namespace OrganizaTodo.Web.Models.Domain;

public sealed class FixedLiability
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal MonthlyAmount { get; set; }
    public int? DueDay { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsPaid { get; set; }
    public DateTime? PaidDate { get; set; }
    public DateTime CreatedAt { get; set; }
}

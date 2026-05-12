namespace OrganizaTodo.Web.Models.Domain;

public sealed class Saving
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public decimal Amount { get; set; }
    public int Month { get; set; }
    public int Year { get; set; }
    public string Notes { get; set; } = string.Empty;
    public bool IsInitialBalance { get; set; }
    public DateTime CreatedAt { get; set; }
}

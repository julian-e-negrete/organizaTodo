namespace OrganizaTodo.Web.Models.Domain;

public sealed class LedgerTag
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public long UsageCount { get; set; }
}

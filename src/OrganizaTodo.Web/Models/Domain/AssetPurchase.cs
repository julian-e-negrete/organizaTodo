namespace OrganizaTodo.Web.Models.Domain;

public sealed class AssetPurchase
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int? ExpenseId { get; set; }
    public int? SavingId { get; set; }
    public string AssetType { get; set; } = "";
    public decimal? Quantity { get; set; }
    public decimal? UnitPriceArs { get; set; }
    public string? ExchangeType { get; set; }
    public decimal TotalArs { get; set; }
    public string? Notes { get; set; }
    public DateOnly PurchaseDate { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? ExpenseDescription { get; set; }
    public string? SavingNotes { get; set; }
}

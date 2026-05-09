namespace OrganizaTodo.Web.Models.Domain;

public sealed class ShoppingListItem
{
    public int Id { get; set; }
    public int ShoppingListId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; } = 1;
    public decimal? EstimatedPrice { get; set; }
    public string? Supermarket { get; set; }
    public int Priority { get; set; } = 2;

    public decimal LineTotal => (EstimatedPrice ?? 0) * Quantity;
    public string PriorityLabel => Priority switch { 1 => "Alta", 3 => "Baja", _ => "Media" };
}

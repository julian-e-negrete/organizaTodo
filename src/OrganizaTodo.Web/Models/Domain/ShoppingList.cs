namespace OrganizaTodo.Web.Models.Domain;

public sealed class ShoppingList
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Name { get; set; } = "Lista del Mes";
    public int Month { get; set; }
    public int Year { get; set; }
    public DateTime CreatedAt { get; set; }
    public decimal TotalEstimated { get; set; }
    public int ItemCount { get; set; }
}

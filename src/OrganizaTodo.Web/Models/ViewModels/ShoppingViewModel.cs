using System.ComponentModel.DataAnnotations;
using OrganizaTodo.Web.Models.Domain;

namespace OrganizaTodo.Web.Models.ViewModels;

public sealed class ShoppingViewModel
{
    public ShoppingList List { get; set; } = new();
    public IEnumerable<ShoppingListItem> Items { get; set; } = [];
    public IEnumerable<MockProduct> SearchResults { get; set; } = [];
    public string? SearchQuery { get; set; }
    public decimal RemainingBalance { get; set; }

    public decimal TotalEstimated => Items.Sum(i => i.LineTotal);
    public bool IsOverBudget => TotalEstimated > RemainingBalance && RemainingBalance > 0;
}

public sealed class AddShoppingItemViewModel
{
    [Required]
    public int ShoppingListId { get; set; }

    [Required(ErrorMessage = "El producto es obligatorio")]
    [StringLength(200)]
    [Display(Name = "Producto")]
    public string ProductName { get; set; } = string.Empty;

    [Required]
    [Range(1, 999)]
    [Display(Name = "Cantidad")]
    public int Quantity { get; set; } = 1;

    [Range(0, 99_999_999)]
    [Display(Name = "Precio estimado")]
    public decimal? EstimatedPrice { get; set; }

    [Display(Name = "Supermercado")]
    public string? Supermarket { get; set; }

    [Range(1, 3)]
    [Display(Name = "Prioridad")]
    public int Priority { get; set; } = 2;
}

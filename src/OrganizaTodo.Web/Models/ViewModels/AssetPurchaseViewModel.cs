using System.ComponentModel.DataAnnotations;

namespace OrganizaTodo.Web.Models.ViewModels;

public sealed class AssetPurchaseViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Seleccioná el tipo de activo")]
    public string AssetType { get; set; } = "USD";

    [Range(0.000001, 999999999, ErrorMessage = "Cantidad inválida")]
    public decimal? Quantity { get; set; }

    [Range(0.01, 999999999, ErrorMessage = "Precio unitario inválido")]
    public decimal? UnitPriceArs { get; set; }

    public string? ExchangeType { get; set; }

    [Required(ErrorMessage = "Ingresá el monto total en ARS")]
    [Range(0.01, 99999999, ErrorMessage = "Monto inválido")]
    public decimal TotalArs { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    [Required]
    public DateOnly PurchaseDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    public bool CreateExpense { get; set; } = true;

    [MaxLength(200)]
    public string? ExpenseDescription { get; set; }
    public int ExpenseMonth { get; set; } = DateTime.Today.Month;
    public int ExpenseYear { get; set; } = DateTime.Today.Year;

    public bool CreateSaving { get; set; } = true;

    [MaxLength(200)]
    public string? SavingNotes { get; set; }
    public int SavingMonth { get; set; } = DateTime.Today.Month;
    public int SavingYear { get; set; } = DateTime.Today.Year;
}

using System.ComponentModel.DataAnnotations;

namespace OrganizaTodo.Web.Models.ViewModels;

public sealed class VehicleViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "La patente es obligatoria")]
    [StringLength(10)]
    [Display(Name = "Patente")]
    public string Plate { get; set; } = "";

    [Required(ErrorMessage = "La marca es obligatoria")]
    [StringLength(100)]
    [Display(Name = "Marca")]
    public string Make { get; set; } = "";

    [Required(ErrorMessage = "El modelo es obligatorio")]
    [StringLength(100)]
    [Display(Name = "Modelo")]
    public string Model { get; set; } = "";

    [Range(1900, 2100, ErrorMessage = "Año inválido")]
    [Display(Name = "Año")]
    public int? Year { get; set; }

    [Required]
    [Range(1, 10000, ErrorMessage = "Ingresá los km por semana")]
    [Display(Name = "Km por semana")]
    public decimal WeeklyKm { get; set; } = 350;

    [Required]
    [Range(1, 100, ErrorMessage = "Rendimiento inválido")]
    [Display(Name = "Rendimiento (km/L)")]
    public decimal FuelEfficiencyKmL { get; set; } = 10;

    [Range(0.01, 999999, ErrorMessage = "Precio inválido")]
    [Display(Name = "Precio nafta ($/L)")]
    public decimal? FuelPricePerLiter { get; set; }

    [Range(0, 9999999, ErrorMessage = "Monto inválido")]
    [Display(Name = "Seguro mensual ($)")]
    public decimal? InsuranceMonthly { get; set; }

    [Range(0, 99999999, ErrorMessage = "Monto inválido")]
    [Display(Name = "Patente anual ($)")]
    public decimal? PatenteAnnual { get; set; }

    [Range(1, 999999, ErrorMessage = "Intervalo inválido")]
    [Display(Name = "Intervalo cambio aceite (km)")]
    public decimal? OilChangeIntervalKm { get; set; }

    [Range(0, 99999999, ErrorMessage = "Monto inválido")]
    [Display(Name = "Costo estimado cambio aceite ($)")]
    public decimal? OilChangeCostEstimate { get; set; }

    [Display(Name = "Fecha último cambio")]
    public DateOnly? LastOilChangeDate { get; set; }

    [Range(0, 9999999, ErrorMessage = "Km inválidos")]
    [Display(Name = "Km en último cambio")]
    public decimal? LastOilChangeKm { get; set; }
}

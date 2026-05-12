using System.ComponentModel.DataAnnotations;

namespace OrganizaTodo.Web.Models.ViewModels;

public sealed class SavingViewModel
{
    public int Id { get; set; }

    [Required]
    [Range(0.01, 99_999_999, ErrorMessage = "Monto inválido")]
    [Display(Name = "Monto ahorrado")]
    public decimal Amount { get; set; }

    [Display(Name = "Mes")]
    public int Month { get; set; } = DateTime.Today.Month;

    [Display(Name = "Año")]
    public int Year { get; set; } = DateTime.Today.Year;

    [StringLength(200)]
    [Display(Name = "Notas")]
    public string Notes { get; set; } = string.Empty;

    [Display(Name = "Saldo preexistente")]
    public bool IsInitialBalance { get; set; }
}

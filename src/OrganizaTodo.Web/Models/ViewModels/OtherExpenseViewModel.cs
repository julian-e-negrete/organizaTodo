using System.ComponentModel.DataAnnotations;

namespace OrganizaTodo.Web.Models.ViewModels;

public sealed class OtherExpenseViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "La descripción es obligatoria")]
    [StringLength(200)]
    [Display(Name = "Descripción")]
    public string Description { get; set; } = string.Empty;

    [Required]
    [Range(0.01, 99_999_999, ErrorMessage = "Monto inválido")]
    [Display(Name = "Monto")]
    public decimal Amount { get; set; }

    public int Month { get; set; } = DateTime.Today.Month;
    public int Year { get; set; } = DateTime.Today.Year;
}

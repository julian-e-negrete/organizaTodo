using System.ComponentModel.DataAnnotations;

namespace OrganizaTodo.Web.Models.ViewModels;

public sealed class FixedLiabilityViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "El nombre es obligatorio")]
    [StringLength(100)]
    [Display(Name = "Pasivo")]
    public string Name { get; set; } = string.Empty;

    [Required]
    [Range(0.01, 99_999_999, ErrorMessage = "Monto inválido")]
    [Display(Name = "Monto mensual")]
    public decimal MonthlyAmount { get; set; }

    [Range(1, 31, ErrorMessage = "Día inválido")]
    [Display(Name = "Día de vencimiento (opcional)")]
    public int? DueDay { get; set; }
}

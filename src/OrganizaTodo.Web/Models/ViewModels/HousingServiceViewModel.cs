using System.ComponentModel.DataAnnotations;

namespace OrganizaTodo.Web.Models.ViewModels;

public sealed class HousingServiceViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "El nombre es obligatorio")]
    [StringLength(100)]
    [Display(Name = "Servicio")]
    public string Name { get; set; } = string.Empty;

    [Required]
    [Range(0.01, 99_999_999, ErrorMessage = "Monto inválido")]
    [Display(Name = "Monto")]
    public decimal Amount { get; set; }

    [Required]
    [Range(1, 31, ErrorMessage = "Día de vencimiento inválido")]
    [Display(Name = "Día de vencimiento")]
    public int DueDay { get; set; } = 1;

    [Required]
    [Display(Name = "Periodicidad")]
    public string Periodicity { get; set; } = "MONTHLY";
}

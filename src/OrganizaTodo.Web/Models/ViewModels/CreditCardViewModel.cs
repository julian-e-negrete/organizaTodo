using System.ComponentModel.DataAnnotations;

namespace OrganizaTodo.Web.Models.ViewModels;

public sealed class CreditCardViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "La descripción es obligatoria")]
    [StringLength(200)]
    [Display(Name = "Descripción")]
    public string Description { get; set; } = string.Empty;

    [Required]
    [Range(0.01, 99_999_999, ErrorMessage = "Monto inválido")]
    [Display(Name = "Monto total")]
    public decimal TotalAmount { get; set; }

    [Required]
    [Range(1, 72, ErrorMessage = "Entre 1 y 72 cuotas")]
    [Display(Name = "Cuotas")]
    public int Installments { get; set; } = 1;

    [Range(0, 100, ErrorMessage = "Interés entre 0 y 100%")]
    [Display(Name = "Interés (%)")]
    public decimal InterestRate { get; set; }

    [Required]
    [Display(Name = "Fecha de compra")]
    [DataType(DataType.Date)]
    public DateTime PurchaseDate { get; set; } = DateTime.Today;
}

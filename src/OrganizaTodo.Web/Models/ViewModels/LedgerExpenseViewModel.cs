using System.ComponentModel.DataAnnotations;
using OrganizaTodo.Web.Models.Domain;

namespace OrganizaTodo.Web.Models.ViewModels;

public sealed class LedgerExpenseViewModel : IValidatableObject
{
    [Required]
    [Range(0.01, 999_999_999, ErrorMessage = "Monto inválido")]
    [Display(Name = "Monto")]
    public decimal Amount { get; set; }

    [Required]
    [StringLength(300)]
    [Display(Name = "Descripción")]
    public string Description { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.DateTime)]
    [Display(Name = "Fecha")]
    public DateTime TransactionDate { get; set; } = DateTime.Now;

    [Display(Name = "Genera intereses")]
    public bool GeneratesInterest { get; set; }

    [Range(0.01, 100, ErrorMessage = "Tasa inválida")]
    [Display(Name = "Tasa de interés mensual (%)")]
    public decimal? InterestRate { get; set; }

    [Display(Name = "Etiquetas")]
    public int[]? SelectedTagIds { get; set; } = [];

    [Display(Name = "Nuevas etiquetas (separadas por coma)")]
    public string? NewTagNames { get; set; }

    public IEnumerable<LedgerTag> AvailableTags { get; set; } = [];

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (GeneratesInterest && (InterestRate is null || InterestRate <= 0))
        {
            yield return new ValidationResult(
                "Ingresá una tasa de interés mensual válida.",
                [nameof(InterestRate)]);
        }
    }
}

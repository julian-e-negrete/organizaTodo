using System.ComponentModel.DataAnnotations;
using OrganizaTodo.Web.Models.Domain;

namespace OrganizaTodo.Web.Models.ViewModels;

public sealed class LedgerIncomeViewModel
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

    [Display(Name = "Etiquetas")]
    public int[]? SelectedTagIds { get; set; } = [];

    [Display(Name = "Nuevas etiquetas (separadas por coma)")]
    public string? NewTagNames { get; set; }

    public IEnumerable<LedgerTag> AvailableTags { get; set; } = [];
}

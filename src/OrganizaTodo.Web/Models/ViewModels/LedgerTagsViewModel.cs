using System.ComponentModel.DataAnnotations;
using OrganizaTodo.Web.Models.Domain;

namespace OrganizaTodo.Web.Models.ViewModels;

public sealed class LedgerTagsViewModel
{
    public IEnumerable<LedgerTag> Tags { get; set; } = [];
}

public sealed class LedgerTagCreateViewModel
{
    [Required]
    [StringLength(50)]
    [Display(Name = "Nombre")]
    public string Name { get; set; } = string.Empty;
}

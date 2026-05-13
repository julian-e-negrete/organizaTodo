using OrganizaTodo.Web.Models.Domain;

namespace OrganizaTodo.Web.Models.ViewModels;

public sealed class AssetPurchaseIndexViewModel
{
    public IEnumerable<AssetPurchase> Items { get; init; } = [];
}

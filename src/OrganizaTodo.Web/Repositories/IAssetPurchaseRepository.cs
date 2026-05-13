using OrganizaTodo.Web.Models.Domain;

namespace OrganizaTodo.Web.Repositories;

public interface IAssetPurchaseRepository
{
    Task<IEnumerable<AssetPurchase>> GetByUserIdAsync(int userId);
    Task<AssetPurchase?> GetByIdAsync(int id, int userId);
    Task<int> CreateAsync(
        int userId, string assetType, decimal? quantity, decimal? unitPriceArs,
        string? exchangeType, decimal totalArs, string? notes, DateOnly purchaseDate,
        bool createExpense, string? expenseDescription, int expenseMonth, int expenseYear,
        bool createSaving, string? savingNotes, int savingMonth, int savingYear);
    Task UpdateAsync(
        int id, int userId, string assetType, decimal? quantity, decimal? unitPriceArs,
        string? exchangeType, decimal totalArs, string? notes, DateOnly purchaseDate);
    Task DeleteAsync(int id, int userId);
}

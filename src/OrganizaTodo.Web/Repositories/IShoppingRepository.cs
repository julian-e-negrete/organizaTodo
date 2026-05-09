using OrganizaTodo.Web.Models.Domain;

namespace OrganizaTodo.Web.Repositories;

public interface IShoppingRepository
{
    Task<ShoppingList> GetOrCreateListAsync(int userId, int month, int year);
    Task<IEnumerable<ShoppingListItem>> GetListItemsAsync(int shoppingListId, int userId);
    Task AddItemAsync(int shoppingListId, int userId, string productName, int quantity, decimal? estimatedPrice, string? supermarket, int priority);
    Task DeleteItemAsync(int id, int userId);
    Task<IEnumerable<ShoppingList>> GetUserListsAsync(int userId);
}

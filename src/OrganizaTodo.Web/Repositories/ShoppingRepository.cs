using Dapper;
using OrganizaTodo.Web.Data;
using OrganizaTodo.Web.Models.Domain;

namespace OrganizaTodo.Web.Repositories;

public sealed class ShoppingRepository(IDbConnectionFactory connectionFactory) : IShoppingRepository
{
    public async Task<ShoppingList> GetOrCreateListAsync(int userId, int month, int year)
    {
        using var conn = connectionFactory.CreateConnection();
        return await conn.QuerySingleAsync<ShoppingList>(
            "SELECT * FROM sp_shopping_get_or_create_list(@userId, @month, @year)",
            new { userId, month, year });
    }

    public async Task<IEnumerable<ShoppingListItem>> GetListItemsAsync(int shoppingListId, int userId)
    {
        using var conn = connectionFactory.CreateConnection();
        return await conn.QueryAsync<ShoppingListItem>(
            "SELECT * FROM sp_shopping_get_list_items(@shoppingListId, @userId)",
            new { shoppingListId, userId });
    }

    public async Task AddItemAsync(int shoppingListId, int userId, string productName, int quantity, decimal? estimatedPrice, string? supermarket, int priority)
    {
        using var conn = connectionFactory.CreateConnection();
        await conn.QueryFirstOrDefaultAsync<object>(
            "SELECT sp_shopping_add_item(@shoppingListId, @userId, @productName, @quantity, @estimatedPrice, @supermarket, @priority)",
            new { shoppingListId, userId, productName, quantity, estimatedPrice, supermarket, priority });
    }

    public async Task DeleteItemAsync(int id, int userId)
    {
        using var conn = connectionFactory.CreateConnection();
        await conn.QueryFirstOrDefaultAsync<object>(
            "SELECT sp_shopping_delete_item(@id, @userId)",
            new { id, userId });
    }

    public async Task<IEnumerable<ShoppingList>> GetUserListsAsync(int userId)
    {
        using var conn = connectionFactory.CreateConnection();
        return await conn.QueryAsync<ShoppingList>(
            "SELECT * FROM sp_shopping_get_user_lists(@userId)",
            new { userId });
    }
}

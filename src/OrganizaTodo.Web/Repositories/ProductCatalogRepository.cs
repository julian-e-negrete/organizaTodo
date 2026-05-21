using Dapper;
using OrganizaTodo.Web.Data;
using OrganizaTodo.Web.Models.Domain;

namespace OrganizaTodo.Web.Repositories;

public sealed class ProductCatalogRepository(IShoppingDbConnectionFactory connectionFactory) : IProductCatalogRepository
{
    public async Task<IEnumerable<ScrapedProduct>> SearchAsync(string query)
    {
        using var conn = connectionFactory.CreateConnection();
        return await conn.QueryAsync<ScrapedProduct>(
            "SELECT * FROM sp_products_search(@query)",
            new { query });
    }

    public async Task<IEnumerable<string>> GetCategoriesAsync()
    {
        using var conn = connectionFactory.CreateConnection();
        return await conn.QueryAsync<string>("SELECT * FROM sp_products_get_categories()");
    }

    public async Task<IEnumerable<ScrapedProduct>> GetByCategoryAsync(string category)
    {
        using var conn = connectionFactory.CreateConnection();
        return await conn.QueryAsync<ScrapedProduct>(
            "SELECT * FROM sp_products_get_by_category(@category)",
            new { category });
    }

    public async Task<IEnumerable<string>> GetParentCategoriesAsync()
    {
        using var conn = connectionFactory.CreateConnection();
        return await conn.QueryAsync<string>("SELECT * FROM sp_products_get_parent_categories()");
    }

    public async Task<IEnumerable<string>> GetSubcategoriesAsync(string parent)
    {
        using var conn = connectionFactory.CreateConnection();
        return await conn.QueryAsync<string>(
            "SELECT * FROM sp_products_get_subcategories(@p_parent)",
            new { p_parent = parent });
    }

    public async Task<IEnumerable<ScrapedProduct>> GetByParentCategoryAsync(string parent)
    {
        using var conn = connectionFactory.CreateConnection();
        return await conn.QueryAsync<ScrapedProduct>(
            "SELECT * FROM sp_products_get_by_parent_category(@p_parent)",
            new { p_parent = parent });
    }
}

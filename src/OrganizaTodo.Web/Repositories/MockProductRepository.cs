using Dapper;
using OrganizaTodo.Web.Data;
using OrganizaTodo.Web.Models.Domain;

namespace OrganizaTodo.Web.Repositories;

public sealed class MockProductRepository(IDbConnectionFactory connectionFactory) : IMockProductRepository
{
    public async Task<IEnumerable<MockProduct>> SearchAsync(string query)
    {
        using var conn = connectionFactory.CreateConnection();
        return await conn.QueryAsync<MockProduct>(
            "SELECT * FROM sp_mock_products_search(@query)",
            new { query });
    }

    public async Task<IEnumerable<string>> GetCategoriesAsync()
    {
        using var conn = connectionFactory.CreateConnection();
        return await conn.QueryAsync<string>("SELECT * FROM sp_mock_products_get_categories()");
    }

    public async Task<IEnumerable<MockProduct>> GetByCategoryAsync(string category)
    {
        using var conn = connectionFactory.CreateConnection();
        return await conn.QueryAsync<MockProduct>(
            "SELECT * FROM sp_mock_products_get_by_category(@category)",
            new { category });
    }
}

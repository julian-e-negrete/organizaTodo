using OrganizaTodo.Web.Models.Domain;

namespace OrganizaTodo.Web.Repositories;

public interface IProductCatalogRepository
{
    Task<IEnumerable<ScrapedProduct>> SearchAsync(string query);
    Task<IEnumerable<string>> GetCategoriesAsync();
    Task<IEnumerable<ScrapedProduct>> GetByCategoryAsync(string category);
}

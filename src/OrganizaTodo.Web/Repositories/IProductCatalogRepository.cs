using OrganizaTodo.Web.Models.Domain;

namespace OrganizaTodo.Web.Repositories;

public interface IProductCatalogRepository
{
    Task<IEnumerable<ScrapedProduct>> SearchAsync(string query);
    Task<IEnumerable<string>> GetCategoriesAsync();
    Task<IEnumerable<ScrapedProduct>> GetByCategoryAsync(string category);
    Task<IEnumerable<string>> GetParentCategoriesAsync();
    Task<IEnumerable<string>> GetSubcategoriesAsync(string parent);
    Task<IEnumerable<ScrapedProduct>> GetByParentCategoryAsync(string parent);
}

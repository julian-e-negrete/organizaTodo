using OrganizaTodo.Web.Models.Domain;

namespace OrganizaTodo.Web.Repositories;

public interface IMockProductRepository
{
    Task<IEnumerable<MockProduct>> SearchAsync(string query);
    Task<IEnumerable<string>> GetCategoriesAsync();
    Task<IEnumerable<MockProduct>> GetByCategoryAsync(string category);
}

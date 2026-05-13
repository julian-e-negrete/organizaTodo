using OrganizaTodo.Web.Models.Domain;

namespace OrganizaTodo.Web.Services;

public interface IDolarService
{
    Task<IReadOnlyList<DolarRate>> GetRatesAsync();
}

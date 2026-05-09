using OrganizaTodo.Web.Models.Domain;

namespace OrganizaTodo.Web.Services;

public interface IAdminService
{
    Task<AdminStats> GetStatsAsync();
}

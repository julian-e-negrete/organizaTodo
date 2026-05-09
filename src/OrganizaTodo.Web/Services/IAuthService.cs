using OrganizaTodo.Web.Models.Domain;
using OrganizaTodo.Web.Models.ViewModels;

namespace OrganizaTodo.Web.Services;

public interface IAuthService
{
    Task<(bool Success, string? Error)> RegisterAsync(RegisterViewModel model);
    Task<(bool Success, string? Error, User? User)> LoginAsync(string email, string password);
}

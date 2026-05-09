using OrganizaTodo.Web.Models.Domain;
using OrganizaTodo.Web.Models.ViewModels;
using OrganizaTodo.Web.Repositories;

namespace OrganizaTodo.Web.Services;

public sealed class AuthService(IUserRepository userRepository) : IAuthService
{
    public async Task<(bool Success, string? Error)> RegisterAsync(RegisterViewModel model)
    {
        if (await userRepository.EmailExistsAsync(model.Email))
            return (false, "El email ya está registrado.");

        var hash = BCrypt.Net.BCrypt.HashPassword(model.Password);
        await userRepository.CreateAsync(model.Email, hash, model.FullName, model.Currency);
        return (true, null);
    }

    public async Task<(bool Success, string? Error, User? User)> LoginAsync(string email, string password)
    {
        var user = await userRepository.GetByEmailAsync(email);

        if (user is null || !user.IsActive)
            return (false, "Credenciales inválidas o cuenta deshabilitada.", null);

        if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
            return (false, "Credenciales inválidas o cuenta deshabilitada.", null);

        await userRepository.UpdateLastAccessAsync(user.Id);
        return (true, null, user);
    }
}

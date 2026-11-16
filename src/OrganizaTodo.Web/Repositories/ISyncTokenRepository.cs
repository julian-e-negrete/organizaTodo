namespace OrganizaTodo.Web.Repositories;

public interface ISyncTokenRepository
{
    Task StoreTokenAsync(int userId, string tokenHash);
    Task<int?> ResolveUserIdAsync(string tokenHash);
    Task TouchLastUsedAsync(string tokenHash);
}

namespace OrganizaTodo.Web.Repositories;

public interface ISyncTokenRepository
{
    Task StoreTokenAsync(int userId, string tokenHash);
}

namespace OrganizaTodo.Web.Services;

public interface ILedgerInterestService
{
    Task<bool> EnsureInitializedAsync(int userId);
    Task<int> CatchUpInterestAsync(int userId);
}

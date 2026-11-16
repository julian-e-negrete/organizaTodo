namespace OrganizaTodo.Web.Services;

public interface ISyncTokenService
{
    (string PlaintextToken, string TokenHash) GenerateToken();
    string HashToken(string plaintextToken);
}

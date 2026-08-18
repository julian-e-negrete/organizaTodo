using System.Security.Cryptography;
using System.Text;

namespace OrganizaTodo.Web.Services;

public sealed class SyncTokenService : ISyncTokenService
{
    public (string PlaintextToken, string TokenHash) GenerateToken()
    {
        var plaintext = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        return (plaintext, HashToken(plaintext));
    }

    public string HashToken(string plaintextToken)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(plaintextToken));
        return Convert.ToHexString(bytes);
    }
}

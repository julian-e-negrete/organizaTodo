using OrganizaTodo.Web.Services;

namespace OrganizaTodo.Desktop.Tests;

public class SyncTokenServiceTests
{
    [Fact]
    public void GenerateToken_ProducesUniquePlaintextTokensAndMatchingHashes()
    {
        var service = new SyncTokenService();

        var (plaintext1, hash1) = service.GenerateToken();
        var (plaintext2, hash2) = service.GenerateToken();

        Assert.NotEqual(plaintext1, plaintext2);
        Assert.NotEqual(hash1, hash2);
        Assert.Equal(hash1, service.HashToken(plaintext1));
    }

    [Fact]
    public void HashToken_IsDeterministic_SameInputSameOutput()
    {
        var service = new SyncTokenService();

        var hashA = service.HashToken("some-fixed-token-value");
        var hashB = service.HashToken("some-fixed-token-value");

        Assert.Equal(hashA, hashB);
    }

    [Fact]
    public void HashToken_DifferentInputs_ProduceDifferentHashes()
    {
        var service = new SyncTokenService();

        Assert.NotEqual(service.HashToken("token-a"), service.HashToken("token-b"));
    }
}

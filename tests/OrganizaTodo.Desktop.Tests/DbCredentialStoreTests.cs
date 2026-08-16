using OrganizaTodo.Desktop.Lifecycle;

namespace OrganizaTodo.Desktop.Tests;

public class DbCredentialStoreTests
{
    [Fact]
    public void LoadOrCreate_FirstCall_PersistsCredentialsToDisk()
    {
        var configDir = Directory.CreateTempSubdirectory("organizatodo-creds-test-").FullName;
        try
        {
            var store = new DbCredentialStore(configDir);

            var credentials = store.LoadOrCreate();

            Assert.False(string.IsNullOrWhiteSpace(credentials.Username));
            Assert.False(string.IsNullOrWhiteSpace(credentials.Password));
            Assert.True(File.Exists(Path.Combine(configDir, "db-credentials.json")));
        }
        finally
        {
            Directory.Delete(configDir, recursive: true);
        }
    }

    [Fact]
    public void LoadOrCreate_SecondCall_ReturnsSameCredentials()
    {
        var configDir = Directory.CreateTempSubdirectory("organizatodo-creds-test-").FullName;
        try
        {
            var first = new DbCredentialStore(configDir).LoadOrCreate();
            var second = new DbCredentialStore(configDir).LoadOrCreate();

            Assert.Equal(first, second);
        }
        finally
        {
            Directory.Delete(configDir, recursive: true);
        }
    }
}

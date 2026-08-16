using OrganizaTodo.Desktop.Lifecycle;

namespace OrganizaTodo.Desktop.Tests;

public class AppPathsTests
{
    [Fact]
    public void From_ComposesSubdirectoriesUnderOrganizaTodoFolder()
    {
        var paths = AppPaths.From("/fake/root");

        Assert.Equal(Path.Combine("/fake/root", "organizatodo"), paths.DataDir);
        Assert.Equal(Path.Combine("/fake/root", "organizatodo", "pgdata"), paths.PgDataDir);
        Assert.Equal(Path.Combine("/fake/root", "organizatodo", "logs"), paths.LogsDir);
        Assert.Equal(Path.Combine("/fake/root", "organizatodo", "config"), paths.ConfigDir);
    }

    [Fact]
    public void EnsureDirectoriesExist_CreatesAllFourDirectories()
    {
        var root = Directory.CreateTempSubdirectory("organizatodo-apppaths-test-").FullName;
        try
        {
            var paths = AppPaths.From(root);
            paths.EnsureDirectoriesExist();

            Assert.True(Directory.Exists(paths.DataDir));
            Assert.True(Directory.Exists(paths.PgDataDir));
            Assert.True(Directory.Exists(paths.LogsDir));
            Assert.True(Directory.Exists(paths.ConfigDir));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}

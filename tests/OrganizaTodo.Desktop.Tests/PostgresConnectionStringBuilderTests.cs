using OrganizaTodo.Desktop.Lifecycle;

namespace OrganizaTodo.Desktop.Tests;

public class PostgresConnectionStringBuilderTests
{
    [Fact]
    public void Build_ProducesLoopbackConnectionString()
    {
        var result = PostgresConnectionStringBuilder.Build(55432, "organizatodo", "app_user", "s3cret");

        Assert.Equal(
            "Host=127.0.0.1;Port=55432;Database=organizatodo;Username=app_user;Password=s3cret;Ssl Mode=Disable",
            result);
    }
}

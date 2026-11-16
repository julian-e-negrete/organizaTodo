namespace OrganizaTodo.Desktop.Tests;

public class WebProgramTests
{
    [Fact]
    public void BuildApp_ConfigureDelegate_OverridesConnectionStringsAndDesktopFlag()
    {
        var app = OrganizaTodo.Web.Program.BuildApp(Array.Empty<string>(), builder =>
        {
            builder.Configuration["ConnectionStrings:DefaultConnection"] = "Host=test;Port=1;Database=d;Username=u;Password=p";
            builder.Configuration["ConnectionStrings:ShoppingConnection"] = "Host=test;Port=1;Database=s;Username=u;Password=p";
            builder.Configuration["Desktop:IsHosted"] = "true";
        });

        Assert.Equal(
            "Host=test;Port=1;Database=d;Username=u;Password=p",
            app.Configuration["ConnectionStrings:DefaultConnection"]);
        Assert.Equal("true", app.Configuration["Desktop:IsHosted"]);
    }
}

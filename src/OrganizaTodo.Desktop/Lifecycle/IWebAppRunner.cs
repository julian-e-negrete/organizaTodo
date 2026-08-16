namespace OrganizaTodo.Desktop.Lifecycle;

public interface IWebAppRunner
{
    Task<string> StartAsync(string defaultConnectionString, string shoppingConnectionString, CancellationToken ct = default);
    Task StopAsync(CancellationToken ct = default);
}

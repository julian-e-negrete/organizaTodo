using OrganizaTodo.Web.Repositories;

namespace OrganizaTodo.Web.Services;

public sealed class LedgerInterestBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<LedgerInterestBackgroundService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(12);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            using var scope = scopeFactory.CreateScope();
            var userRepo = scope.ServiceProvider.GetRequiredService<IUserRepository>();
            var interestService = scope.ServiceProvider.GetRequiredService<ILedgerInterestService>();

            foreach (var user in (await userRepo.GetAllAsync()).Where(u => u.IsActive))
            {
                try
                {
                    await interestService.CatchUpInterestAsync(user.Id);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Interest catch-up failed for user {UserId}", user.Id);
                }
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }
}

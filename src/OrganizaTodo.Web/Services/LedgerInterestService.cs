using OrganizaTodo.Web.Repositories;

namespace OrganizaTodo.Web.Services;

public sealed class LedgerInterestService(ILedgerRepository ledgerRepo, ISavingRepository savingRepo) : ILedgerInterestService
{
    public async Task<bool> EnsureInitializedAsync(int userId)
    {
        var count = await ledgerRepo.GetTransactionCountAsync(userId);
        if (count > 0) return false;

        var cumulative = await savingRepo.GetCumulativeTotalAsync(userId);
        await ledgerRepo.CreateInitialAsync(userId, cumulative, DateTime.Now);
        return true;
    }

    public async Task<int> CatchUpInterestAsync(int userId)
    {
        var (lastInterestDate, firstAnchorExpenseDate) = await ledgerRepo.GetInterestPointersAsync(userId);
        if (firstAnchorExpenseDate is null) return 0;

        var cursor = lastInterestDate.HasValue
            ? FirstDayOfMonth(lastInterestDate.Value).AddMonths(1)
            : FirstDayOfMonth(firstAnchorExpenseDate.Value);

        var currentMonthStart = FirstDayOfMonth(DateTime.Today);
        var created = 0;

        while (cursor < currentMonthStart)
        {
            var monthEnd = cursor.AddMonths(1).AddTicks(-1);
            var anchor = await ledgerRepo.GetLatestAnchorExpenseAsync(userId, monthEnd);
            if (anchor is null)
            {
                cursor = cursor.AddMonths(1);
                continue;
            }

            var balance = await ledgerRepo.GetBalanceAsOfAsync(userId, monthEnd);
            if (balance >= 0)
            {
                cursor = cursor.AddMonths(1);
                continue;
            }

            var interestAmount = Math.Round(Math.Abs(balance) * anchor.Value.InterestRate / 100m, 2);
            var transactionDate = FirstDayOfMonth(cursor.AddMonths(1));
            await ledgerRepo.CreateInterestAsync(userId, interestAmount, transactionDate, anchor.Value.InterestRate, anchor.Value.Id, balance);
            created++;
            cursor = cursor.AddMonths(1);
        }

        return created;
    }

    private static DateTime FirstDayOfMonth(DateTime date) => new(date.Year, date.Month, 1);
}

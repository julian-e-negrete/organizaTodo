using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrganizaTodo.Web.Extensions;
using OrganizaTodo.Web.Models.Domain;
using OrganizaTodo.Web.Models.ViewModels;
using OrganizaTodo.Web.Repositories;
using OrganizaTodo.Web.Services;

namespace OrganizaTodo.Web.Controllers;

[Authorize]
public sealed class LedgerController(ILedgerRepository repo, ILedgerInterestService interestService) : Controller
{
    public async Task<IActionResult> Index()
    {
        var userId = User.GetUserId();

        await interestService.EnsureInitializedAsync(userId);
        await interestService.CatchUpInterestAsync(userId);

        var balance = await repo.GetCurrentBalanceAsync(userId);
        var (recent, _) = await repo.GetHistoryAsync(userId, new LedgerHistoryFilter(), page: 1, pageSize: 10);
        var series = await repo.GetBalanceSeriesAsync(userId);

        return View(new LedgerIndexViewModel
        {
            CurrentBalance = balance,
            RecentTransactions = recent,
            BalanceSeries = series
        });
    }

    public async Task<IActionResult> CreateExpense()
    {
        return View(new LedgerExpenseViewModel
        {
            AvailableTags = await repo.GetTagsByUserIdAsync(User.GetUserId())
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateExpense(LedgerExpenseViewModel vm)
    {
        var userId = User.GetUserId();
        if (!ModelState.IsValid)
        {
            vm.AvailableTags = await repo.GetTagsByUserIdAsync(userId);
            return View(vm);
        }

        var id = await repo.CreateExpenseAsync(
            userId, vm.Amount, vm.Description, vm.TransactionDate, vm.GeneratesInterest, vm.InterestRate);
        await LinkTagsAsync(id, userId, vm.SelectedTagIds, vm.NewTagNames);

        TempData["Success"] = "Gasto registrado.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> CreateIncome()
    {
        return View(new LedgerIncomeViewModel
        {
            AvailableTags = await repo.GetTagsByUserIdAsync(User.GetUserId())
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateIncome(LedgerIncomeViewModel vm)
    {
        var userId = User.GetUserId();
        if (!ModelState.IsValid)
        {
            vm.AvailableTags = await repo.GetTagsByUserIdAsync(userId);
            return View(vm);
        }

        var id = await repo.CreateIncomeAsync(userId, vm.Amount, vm.Description, vm.TransactionDate);
        await LinkTagsAsync(id, userId, vm.SelectedTagIds, vm.NewTagNames);

        TempData["Success"] = "Ingreso registrado.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> History(LedgerHistoryViewModel vm)
    {
        var userId = User.GetUserId();
        if (vm.Page < 1) vm.Page = 1;
        if (vm.PageSize is < 1 or > 100) vm.PageSize = 25;

        var filter = BuildFilter(vm);

        if (!string.IsNullOrEmpty(vm.GroupBy))
        {
            var (allItems, totalCount) = await repo.GetHistoryAsync(userId, filter, page: 1, pageSize: 5000);
            vm.TotalCount = totalCount;
            vm.Groups = GroupTransactions(allItems, vm.GroupBy);
        }
        else
        {
            var (items, totalCount) = await repo.GetHistoryAsync(userId, filter, vm.Page, vm.PageSize);
            vm.Items = items;
            vm.TotalCount = totalCount;
        }

        vm.AvailableTags = await repo.GetTagsByUserIdAsync(userId);

        return View(vm);
    }

    public async Task<IActionResult> ExportCsv(LedgerHistoryViewModel vm)
    {
        var userId = User.GetUserId();
        var filter = BuildFilter(vm);

        var (items, _) = await repo.GetHistoryAsync(userId, filter, page: 1, pageSize: 10000);

        var sb = new StringBuilder();
        sb.AppendLine("Fecha,Tipo,Descripción,Etiquetas,Monto,Saldo");
        foreach (var item in items)
        {
            sb.AppendLine(string.Join(',',
                item.TransactionDate.ToString("yyyy-MM-dd HH:mm"),
                item.Type,
                CsvField(item.Description),
                CsvField(string.Join("; ", item.TagList)),
                item.Amount.ToString("F2", CultureInfo.InvariantCulture),
                item.BalanceAfter.ToString("F2", CultureInfo.InvariantCulture)));
        }

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        return File(bytes, "text/csv", $"ledger_{DateTime.Now:yyyyMMddHHmmss}.csv");
    }

    private static LedgerHistoryFilter BuildFilter(LedgerHistoryViewModel vm) => new()
    {
        DateFrom = vm.DateFrom,
        DateTo = vm.DateTo,
        Type = vm.Type,
        TagIds = vm.TagIds,
        AmountMin = vm.AmountMin,
        AmountMax = vm.AmountMax,
        OnlyWithInterest = vm.OnlyWithInterest,
        OnlyNegativeBalance = vm.OnlyNegativeBalance,
        Search = vm.Search
    };

    private static string CsvField(string value) =>
        value.Contains(',') || value.Contains('"') || value.Contains('\n')
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;

    private static IEnumerable<LedgerHistoryGroup> GroupTransactions(IEnumerable<LedgerTransaction> items, string groupBy)
    {
        DateTime PeriodStart(DateTime d) => groupBy switch
        {
            "week" => d.Date.AddDays(-(int)(7 + (d.DayOfWeek - DayOfWeek.Monday)) % 7),
            "month" => new DateTime(d.Year, d.Month, 1),
            "year" => new DateTime(d.Year, 1, 1),
            _ => d.Date
        };

        var culture = new CultureInfo("es-AR");
        string Label(DateTime periodStart) => groupBy switch
        {
            "week" => $"Semana del {periodStart:dd/MM/yyyy}",
            "month" => culture.TextInfo.ToTitleCase(periodStart.ToString("MMMM yyyy", culture)),
            "year" => periodStart.Year.ToString(),
            _ => periodStart.ToString("dd/MM/yyyy")
        };

        return items
            .GroupBy(t => PeriodStart(t.TransactionDate))
            .OrderByDescending(g => g.Key)
            .Select(g => new LedgerHistoryGroup
            {
                Label = Label(g.Key),
                PeriodStart = g.Key,
                Items = g.OrderByDescending(t => t.TransactionDate).ThenByDescending(t => t.Id).ToList(),
                Subtotal = g.Sum(t => t.Amount),
                Count = g.Count()
            });
    }

    [HttpGet]
    public async Task<IActionResult> TransactionDetail(int id)
    {
        var userId = User.GetUserId();
        var transaction = await repo.GetDetailAsync(id, userId);
        if (transaction is null) return NotFound();

        var children = transaction is { Type: "expense", GeneratesInterest: true }
            ? await repo.GetChildrenAsync(id, userId)
            : [];

        return PartialView("_TransactionDetail", new LedgerTransactionDetailViewModel
        {
            Transaction = transaction,
            ChildInterestTransactions = children
        });
    }

    public async Task<IActionResult> Tags()
    {
        return View(new LedgerTagsViewModel
        {
            Tags = await repo.GetTagsByUserIdAsync(User.GetUserId())
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Tags(LedgerTagCreateViewModel vm)
    {
        var userId = User.GetUserId();
        if (!ModelState.IsValid)
        {
            return View(new LedgerTagsViewModel { Tags = await repo.GetTagsByUserIdAsync(userId) });
        }

        await repo.GetOrCreateTagAsync(userId, vm.Name);
        TempData["Success"] = "Etiqueta creada.";
        return RedirectToAction(nameof(Tags));
    }

    private async Task LinkTagsAsync(int transactionId, int userId, int[]? selectedTagIds, string? newTagNames)
    {
        foreach (var tagId in (selectedTagIds ?? []).Distinct())
            await repo.LinkTagAsync(transactionId, tagId);

        if (string.IsNullOrWhiteSpace(newTagNames)) return;

        foreach (var name in newTagNames.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var tag = await repo.GetOrCreateTagAsync(userId, name);
            await repo.LinkTagAsync(transactionId, tag.Id);
        }
    }
}

using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrganizaTodo.Web.Extensions;
using OrganizaTodo.Web.Models.ViewModels;
using OrganizaTodo.Web.Services;

namespace OrganizaTodo.Web.Controllers;

[Authorize]
public sealed class ReportsController(IBalanceService balanceService) : Controller
{
    public async Task<IActionResult> Index(int? year)
    {
        var y      = year ?? DateTime.Today.Year;
        var userId = User.GetUserId();

        var history = new List<MonthlyBalanceSummary>();
        for (var m = 1; m <= 12; m++)
        {
            var balance = await balanceService.GetMonthlyBalanceAsync(userId, m, y);
            history.Add(new MonthlyBalanceSummary { Month = m, Year = y, Balance = balance });
        }

        return View(new ReportViewModel { History = history, SelectedYear = y });
    }

    public async Task<IActionResult> ExportCsv(int? year)
    {
        var y      = year ?? DateTime.Today.Year;
        var userId = User.GetUserId();

        var sb = new StringBuilder();
        sb.AppendLine("Mes,Año,Ingresos,Servicios,Tarjeta,Pasivos Fijos,Total Pasivos,Otros Egresos,Saldo Restante");

        for (var m = 1; m <= 12; m++)
        {
            var b = await balanceService.GetMonthlyBalanceAsync(userId, m, y);
            var monthName = new DateTime(y, m, 1).ToString("MMMM",
                new System.Globalization.CultureInfo("es-AR"));
            sb.AppendLine($"{monthName},{y},{b.TotalIncome},{b.TotalServices},{b.TotalCreditCard},{b.TotalFixedLiabilities},{b.TotalLiabilities},{b.TotalOtherExpenses},{b.RemainingBalance}");
        }

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        return File(bytes, "text/csv", $"balance_{y}.csv");
    }
}

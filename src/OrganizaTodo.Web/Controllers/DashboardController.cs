using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrganizaTodo.Web.Extensions;
using OrganizaTodo.Web.Models.ViewModels;
using OrganizaTodo.Web.Repositories;
using OrganizaTodo.Web.Services;

namespace OrganizaTodo.Web.Controllers;

[Authorize]
public sealed class DashboardController(IBalanceService balanceService, IHousingServiceRepository housingRepo) : Controller
{
    public async Task<IActionResult> Index(int? month, int? year)
    {
        var now = DateTime.Today;
        var m = month ?? now.Month;
        var y = year ?? now.Year;

        var balance  = await balanceService.GetMonthlyBalanceAsync(User.GetUserId(), m, y);
        var services = await housingRepo.GetByUserIdAsync(User.GetUserId());

        var vm = new DashboardViewModel
        {
            Balance        = balance,
            Month          = m,
            Year           = y,
            UnpaidServices = services.Where(s => !s.IsPaid)
        };

        return View(vm);
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrganizaTodo.Web.Extensions;
using OrganizaTodo.Web.Models.ViewModels;
using OrganizaTodo.Web.Repositories;
using OrganizaTodo.Web.Services;

namespace OrganizaTodo.Web.Controllers;

[Authorize]
public sealed class DashboardController(
    IBalanceService balanceService,
    IHousingServiceRepository housingRepo,
    IFixedLiabilityRepository liabilityRepo,
    IVehicleRepository vehicleRepo) : Controller
{
    public async Task<IActionResult> Index(int? month, int? year)
    {
        var now    = DateTime.Today;
        var m      = month ?? now.Month;
        var y      = year  ?? now.Year;
        var userId = User.GetUserId();

        var (balance, services, liabilities, vehicles) = (
            await balanceService.GetMonthlyBalanceAsync(userId, m, y),
            await housingRepo.GetByUserIdAsync(userId),
            await liabilityRepo.GetByUserIdAsync(userId),
            await vehicleRepo.GetByUserIdAsync(userId, m, y)
        );

        return View(new DashboardViewModel
        {
            Balance           = balance,
            Month             = m,
            Year              = y,
            UnpaidServices    = services.Where(s => !s.IsPaid),
            UnpaidLiabilities = liabilities.Where(l => !l.IsPaid),
            VehiclesWithCosts = vehicles
        });
    }
}

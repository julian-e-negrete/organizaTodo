using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrganizaTodo.Web.Extensions;
using OrganizaTodo.Web.Models.ViewModels;
using OrganizaTodo.Web.Repositories;

namespace OrganizaTodo.Web.Controllers;

[Authorize]
public sealed class ExpensesController(
    IOtherExpenseRepository repo,
    IAssetPurchaseRepository assetRepo,
    IVehicleRepository vehicleRepo) : Controller
{
    public async Task<IActionResult> Index(int? month, int? year)
    {
        var m      = month ?? DateTime.Today.Month;
        var y      = year  ?? DateTime.Today.Year;
        var userId = User.GetUserId();

        var items          = await repo.GetByUserIdAndPeriodAsync(userId, m, y);
        var assetPurchases = await assetRepo.GetByUserIdAsync(userId);

        var assetByExpenseId = assetPurchases
            .Where(ap => ap.ExpenseId.HasValue)
            .GroupBy(ap => ap.ExpenseId!.Value)
            .ToDictionary(g => g.Key, g => g.First());

        return View(new OtherExpenseIndexViewModel
        {
            Items                    = items,
            Month                    = m,
            Year                     = y,
            AssetPurchaseByExpenseId = assetByExpenseId
        });
    }

    public async Task<IActionResult> Create()
    {
        await PopulateVehiclesAsync();
        return View(new OtherExpenseViewModel());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(OtherExpenseViewModel vm)
    {
        if (!ModelState.IsValid) { await PopulateVehiclesAsync(); return View(vm); }
        await repo.CreateAsync(User.GetUserId(), vm.Description, vm.Amount, vm.Month, vm.Year, vm.VehicleId);
        TempData["Success"] = "Egreso registrado.";
        return RedirectToAction(nameof(Index), new { vm.Month, vm.Year });
    }

    public async Task<IActionResult> Edit(int id)
    {
        var item = await repo.GetByIdAsync(id, User.GetUserId());
        if (item is null) return NotFound();
        await PopulateVehiclesAsync();
        return View(new OtherExpenseViewModel
        {
            Id = item.Id, Description = item.Description, Amount = item.Amount,
            Month = item.Month, Year = item.Year, VehicleId = item.VehicleId
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(OtherExpenseViewModel vm)
    {
        if (!ModelState.IsValid) { await PopulateVehiclesAsync(); return View(vm); }
        await repo.UpdateAsync(vm.Id, User.GetUserId(), vm.Description, vm.Amount, vm.VehicleId);
        TempData["Success"] = "Egreso actualizado.";
        return RedirectToAction(nameof(Index), new { vm.Month, vm.Year });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, int month, int year)
    {
        await repo.DeleteAsync(id, User.GetUserId());
        TempData["Success"] = "Egreso eliminado.";
        return RedirectToAction(nameof(Index), new { month, year });
    }

    private async Task PopulateVehiclesAsync()
    {
        var vehicles = await vehicleRepo.GetByUserIdAsync(
            User.GetUserId(), DateTime.Today.Month, DateTime.Today.Year);
        ViewBag.Vehicles = new SelectList(
            vehicles.Select(v => new { v.Id, Name = $"{v.Make} {v.Model} ({v.Plate})" }),
            "Id", "Name");
    }
}

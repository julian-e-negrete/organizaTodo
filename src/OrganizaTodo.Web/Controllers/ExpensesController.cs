using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrganizaTodo.Web.Extensions;
using OrganizaTodo.Web.Models.ViewModels;
using OrganizaTodo.Web.Repositories;

namespace OrganizaTodo.Web.Controllers;

[Authorize]
public sealed class ExpensesController(IOtherExpenseRepository repo, IAssetPurchaseRepository assetRepo) : Controller
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

        var vm = new OtherExpenseIndexViewModel
        {
            Items                   = items,
            Month                   = m,
            Year                    = y,
            AssetPurchaseByExpenseId = assetByExpenseId
        };
        return View(vm);
    }

    public IActionResult Create() => View(new OtherExpenseViewModel());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(OtherExpenseViewModel vm)
    {
        if (!ModelState.IsValid) return View(vm);
        await repo.CreateAsync(User.GetUserId(), vm.Description, vm.Amount, vm.Month, vm.Year);
        TempData["Success"] = "Egreso registrado.";
        return RedirectToAction(nameof(Index), new { vm.Month, vm.Year });
    }

    public async Task<IActionResult> Edit(int id)
    {
        var item = await repo.GetByIdAsync(id, User.GetUserId());
        if (item is null) return NotFound();
        return View(new OtherExpenseViewModel
        {
            Id = item.Id, Description = item.Description, Amount = item.Amount,
            Month = item.Month, Year = item.Year
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(OtherExpenseViewModel vm)
    {
        if (!ModelState.IsValid) return View(vm);
        await repo.UpdateAsync(vm.Id, User.GetUserId(), vm.Description, vm.Amount);
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
}

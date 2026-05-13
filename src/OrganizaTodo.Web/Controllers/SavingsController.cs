using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrganizaTodo.Web.Extensions;
using OrganizaTodo.Web.Models.ViewModels;
using OrganizaTodo.Web.Repositories;

namespace OrganizaTodo.Web.Controllers;

[Authorize]
public sealed class SavingsController(ISavingRepository repo, IAssetPurchaseRepository assetRepo) : Controller
{
    public async Task<IActionResult> Index()
    {
        var userId = User.GetUserId();
        var items          = await repo.GetByUserIdAsync(userId);
        var cumulative     = await repo.GetCumulativeTotalAsync(userId);
        var average        = await repo.GetMonthlyAverageAsync(userId);
        var assetPurchases = await assetRepo.GetByUserIdAsync(userId);

        var assetBySavingId = assetPurchases
            .Where(ap => ap.SavingId.HasValue)
            .GroupBy(ap => ap.SavingId!.Value)
            .ToDictionary(g => g.Key, g => g.First());

        return View(new SavingsIndexViewModel
        {
            Items               = items,
            CumulativeTotal     = cumulative,
            MonthlyAverage      = average,
            AssetPurchaseBySavingId = assetBySavingId
        });
    }

    public IActionResult Create() => View(new SavingViewModel());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(SavingViewModel vm)
    {
        if (!ModelState.IsValid) return View(vm);
        await repo.CreateAsync(User.GetUserId(), vm.Amount, vm.Month, vm.Year, vm.Notes, vm.IsInitialBalance);
        TempData["Success"] = "Ahorro registrado.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var item = await repo.GetByIdAsync(id, User.GetUserId());
        if (item is null) return NotFound();
        return View(new SavingViewModel
        {
            Id = item.Id, Amount = item.Amount,
            Month = item.Month, Year = item.Year, Notes = item.Notes
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(SavingViewModel vm)
    {
        if (!ModelState.IsValid) return View(vm);
        await repo.UpdateAsync(vm.Id, User.GetUserId(), vm.Amount, vm.Notes);
        TempData["Success"] = "Ahorro actualizado.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        await repo.DeleteAsync(id, User.GetUserId());
        TempData["Success"] = "Ahorro eliminado.";
        return RedirectToAction(nameof(Index));
    }
}

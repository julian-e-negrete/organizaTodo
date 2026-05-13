using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrganizaTodo.Web.Extensions;
using OrganizaTodo.Web.Models.ViewModels;
using OrganizaTodo.Web.Repositories;
using OrganizaTodo.Web.Services;

namespace OrganizaTodo.Web.Controllers;

[Authorize]
public sealed class AssetPurchasesController(
    IAssetPurchaseRepository repo,
    IDolarService dolarService) : Controller
{
    public async Task<IActionResult> Index()
    {
        var items = await repo.GetByUserIdAsync(User.GetUserId());
        return View(new AssetPurchaseIndexViewModel { Items = items });
    }

    public IActionResult Create() => View(new AssetPurchaseViewModel());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(AssetPurchaseViewModel vm)
    {
        if (!ModelState.IsValid) return View(vm);

        if (vm.CreateExpense && string.IsNullOrWhiteSpace(vm.ExpenseDescription))
            vm.ExpenseDescription = DefaultDescription(vm);

        await repo.CreateAsync(
            User.GetUserId(),
            vm.AssetType, vm.Quantity, vm.UnitPriceArs, vm.ExchangeType,
            vm.TotalArs, vm.Notes, vm.PurchaseDate,
            vm.CreateExpense, vm.ExpenseDescription, vm.ExpenseMonth, vm.ExpenseYear,
            vm.CreateSaving, vm.SavingNotes, vm.SavingMonth, vm.SavingYear);

        TempData["Success"] = "Compra de activo registrada.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var item = await repo.GetByIdAsync(id, User.GetUserId());
        if (item is null) return NotFound();

        return View(new AssetPurchaseViewModel
        {
            Id           = item.Id,
            AssetType    = item.AssetType,
            Quantity     = item.Quantity,
            UnitPriceArs = item.UnitPriceArs,
            ExchangeType = item.ExchangeType,
            TotalArs     = item.TotalArs,
            Notes        = item.Notes,
            PurchaseDate = item.PurchaseDate,
            CreateExpense = false,
            CreateSaving  = false
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(AssetPurchaseViewModel vm)
    {
        if (!ModelState.IsValid) return View(vm);

        await repo.UpdateAsync(
            vm.Id, User.GetUserId(),
            vm.AssetType, vm.Quantity, vm.UnitPriceArs, vm.ExchangeType,
            vm.TotalArs, vm.Notes, vm.PurchaseDate);

        TempData["Success"] = "Compra actualizada.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        await repo.DeleteAsync(id, User.GetUserId());
        TempData["Success"] = "Compra eliminada.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> GetDolarRates()
    {
        var rates = await dolarService.GetRatesAsync();
        return Json(rates.Select(r => new { r.Casa, r.Nombre, r.Compra, r.Venta }));
    }

    private static string DefaultDescription(AssetPurchaseViewModel vm) => vm.AssetType switch
    {
        "USD"    => vm.Quantity.HasValue ? $"Compra USD {vm.Quantity:0.##}" : "Compra USD",
        "ACCION" => "Compra de acción",
        "CRYPTO" => "Compra de crypto",
        _        => "Compra de activo"
    };
}

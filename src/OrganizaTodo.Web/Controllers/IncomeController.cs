using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrganizaTodo.Web.Extensions;
using OrganizaTodo.Web.Models.ViewModels;
using OrganizaTodo.Web.Repositories;

namespace OrganizaTodo.Web.Controllers;

[Authorize]
public sealed class IncomeController(IIncomeRepository repo, IRecurringIncomeRepository recurringRepo) : Controller
{
    public async Task<IActionResult> Index(int? month, int? year)
    {
        var m = month ?? DateTime.Today.Month;
        var y = year ?? DateTime.Today.Year;
        var vm = new IncomeIndexViewModel
        {
            Items          = await repo.GetByUserIdAndPeriodAsync(User.GetUserId(), m, y),
            RecurringItems = await recurringRepo.GetByUserIdAsync(User.GetUserId()),
            Month = m,
            Year  = y
        };
        return View(vm);
    }

    // ── Variable income (one-off per month) ──────────────────────────────────

    public IActionResult Create() => View(new IncomeViewModel());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(IncomeViewModel vm)
    {
        if (!ModelState.IsValid) return View(vm);
        await repo.CreateAsync(User.GetUserId(), vm.Description, vm.Amount, vm.Category, vm.Month, vm.Year);
        TempData["Success"] = "Ingreso registrado.";
        return RedirectToAction(nameof(Index), new { vm.Month, vm.Year });
    }

    public async Task<IActionResult> Edit(int id)
    {
        var item = await repo.GetByIdAsync(id, User.GetUserId());
        if (item is null) return NotFound();
        return View(new IncomeViewModel
        {
            Id = item.Id, Description = item.Description, Amount = item.Amount,
            Category = item.Category, Month = item.Month, Year = item.Year
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(IncomeViewModel vm)
    {
        if (!ModelState.IsValid) return View(vm);
        await repo.UpdateAsync(vm.Id, User.GetUserId(), vm.Description, vm.Amount, vm.Category);
        TempData["Success"] = "Ingreso actualizado.";
        return RedirectToAction(nameof(Index), new { vm.Month, vm.Year });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, int month, int year)
    {
        await repo.DeleteAsync(id, User.GetUserId());
        TempData["Success"] = "Ingreso eliminado.";
        return RedirectToAction(nameof(Index), new { month, year });
    }

    // ── Recurring income (salary / fixed) ────────────────────────────────────

    public IActionResult CreateRecurring() => View(new RecurringIncomeViewModel());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateRecurring(RecurringIncomeViewModel vm)
    {
        if (!ModelState.IsValid) return View(vm);
        await recurringRepo.CreateAsync(User.GetUserId(), vm.Description, vm.Amount);
        TempData["Success"] = "Ingreso fijo registrado.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> EditRecurring(int id)
    {
        var item = await recurringRepo.GetByIdAsync(id, User.GetUserId());
        if (item is null) return NotFound();
        return View(new RecurringIncomeViewModel
        {
            Id = item.Id, Description = item.Description, Amount = item.Amount
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> EditRecurring(RecurringIncomeViewModel vm)
    {
        if (!ModelState.IsValid) return View(vm);
        await recurringRepo.UpdateAsync(vm.Id, User.GetUserId(), vm.Description, vm.Amount);
        TempData["Success"] = "Ingreso fijo actualizado.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleRecurring(int id)
    {
        await recurringRepo.ToggleActiveAsync(id, User.GetUserId());
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteRecurring(int id)
    {
        await recurringRepo.DeleteAsync(id, User.GetUserId());
        TempData["Success"] = "Ingreso fijo eliminado.";
        return RedirectToAction(nameof(Index));
    }
}

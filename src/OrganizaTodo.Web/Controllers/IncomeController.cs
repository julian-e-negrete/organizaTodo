using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrganizaTodo.Web.Extensions;
using OrganizaTodo.Web.Models.ViewModels;
using OrganizaTodo.Web.Repositories;

namespace OrganizaTodo.Web.Controllers;

[Authorize]
public sealed class IncomeController(IIncomeRepository repo) : Controller
{
    public async Task<IActionResult> Index(int? month, int? year)
    {
        var m = month ?? DateTime.Today.Month;
        var y = year ?? DateTime.Today.Year;
        var vm = new IncomeIndexViewModel
        {
            Items = await repo.GetByUserIdAndPeriodAsync(User.GetUserId(), m, y),
            Month = m,
            Year  = y
        };
        return View(vm);
    }

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
}

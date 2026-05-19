using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrganizaTodo.Web.Extensions;
using OrganizaTodo.Web.Models.ViewModels;
using OrganizaTodo.Web.Repositories;

namespace OrganizaTodo.Web.Controllers;

[Authorize]
public sealed class CreditCardController(ICreditCardRepository repo) : Controller
{
    public async Task<IActionResult> Index(int? month, int? year)
    {
        var m = month ?? DateTime.Today.Month;
        var y = year ?? DateTime.Today.Year;
        var items = await repo.GetByUserIdAsync(User.GetUserId(), m, y);
        return View(new CreditCardIndexViewModel { Items = items, Month = m, Year = y });
    }

    public IActionResult Create(int? month, int? year)
    {
        var vm = new CreditCardViewModel();
        if (month.HasValue) vm.DueMonth = month.Value;
        if (year.HasValue)  vm.DueYear  = year.Value;
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreditCardViewModel vm)
    {
        if (!ModelState.IsValid) return View(vm);
        await repo.CreateAsync(User.GetUserId(), vm.Description, vm.TotalAmount,
            vm.Installments, vm.InterestRate, vm.PurchaseDate, vm.DueMonth, vm.DueYear);
        TempData["Success"] = "Compra registrada.";
        return RedirectToAction(nameof(Index), new { month = vm.DueMonth, year = vm.DueYear });
    }

    public async Task<IActionResult> Edit(int id)
    {
        var item = await repo.GetByIdAsync(id, User.GetUserId());
        if (item is null) return NotFound();
        return View(new CreditCardViewModel
        {
            Id = item.Id, Description = item.Description, TotalAmount = item.TotalAmount,
            Installments = item.Installments, InterestRate = item.InterestRate,
            PurchaseDate = item.PurchaseDate, DueMonth = item.DueMonth, DueYear = item.DueYear
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(CreditCardViewModel vm)
    {
        if (!ModelState.IsValid) return View(vm);
        await repo.UpdateAsync(vm.Id, User.GetUserId(), vm.Description, vm.TotalAmount,
            vm.Installments, vm.InterestRate, vm.DueMonth, vm.DueYear);
        TempData["Success"] = "Compra actualizada.";
        return RedirectToAction(nameof(Index), new { month = vm.DueMonth, year = vm.DueYear });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, int month, int year)
    {
        await repo.DeleteAsync(id, User.GetUserId());
        TempData["Success"] = "Compra eliminada.";
        return RedirectToAction(nameof(Index), new { month, year });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AdvanceInstallment(int id, int month, int year)
    {
        await repo.AdvanceInstallmentAsync(id, User.GetUserId());
        TempData["Success"] = "Cuota marcada como pagada.";
        return RedirectToAction(nameof(Index), new { month, year });
    }
}

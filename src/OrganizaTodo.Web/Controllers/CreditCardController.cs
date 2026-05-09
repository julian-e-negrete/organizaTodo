using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrganizaTodo.Web.Extensions;
using OrganizaTodo.Web.Models.ViewModels;
using OrganizaTodo.Web.Repositories;

namespace OrganizaTodo.Web.Controllers;

[Authorize]
public sealed class CreditCardController(ICreditCardRepository repo) : Controller
{
    public async Task<IActionResult> Index()
        => View(await repo.GetByUserIdAsync(User.GetUserId()));

    public IActionResult Create() => View(new CreditCardViewModel());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreditCardViewModel vm)
    {
        if (!ModelState.IsValid) return View(vm);
        await repo.CreateAsync(User.GetUserId(), vm.Description, vm.TotalAmount, vm.Installments, vm.InterestRate, vm.PurchaseDate);
        TempData["Success"] = "Compra registrada.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var item = await repo.GetByIdAsync(id, User.GetUserId());
        if (item is null) return NotFound();
        return View(new CreditCardViewModel
        {
            Id = item.Id, Description = item.Description, TotalAmount = item.TotalAmount,
            Installments = item.Installments, InterestRate = item.InterestRate, PurchaseDate = item.PurchaseDate
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(CreditCardViewModel vm)
    {
        if (!ModelState.IsValid) return View(vm);
        await repo.UpdateAsync(vm.Id, User.GetUserId(), vm.Description, vm.TotalAmount, vm.Installments, vm.InterestRate);
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

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AdvanceInstallment(int id)
    {
        await repo.AdvanceInstallmentAsync(id, User.GetUserId());
        TempData["Success"] = "Cuota avanzada.";
        return RedirectToAction(nameof(Index));
    }
}

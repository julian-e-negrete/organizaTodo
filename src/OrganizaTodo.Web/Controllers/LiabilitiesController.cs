using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrganizaTodo.Web.Extensions;
using OrganizaTodo.Web.Models.ViewModels;
using OrganizaTodo.Web.Repositories;

namespace OrganizaTodo.Web.Controllers;

[Authorize]
public sealed class LiabilitiesController(IFixedLiabilityRepository repo) : Controller
{
    public async Task<IActionResult> Index()
        => View(await repo.GetByUserIdAsync(User.GetUserId()));

    public IActionResult Create() => View(new FixedLiabilityViewModel());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(FixedLiabilityViewModel vm)
    {
        if (!ModelState.IsValid) return View(vm);
        await repo.CreateAsync(User.GetUserId(), vm.Name, vm.MonthlyAmount, vm.DueDay);
        TempData["Success"] = "Pasivo agregado.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var item = await repo.GetByIdAsync(id, User.GetUserId());
        if (item is null) return NotFound();
        return View(new FixedLiabilityViewModel
        {
            Id = item.Id, Name = item.Name, MonthlyAmount = item.MonthlyAmount, DueDay = item.DueDay
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(FixedLiabilityViewModel vm)
    {
        if (!ModelState.IsValid) return View(vm);
        await repo.UpdateAsync(vm.Id, User.GetUserId(), vm.Name, vm.MonthlyAmount, vm.DueDay);
        TempData["Success"] = "Pasivo actualizado.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        await repo.DeleteAsync(id, User.GetUserId());
        TempData["Success"] = "Pasivo eliminado.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> TogglePaid(int id, bool isPaid)
    {
        await repo.MarkPaidAsync(id, User.GetUserId(), isPaid);
        return RedirectToAction(nameof(Index));
    }
}

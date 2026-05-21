using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrganizaTodo.Web.Extensions;
using OrganizaTodo.Web.Models.ViewModels;
using OrganizaTodo.Web.Repositories;
using OrganizaTodo.Web.Services;

namespace OrganizaTodo.Web.Controllers;

[Authorize]
public sealed class ShoppingController(
    IShoppingRepository shoppingRepo,
    IProductCatalogRepository productRepo,
    IBalanceService balanceService) : Controller
{
    public async Task<IActionResult> Index(int? month, int? year, string? q, string? category)
    {
        var m = month ?? DateTime.Today.Month;
        var y = year ?? DateTime.Today.Year;
        var userId = User.GetUserId();

        var list       = await shoppingRepo.GetOrCreateListAsync(userId, m, y);
        var items      = await shoppingRepo.GetListItemsAsync(list.Id, userId);
        var balance    = await balanceService.GetMonthlyBalanceAsync(userId, m, y);
        var categories = await productRepo.GetCategoriesAsync();

        var searchResults = !string.IsNullOrWhiteSpace(q)
            ? await productRepo.SearchAsync(q)
            : !string.IsNullOrWhiteSpace(category)
                ? await productRepo.GetByCategoryAsync(category)
                : [];

        return View(new ShoppingViewModel
        {
            List             = list,
            Items            = items,
            SearchResults    = searchResults,
            Categories       = categories,
            SearchQuery      = q,
            SelectedCategory = category,
            RemainingBalance = balance.RemainingBalance
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddItem(AddShoppingItemViewModel vm)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Datos inválidos.";
            return RedirectToAction(nameof(Index));
        }

        await shoppingRepo.AddItemAsync(vm.ShoppingListId, User.GetUserId(),
            vm.ProductName, vm.Quantity, vm.EstimatedPrice, vm.Supermarket, vm.Priority);

        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteItem(int id, int month, int year)
    {
        await shoppingRepo.DeleteItemAsync(id, User.GetUserId());
        return RedirectToAction(nameof(Index), new { month, year });
    }
}

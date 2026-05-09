using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrganizaTodo.Web.Repositories;
using OrganizaTodo.Web.Services;

namespace OrganizaTodo.Web.Controllers;

[Authorize(Roles = "ADMIN")]
public sealed class AdminController(IAdminService adminService, IUserRepository userRepository) : Controller
{
    public async Task<IActionResult> Index()
        => View(await adminService.GetStatsAsync());

    public async Task<IActionResult> Users()
        => View(await userRepository.GetAllAsync());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SetActive(int id, bool isActive)
    {
        await userRepository.SetActiveAsync(id, isActive);
        TempData["Success"] = $"Usuario {(isActive ? "habilitado" : "deshabilitado")}.";
        return RedirectToAction(nameof(Users));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SetRole(int id, string role)
    {
        if (role is not ("USER" or "ADMIN")) return BadRequest();
        await userRepository.SetRoleAsync(id, role);
        TempData["Success"] = $"Rol actualizado a {role}.";
        return RedirectToAction(nameof(Users));
    }
}

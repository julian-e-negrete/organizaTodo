using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrganizaTodo.Web.Extensions;
using OrganizaTodo.Web.Models.ViewModels;
using OrganizaTodo.Web.Repositories;
using OrganizaTodo.Web.Services;

namespace OrganizaTodo.Web.Controllers;

[Authorize]
public sealed class VehiclesController(IVehicleRepository repo, IFuelPriceService fuelPriceService) : Controller
{
    public async Task<IActionResult> Index(int? month, int? year)
    {
        var m = month ?? DateTime.Today.Month;
        var y = year ?? DateTime.Today.Year;
        var items = await repo.GetByUserIdAsync(User.GetUserId(), m, y);
        return View(new VehicleIndexViewModel { Items = items, Month = m, Year = y });
    }

    public IActionResult Create() => View(new VehicleViewModel());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(VehicleViewModel vm)
    {
        if (!ModelState.IsValid) return View(vm);
        await repo.CreateAsync(User.GetUserId(), vm.Plate, vm.Make, vm.Model, vm.Year,
            vm.WeeklyKm, vm.FuelEfficiencyKmL, vm.FuelPricePerLiter,
            vm.InsuranceMonthly, vm.PatenteAnnual,
            vm.OilChangeIntervalKm, vm.OilChangeCostEstimate,
            vm.LastOilChangeDate, vm.LastOilChangeKm);
        TempData["Success"] = "Vehículo registrado.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var item = await repo.GetByIdAsync(id, User.GetUserId());
        if (item is null) return NotFound();
        return View(new VehicleViewModel
        {
            Id = item.Id, Plate = item.Plate, Make = item.Make, Model = item.Model, Year = item.Year,
            WeeklyKm = item.WeeklyKm, FuelEfficiencyKmL = item.FuelEfficiencyKmL,
            FuelPricePerLiter = item.FuelPricePerLiter,
            InsuranceMonthly = item.InsuranceMonthly, PatenteAnnual = item.PatenteAnnual,
            OilChangeIntervalKm = item.OilChangeIntervalKm, OilChangeCostEstimate = item.OilChangeCostEstimate,
            LastOilChangeDate = item.LastOilChangeDate, LastOilChangeKm = item.LastOilChangeKm
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(VehicleViewModel vm)
    {
        if (!ModelState.IsValid) return View(vm);
        await repo.UpdateAsync(vm.Id, User.GetUserId(), vm.Plate, vm.Make, vm.Model, vm.Year,
            vm.WeeklyKm, vm.FuelEfficiencyKmL, vm.FuelPricePerLiter,
            vm.InsuranceMonthly, vm.PatenteAnnual,
            vm.OilChangeIntervalKm, vm.OilChangeCostEstimate,
            vm.LastOilChangeDate, vm.LastOilChangeKm);
        TempData["Success"] = "Vehículo actualizado.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        await repo.DeleteAsync(id, User.GetUserId());
        TempData["Success"] = "Vehículo eliminado.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> TogglePayment(int vehicleId, int month, int year, string paymentType)
    {
        await repo.TogglePaymentAsync(vehicleId, User.GetUserId(), month, year, paymentType);
        return RedirectToAction(nameof(Index), new { month, year });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> LogMaintenance(int vehicleId, DateOnly serviceDate, decimal kmAtService,
        decimal? cost, string? notes, int month, int year)
    {
        await repo.LogMaintenanceAsync(vehicleId, User.GetUserId(), serviceDate, kmAtService, cost, notes);
        TempData["Success"] = "Cambio de aceite registrado.";
        return RedirectToAction(nameof(Index), new { month, year });
    }

    [HttpGet]
    public async Task<IActionResult> GetFuelPrices()
    {
        var prices = await fuelPriceService.GetCurrentPricesAsync();
        if (prices is null)
            return Json(new { error = "No se pudieron obtener los precios de surtidores.com.ar." });
        return Json(new
        {
            super     = prices.Super,
            premium   = prices.Premium,
            gasoil    = prices.Gasoil,
            fetchedAt = prices.FetchedAt.ToString("dd/MM/yyyy HH:mm")
        });
    }
}

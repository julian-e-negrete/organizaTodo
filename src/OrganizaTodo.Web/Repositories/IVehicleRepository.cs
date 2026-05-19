using OrganizaTodo.Web.Models.Domain;

namespace OrganizaTodo.Web.Repositories;

public interface IVehicleRepository
{
    Task<IEnumerable<Vehicle>> GetByUserIdAsync(int userId, int month, int year);
    Task<Vehicle?> GetByIdAsync(int id, int userId);
    Task CreateAsync(int userId, string plate, string make, string model, int? year,
        decimal weeklyKm, decimal fuelEfficiencyKmL, decimal? fuelPricePerLiter,
        decimal? insuranceMonthly, decimal? patenteAnnual,
        decimal? oilChangeIntervalKm, decimal? oilChangeCostEstimate,
        DateOnly? lastOilChangeDate, decimal? lastOilChangeKm);
    Task UpdateAsync(int id, int userId, string plate, string make, string model, int? year,
        decimal weeklyKm, decimal fuelEfficiencyKmL, decimal? fuelPricePerLiter,
        decimal? insuranceMonthly, decimal? patenteAnnual,
        decimal? oilChangeIntervalKm, decimal? oilChangeCostEstimate,
        DateOnly? lastOilChangeDate, decimal? lastOilChangeKm);
    Task DeleteAsync(int id, int userId);
    Task TogglePaymentAsync(int vehicleId, int userId, int month, int year, string paymentType);
    Task LogMaintenanceAsync(int vehicleId, int userId, DateOnly serviceDate, decimal kmAtService, decimal? cost, string? notes);
    Task<IEnumerable<VehicleMaintenanceLog>> GetMaintenanceLogsAsync(int vehicleId, int userId);
}

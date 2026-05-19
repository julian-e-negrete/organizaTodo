using Dapper;
using OrganizaTodo.Web.Data;
using OrganizaTodo.Web.Models.Domain;

namespace OrganizaTodo.Web.Repositories;

public sealed class VehicleRepository(IDbConnectionFactory connectionFactory) : IVehicleRepository
{
    public async Task<IEnumerable<Vehicle>> GetByUserIdAsync(int userId, int month, int year)
    {
        using var conn = connectionFactory.CreateConnection();
        return await conn.QueryAsync<Vehicle>(
            "SELECT * FROM sp_vehicles_get_by_user_id(@userId, @month, @year)",
            new { userId, month, year });
    }

    public async Task<Vehicle?> GetByIdAsync(int id, int userId)
    {
        using var conn = connectionFactory.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<Vehicle>(
            "SELECT * FROM sp_vehicles_get_by_id(@id, @userId)",
            new { id, userId });
    }

    public async Task CreateAsync(int userId, string plate, string make, string model, int? year,
        decimal weeklyKm, decimal fuelEfficiencyKmL, decimal? fuelPricePerLiter,
        decimal? insuranceMonthly, decimal? patenteAnnual,
        decimal? oilChangeIntervalKm, decimal? oilChangeCostEstimate,
        DateOnly? lastOilChangeDate, decimal? lastOilChangeKm)
    {
        using var conn = connectionFactory.CreateConnection();
        await conn.QueryFirstOrDefaultAsync<object>(
            "SELECT sp_vehicles_create(@userId, @plate, @make, @model, @year, @weeklyKm, @fuelEfficiencyKmL, @fuelPricePerLiter, @insuranceMonthly, @patenteAnnual, @oilChangeIntervalKm, @oilChangeCostEstimate, @lastOilChangeDate, @lastOilChangeKm)",
            new { userId, plate, make, model, year, weeklyKm, fuelEfficiencyKmL, fuelPricePerLiter, insuranceMonthly, patenteAnnual, oilChangeIntervalKm, oilChangeCostEstimate, lastOilChangeDate, lastOilChangeKm });
    }

    public async Task UpdateAsync(int id, int userId, string plate, string make, string model, int? year,
        decimal weeklyKm, decimal fuelEfficiencyKmL, decimal? fuelPricePerLiter,
        decimal? insuranceMonthly, decimal? patenteAnnual,
        decimal? oilChangeIntervalKm, decimal? oilChangeCostEstimate,
        DateOnly? lastOilChangeDate, decimal? lastOilChangeKm)
    {
        using var conn = connectionFactory.CreateConnection();
        await conn.QueryFirstOrDefaultAsync<object>(
            "SELECT sp_vehicles_update(@id, @userId, @plate, @make, @model, @year, @weeklyKm, @fuelEfficiencyKmL, @fuelPricePerLiter, @insuranceMonthly, @patenteAnnual, @oilChangeIntervalKm, @oilChangeCostEstimate, @lastOilChangeDate, @lastOilChangeKm)",
            new { id, userId, plate, make, model, year, weeklyKm, fuelEfficiencyKmL, fuelPricePerLiter, insuranceMonthly, patenteAnnual, oilChangeIntervalKm, oilChangeCostEstimate, lastOilChangeDate, lastOilChangeKm });
    }

    public async Task DeleteAsync(int id, int userId)
    {
        using var conn = connectionFactory.CreateConnection();
        await conn.QueryFirstOrDefaultAsync<object>(
            "SELECT sp_vehicles_delete(@id, @userId)",
            new { id, userId });
    }

    public async Task TogglePaymentAsync(int vehicleId, int userId, int month, int year, string paymentType)
    {
        using var conn = connectionFactory.CreateConnection();
        await conn.QueryFirstOrDefaultAsync<object>(
            "SELECT sp_vehicle_payments_toggle(@vehicleId, @userId, @month, @year, @paymentType)",
            new { vehicleId, userId, month, year, paymentType });
    }

    public async Task LogMaintenanceAsync(int vehicleId, int userId, DateOnly serviceDate, decimal kmAtService, decimal? cost, string? notes)
    {
        using var conn = connectionFactory.CreateConnection();
        await conn.QueryFirstOrDefaultAsync<object>(
            "SELECT sp_vehicle_maintenance_log_create(@vehicleId, @userId, @serviceDate, @kmAtService, @cost, @notes)",
            new { vehicleId, userId, serviceDate, kmAtService, cost, notes });
    }

    public async Task<IEnumerable<VehicleMaintenanceLog>> GetMaintenanceLogsAsync(int vehicleId, int userId)
    {
        using var conn = connectionFactory.CreateConnection();
        return await conn.QueryAsync<VehicleMaintenanceLog>(
            "SELECT * FROM sp_vehicle_maintenance_logs_get(@vehicleId, @userId)",
            new { vehicleId, userId });
    }
}

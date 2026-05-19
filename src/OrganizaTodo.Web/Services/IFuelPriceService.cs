using OrganizaTodo.Web.Models.Domain;

namespace OrganizaTodo.Web.Services;

public interface IFuelPriceService
{
    Task<FuelPrices?> GetCurrentPricesAsync();
}

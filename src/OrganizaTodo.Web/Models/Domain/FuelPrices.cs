namespace OrganizaTodo.Web.Models.Domain;

public sealed class FuelPrices
{
    public decimal Super { get; set; }
    public decimal Premium { get; set; }
    public decimal Gasoil { get; set; }
    public DateTime FetchedAt { get; set; } = DateTime.Now;
}

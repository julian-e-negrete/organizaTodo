namespace OrganizaTodo.Web.Models.Domain;

public sealed class ScrapedProduct
{
    public string Name      { get; set; } = string.Empty;
    public string Brand     { get; set; } = string.Empty;
    public string Category  { get; set; } = string.Empty;
    public decimal Price    { get; set; }
    public decimal? ListPrice { get; set; }
    public string? Promo    { get; set; }
    public bool Available   { get; set; }
    public string Source    { get; set; } = string.Empty;
    public DateTime ScrapedAt { get; set; }

    public bool IsOnPromo   => !string.IsNullOrWhiteSpace(Promo);
    public bool IsCarrefour => Source == "Carrefour";
    public bool IsCoto      => Source == "Coto";
}

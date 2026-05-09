namespace OrganizaTodo.Web.Models.Domain;

public sealed class MockProduct
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal? CotoPrice { get; set; }
    public decimal? CarrefourPrice { get; set; }
    public string Unit { get; set; } = "unidad";

    public decimal? CheapestPrice => (CotoPrice, CarrefourPrice) switch
    {
        (null, null) => null,
        (var c, null) => c,
        (null, var r) => r,
        (var c, var r) => Math.Min(c.Value, r.Value)
    };

    public string CheapestSupermarket => (CotoPrice, CarrefourPrice) switch
    {
        (null, null) => "-",
        (_, null) => "Coto",
        (null, _) => "Carrefour",
        (var c, var r) => c <= r ? "Coto" : "Carrefour"
    };
}

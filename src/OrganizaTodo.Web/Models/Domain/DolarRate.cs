using System.Text.Json.Serialization;

namespace OrganizaTodo.Web.Models.Domain;

public sealed class DolarRate
{
    [JsonPropertyName("moneda")]            public string Moneda { get; set; } = "";
    [JsonPropertyName("casa")]              public string Casa { get; set; } = "";
    [JsonPropertyName("nombre")]            public string Nombre { get; set; } = "";
    [JsonPropertyName("compra")]            public decimal Compra { get; set; }
    [JsonPropertyName("venta")]             public decimal Venta { get; set; }
    [JsonPropertyName("fechaActualizacion")] public DateTime FechaActualizacion { get; set; }
}

using System.Net.Http.Json;
using OrganizaTodo.Web.Models.Domain;

namespace OrganizaTodo.Web.Services;

public sealed class DolarService(HttpClient http) : IDolarService
{
    public async Task<IReadOnlyList<DolarRate>> GetRatesAsync()
    {
        try
        {
            var rates = await http.GetFromJsonAsync<DolarRate[]>("https://dolarapi.com/v1/dolares");
            return rates ?? [];
        }
        catch
        {
            return [];
        }
    }
}

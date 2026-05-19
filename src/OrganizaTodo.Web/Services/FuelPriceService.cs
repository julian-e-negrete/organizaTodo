using System.Text.RegularExpressions;
using OrganizaTodo.Web.Models.Domain;

namespace OrganizaTodo.Web.Services;

public sealed class FuelPriceService(HttpClient http) : IFuelPriceService
{
    private static readonly string[] MonthNames =
        ["", "Enero", "Febrero", "Marzo", "Abril", "Mayo", "Junio",
         "Julio", "Agosto", "Septiembre", "Octubre", "Noviembre", "Diciembre"];

    public async Task<FuelPrices?> GetCurrentPricesAsync()
    {
        try
        {
            var html = await http.GetStringAsync("https://surtidores.com.ar/precios/");
            return Parse(html, DateTime.Now.Year, DateTime.Now.Month);
        }
        catch
        {
            return null;
        }
    }

    private static FuelPrices? Parse(string html, int year, int month)
    {
        var tables = Regex.Matches(html, @"<table>(.*?)</table>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        var targetMonthName = MonthNames[month];

        foreach (Match tableMatch in tables)
        {
            var rows = Regex.Matches(tableMatch.Groups[1].Value, @"<tr>(.*?)</tr>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            if (rows.Count < 2) continue;

            var headerCells = ExtractCells(rows[0].Groups[1].Value);
            if (headerCells.Count == 0) continue;

            var yearText = StripTags(headerCells[0]).Trim();
            if (!int.TryParse(yearText, out var tableYear) || tableYear != year) continue;

            int monthCol = -1;
            for (int i = 1; i < headerCells.Count; i++)
            {
                if (string.Equals(StripTags(headerCells[i]).Trim(), targetMonthName, StringComparison.OrdinalIgnoreCase))
                {
                    monthCol = i;
                    break;
                }
            }
            if (monthCol < 0) continue;

            var result = new FuelPrices { FetchedAt = DateTime.Now };
            bool hasData = false;

            for (int rowIdx = 1; rowIdx < rows.Count; rowIdx++)
            {
                var cells = ExtractCells(rows[rowIdx].Groups[1].Value);
                if (cells.Count < 2) continue;

                var fuelType = StripTags(cells[0]).Trim();

                // Scan from current month column backwards to get the latest non-blank price
                decimal price = 0;
                for (int col = monthCol; col >= 1 && price == 0; col--)
                {
                    if (col >= cells.Count) continue;
                    var raw = StripTags(cells[col]).Trim().Replace(",", ".");
                    if (!string.IsNullOrWhiteSpace(raw) &&
                        decimal.TryParse(raw, System.Globalization.NumberStyles.Any,
                            System.Globalization.CultureInfo.InvariantCulture, out var parsed) && parsed > 0)
                    {
                        price = parsed;
                    }
                }

                if (string.Equals(fuelType, "Super", StringComparison.OrdinalIgnoreCase))
                    { result.Super = price; hasData = price > 0; }
                else if (string.Equals(fuelType, "Premium", StringComparison.OrdinalIgnoreCase))
                    result.Premium = price;
                else if (string.Equals(fuelType, "Gasoil", StringComparison.OrdinalIgnoreCase))
                    result.Gasoil = price;
            }

            if (hasData) return result;
        }

        return null;
    }

    private static List<string> ExtractCells(string rowHtml)
    {
        var cells = new List<string>();
        foreach (Match m in Regex.Matches(rowHtml, @"<td[^>]*>(.*?)</td>", RegexOptions.Singleline | RegexOptions.IgnoreCase))
            cells.Add(m.Groups[1].Value);
        return cells;
    }

    private static string StripTags(string html) => Regex.Replace(html, @"<[^>]+>", "");
}

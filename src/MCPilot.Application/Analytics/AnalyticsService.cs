using MCPilot.Application.Abstractions;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace MCPilot.Application.Analytics;

public sealed class AnalyticsService(IToolCatalog toolCatalog, ILogger<AnalyticsService> logger) : IAnalyticsService
{
    private const string ToolPrefix = "db__";

    private static readonly string[] TurkishShortMonths =
        ["Oca", "Şub", "Mar", "Nis", "May", "Haz", "Tem", "Ağu", "Eyl", "Eki", "Kas", "Ara"];

    public async Task<KpiSummaryDto> GetKpiSummaryAsync(DateRange range, CancellationToken ct = default)
    {
        var current = await InvokeAsync("get_sales_summary", RangeArgs(range), ct);

        var revenue = Money(current, "totalrevenue");
        var profit = Money(current, "totalprofit");
        var orders = Count(current, "totalorders");
        var aov = Money(current, "averageordervalue");

        JToken? previous = null;
        try
        {
            previous = await InvokeAsync("get_sales_summary", RangeArgs(range.PreviousPeriod()), ct);
        }
        catch (AnalyticsUnavailableException ex)
        {
            logger.LogWarning(ex, "Onceki donem ozeti alinamadi; degisim yuzdeleri bos donecek.");
        }

        return new KpiSummaryDto(
            revenue,
            Change(revenue, previous is null ? null : Money(previous, "totalrevenue")),
            profit,
            Change(profit, previous is null ? null : Money(previous, "totalprofit")),
            orders,
            Change(orders, previous is null ? null : Count(previous, "totalorders")),
            aov,
            Change(aov, previous is null ? null : Money(previous, "averageordervalue")));
    }

    public async Task<IReadOnlyList<TopProductDto>> GetTopProductsAsync(
        DateRange range,
        int limit,
        string? category,
        CancellationToken ct = default)
    {
        var args = RangeArgs(range);
        args["limit"] = limit;

        if (!string.IsNullOrWhiteSpace(category))
        {
            args["category"] = category;
        }

        var current = await InvokeArrayAsync("get_top_products", args, ct);

        var previousRevenue = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var previousArgs = RangeArgs(range.PreviousPeriod());
            previousArgs["limit"] = Math.Max(limit * 10, 200);

            if (!string.IsNullOrWhiteSpace(category))
            {
                previousArgs["category"] = category;
            }

            foreach (var row in await InvokeArrayAsync("get_top_products", previousArgs, ct))
            {
                previousRevenue[Text(row, "productname")] = Money(row, "totalrevenue");
            }
        }
        catch (AnalyticsUnavailableException ex)
        {
            logger.LogWarning(ex, "Onceki donem urun verisi alinamadi; trend bos donecek.");
        }

        return
        [
            .. current.Select((row, index) =>
            {
                var name = Text(row, "productname");
                var revenue = Money(row, "totalrevenue");

                string? trend = previousRevenue.TryGetValue(name, out var before)
                    ? revenue >= before ? "up" : "down"
                    : null;

                return new TopProductDto(
                    index + 1,
                    name,
                    Text(row, "categoryname"),
                    Count(row, "totalunits"),
                    revenue,
                    trend);
            }),
        ];
    }

    public async Task<IReadOnlyList<CategorySalesDto>> GetSalesByCategoryAsync(DateRange range, CancellationToken ct = default)
    {
        var rows = await InvokeArrayAsync("get_category_sales", RangeArgs(range), ct);
        var total = rows.Sum(r => Money(r, "totalrevenue"));

        return
        [
            .. rows.Select(row =>
            {
                var revenue = Money(row, "totalrevenue");
                return new CategorySalesDto(Text(row, "categoryname"), Share(revenue, total), revenue);
            }),
        ];
    }

    public async Task<IReadOnlyList<CitySalesDto>> GetSalesByCityAsync(DateRange range, CancellationToken ct = default)
    {
        var rows = await InvokeArrayAsync("get_city_sales", RangeArgs(range), ct);
        var total = rows.Sum(r => Money(r, "totalrevenue"));

        return
        [
            .. rows.Select(row =>
            {
                var revenue = Money(row, "totalrevenue");
                return new CitySalesDto(Text(row, "city"), revenue, Share(revenue, total));
            }),
        ];
    }

    public async Task<IReadOnlyList<TopCustomerDto>> GetTopCustomersAsync(int limit, CancellationToken ct = default)
    {
        var rows = await InvokeArrayAsync("get_top_customers", new JObject { ["limit"] = limit }, ct);

        return
        [
            .. rows.Select((row, index) => new TopCustomerDto(
                index + 1,
                Text(row, "customerfullname"),
                Text(row, "currentsegment"),
                Money(row, "loyaltyscore"),
                Money(row, "lifetimevalue"),
                Count(row, "totalorders"))),
        ];
    }

    public async Task<IReadOnlyList<CustomerSegmentDto>> GetCustomerSegmentsAsync(CancellationToken ct = default)
    {
        var rows = await InvokeArrayAsync("get_customer_segments", [], ct);
        var total = rows.Sum(r => Money(r, "totalrevenue"));

        return
        [
            .. rows.Select(row =>
            {
                var revenue = Money(row, "totalrevenue");
                return new CustomerSegmentDto(
                    Text(row, "currentsegment"),
                    Share(revenue, total),
                    revenue,
                    Count(row, "customercount"));
            }),
        ];
    }

    public async Task<IReadOnlyList<MonthlySalesDto>> GetMonthlySalesAsync(DateRange range, CancellationToken ct = default)
    {
        var result = new List<MonthlySalesDto>();

        for (var year = range.StartDate.Year; year <= range.EndDate.Year; year++)
        {
            var rows = await InvokeArrayAsync("get_monthly_trend", new JObject { ["year"] = year }, ct);

            foreach (var row in rows)
            {
                var month = Count(row, "month");
                if (month is < 1 or > 12)
                {
                    continue;
                }

                var monthEnd = new DateOnly(year, month, DateTime.DaysInMonth(year, month));
                var monthStart = new DateOnly(year, month, 1);

                if (monthEnd < range.StartDate || monthStart > range.EndDate)
                {
                    continue;
                }

                result.Add(new MonthlySalesDto(
                    TurkishShortMonths[month - 1],
                    month,
                    year,
                    Money(row, "totalrevenue"),
                    Money(row, "totalprofit")));
            }
        }

        return result;
    }

    private static JObject RangeArgs(DateRange range) => new()
    {
        ["from"] = range.StartDate.ToString("yyyy-MM-dd"),
        ["to"] = range.EndDate.ToString("yyyy-MM-dd"),
    };

    private async Task<JToken> InvokeAsync(string tool, JObject args, CancellationToken ct)
    {
        var name = ToolPrefix + tool;
        var result = await toolCatalog.InvokeAsync(name, args, ct);

        if (result.IsError)
        {
            logger.LogError("Analiz araci hata dondurdu: {Tool} - {Message}", name, result.Content);
            throw new AnalyticsUnavailableException($"'{tool}' verisi alinamadi.");
        }

        if (result.StructuredContent is { } structured && structured.Type != JTokenType.Null)
        {
            return structured;
        }

        try
        {
            return JToken.Parse(result.Content);
        }
        catch (Newtonsoft.Json.JsonReaderException ex)
        {
            logger.LogError(ex, "Analiz araci JSON olmayan cikti dondurdu: {Tool}", name);
            throw new AnalyticsUnavailableException($"'{tool}' beklenmeyen bir cikti dondurdu.", ex);
        }
    }

    private async Task<IReadOnlyList<JToken>> InvokeArrayAsync(string tool, JObject args, CancellationToken ct)
    {
        var token = await InvokeAsync(tool, args, ct);

        return token switch
        {
            JArray array => [.. array],
            JObject obj when obj["result"] is JArray nested => [.. nested],
            JObject obj => [obj],
            _ => [],
        };
    }

    private static JToken? Field(JToken token, string name) =>
        token is JObject obj && obj.TryGetValue(name, StringComparison.OrdinalIgnoreCase, out var value)
            ? value
            : null;

    private static decimal Money(JToken token, string name)
    {
        var field = Field(token, name);
        return field is null || field.Type is JTokenType.Null
            ? 0m
            : Math.Round(field.Value<decimal>(), 2, MidpointRounding.AwayFromZero);
    }

    private static int Count(JToken token, string name)
    {
        var field = Field(token, name);
        return field is null || field.Type is JTokenType.Null ? 0 : field.Value<int>();
    }

    private static string Text(JToken token, string name) => Field(token, name)?.Value<string>() ?? string.Empty;

    private static decimal Share(decimal value, decimal total) =>
        total == 0m ? 0m : Math.Round(value / total * 100m, 1, MidpointRounding.AwayFromZero);

    private static decimal? Change(decimal current, decimal? previous) =>
        previous is null or 0m ? null : Math.Round((current - previous.Value) / previous.Value * 100m, 1, MidpointRounding.AwayFromZero);
}

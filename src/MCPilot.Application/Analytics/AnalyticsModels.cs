namespace MCPilot.Application.Analytics;

public sealed record KpiSummaryDto(
    decimal Revenue,
    decimal? RevenueChange,
    decimal Profit,
    decimal? ProfitChange,
    int Orders,
    decimal? OrdersChange,
    decimal Aov,
    decimal? AovChange);

public sealed record TopProductDto(
    int Rank,
    string Name,
    string Category,
    int Units,
    decimal Revenue,
    string? Trend);

public sealed record CategorySalesDto(
    string Name,
    decimal Value,
    decimal Revenue);

public sealed record CitySalesDto(
    string City,
    decimal Revenue,
    decimal Percentage);

public sealed record TopCustomerDto(
    int Rank,
    string Name,
    string Segment,
    decimal Score,
    decimal Ltv,
    int Orders);

public sealed record CustomerSegmentDto(
    string Name,
    decimal Value,
    decimal Revenue,
    int CustomerCount);

public sealed record MonthlySalesDto(
    string Month,
    int MonthNumber,
    int Year,
    decimal Revenue,
    decimal Profit);

public sealed record DateRange(DateOnly StartDate, DateOnly EndDate)
{
    public int LengthInDays => EndDate.DayNumber - StartDate.DayNumber + 1;

    public DateRange PreviousPeriod()
    {
        var end = StartDate.AddDays(-1);
        return new DateRange(end.AddDays(-(LengthInDays - 1)), end);
    }
}

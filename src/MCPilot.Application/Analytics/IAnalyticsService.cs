using MCPilot.Application.Models;

namespace MCPilot.Application.Analytics;

public interface IAnalyticsService
{
    Task<KpiSummaryDto> GetKpiSummaryAsync(DateRange range, CancellationToken ct = default);

    Task<IReadOnlyList<TopProductDto>> GetTopProductsAsync(DateRange range, int limit, string? category, CancellationToken ct = default);

    Task<IReadOnlyList<CategorySalesDto>> GetSalesByCategoryAsync(DateRange range, CancellationToken ct = default);

    Task<IReadOnlyList<CitySalesDto>> GetSalesByCityAsync(DateRange range, CancellationToken ct = default);

    Task<IReadOnlyList<TopCustomerDto>> GetTopCustomersAsync(int limit, CancellationToken ct = default);

    Task<IReadOnlyList<CustomerSegmentDto>> GetCustomerSegmentsAsync(CancellationToken ct = default);

    Task<IReadOnlyList<MonthlySalesDto>> GetMonthlySalesAsync(DateRange range, CancellationToken ct = default);
}

public sealed class AnalyticsUnavailableException(string message, Exception? innerException = null)
    : Exception(message, innerException);

using MCPilot.Application.Analytics;
using Microsoft.AspNetCore.Mvc;

namespace MCPilot.Api.Controllers;

[ApiController]
[Route("api/analytics")]
[Produces("application/json")]
public sealed class AnalyticsController(IAnalyticsService analytics) : ControllerBase
{
    [HttpGet("kpi-summary")]
    [ProducesResponseType(typeof(KpiSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<KpiSummaryDto>> GetKpiSummary(
        [FromQuery] string startDate,
        [FromQuery] string endDate,
        CancellationToken cancellationToken)
    {
        if (!TryParseRange(startDate, endDate, out var range, out var problem))
        {
            return BadRequest(problem);
        }

        return Ok(await analytics.GetKpiSummaryAsync(range, cancellationToken));
    }

    [HttpGet("top-products")]
    [ProducesResponseType(typeof(IReadOnlyList<TopProductDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<TopProductDto>>> GetTopProducts(
        [FromQuery] string startDate,
        [FromQuery] string endDate,
        [FromQuery] int limit = 5,
        [FromQuery] string? category = null,
        CancellationToken cancellationToken = default)
    {
        if (!TryParseRange(startDate, endDate, out var range, out var problem))
        {
            return BadRequest(problem);
        }

        return Ok(await analytics.GetTopProductsAsync(range, NormalizeLimit(limit), category, cancellationToken));
    }

    [HttpGet("sales-by-category")]
    [ProducesResponseType(typeof(IReadOnlyList<CategorySalesDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<CategorySalesDto>>> GetSalesByCategory(
        [FromQuery] string startDate,
        [FromQuery] string endDate,
        CancellationToken cancellationToken)
    {
        if (!TryParseRange(startDate, endDate, out var range, out var problem))
        {
            return BadRequest(problem);
        }

        return Ok(await analytics.GetSalesByCategoryAsync(range, cancellationToken));
    }

    [HttpGet("sales-by-city")]
    [ProducesResponseType(typeof(IReadOnlyList<CitySalesDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<CitySalesDto>>> GetSalesByCity(
        [FromQuery] string startDate,
        [FromQuery] string endDate,
        CancellationToken cancellationToken)
    {
        if (!TryParseRange(startDate, endDate, out var range, out var problem))
        {
            return BadRequest(problem);
        }

        return Ok(await analytics.GetSalesByCityAsync(range, cancellationToken));
    }

    [HttpGet("top-customers")]
    [ProducesResponseType(typeof(IReadOnlyList<TopCustomerDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TopCustomerDto>>> GetTopCustomers(
        [FromQuery] int limit = 5,
        CancellationToken cancellationToken = default) =>
        Ok(await analytics.GetTopCustomersAsync(NormalizeLimit(limit), cancellationToken));

    [HttpGet("customer-segments")]
    [ProducesResponseType(typeof(IReadOnlyList<CustomerSegmentDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CustomerSegmentDto>>> GetCustomerSegments(
        CancellationToken cancellationToken) =>
        Ok(await analytics.GetCustomerSegmentsAsync(cancellationToken));

    [HttpGet("monthly-sales")]
    [ProducesResponseType(typeof(IReadOnlyList<MonthlySalesDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<MonthlySalesDto>>> GetMonthlySales(
        [FromQuery] string startDate,
        [FromQuery] string endDate,
        CancellationToken cancellationToken)
    {
        if (!TryParseRange(startDate, endDate, out var range, out var problem))
        {
            return BadRequest(problem);
        }

        return Ok(await analytics.GetMonthlySalesAsync(range, cancellationToken));
    }

    private static int NormalizeLimit(int limit) => Math.Clamp(limit, 1, 100);

    private static bool TryParseRange(string? startDate, string? endDate, out DateRange range, out ProblemDetails? problem)
    {
        range = default!;
        problem = null;

        if (!DateOnly.TryParse(startDate, out var start) || !DateOnly.TryParse(endDate, out var end))
        {
            problem = new ProblemDetails
            {
                Title = "Gecersiz tarih araligi.",
                Status = StatusCodes.Status400BadRequest,
                Detail = "startDate ve endDate zorunludur ve yyyy-MM-dd biciminde olmalidir.",
            };

            return false;
        }

        if (end < start)
        {
            problem = new ProblemDetails
            {
                Title = "Gecersiz tarih araligi.",
                Status = StatusCodes.Status400BadRequest,
                Detail = "endDate, startDate degerinden once olamaz.",
            };

            return false;
        }

        range = new DateRange(start, end);
        return true;
    }
}

using GarmentERP.Application.DTOs;
using GarmentERP.Application.Interfaces;
using GarmentERP.Shared.Models;
using Microsoft.AspNetCore.Mvc;

namespace GarmentERP.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class ReportsController : ControllerBase
{
    private readonly IEnterpriseReportService _reportService;

    public ReportsController(IEnterpriseReportService reportService)
    {
        _reportService = reportService;
    }

    [HttpGet("sales/daily")]
    public async Task<ActionResult<ApiResponse<DailySalesReport>>> GetDailySales([FromQuery] Guid branchId, [FromQuery] DateTime? date, CancellationToken ct)
    {
        var targetDate = date ?? DateTime.UtcNow;
        return Ok(await _reportService.GetDailySalesReportAsync(branchId, targetDate, ct));
    }

    [HttpGet("stock/valuation")]
    public async Task<ActionResult<ApiResponse<StockValuationReport>>> GetStockValuation([FromQuery] Guid? warehouseId, CancellationToken ct)
    {
        return Ok(await _reportService.GetStockValuationReportAsync(warehouseId, ct));
    }

    [HttpGet("financial/pnl")]
    public async Task<ActionResult<ApiResponse<ProfitAndLossReport>>> GetProfitAndLoss([FromQuery] DateTime? fromDate, [FromQuery] DateTime? toDate, CancellationToken ct)
    {
        var from = fromDate ?? DateTime.UtcNow.AddDays(-30);
        var to = toDate ?? DateTime.UtcNow;
        return Ok(await _reportService.GetProfitAndLossReportAsync(from, to, ct));
    }
}

using GarmentERP.Application.DTOs;
using GarmentERP.Application.Interfaces;
using GarmentERP.Shared.Models;
using Microsoft.AspNetCore.Mvc;

namespace GarmentERP.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class DashboardController : ControllerBase
{
    private readonly IDashboardService _dashboardService;

    public DashboardController(IDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    [HttpGet("kpis")]
    public async Task<ActionResult<ApiResponse<StoreKpiSummary>>> GetKpis([FromQuery] Guid branchId, CancellationToken ct)
    {
        return Ok(await _dashboardService.GetStoreKpisAsync(branchId, ct));
    }

    [HttpGet("low-stock")]
    public async Task<ActionResult<ApiResponse<List<LowStockAlertDto>>>> GetLowStock([FromQuery] Guid branchId, CancellationToken ct)
    {
        return Ok(await _dashboardService.GetLowStockAlertsAsync(branchId, ct));
    }

    [HttpGet("top-styles")]
    public async Task<ActionResult<ApiResponse<List<TopSellingGarmentDto>>>> GetTopStyles([FromQuery] Guid branchId, [FromQuery] int limit = 5, CancellationToken ct = default)
    {
        return Ok(await _dashboardService.GetTopSellingStylesAsync(branchId, limit, ct));
    }
}

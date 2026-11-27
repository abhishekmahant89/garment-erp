using GarmentERP.Application.DTOs;
using GarmentERP.Application.Interfaces;
using GarmentERP.Shared.Models;
using Microsoft.AspNetCore.Mvc;

namespace GarmentERP.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class GstController : ControllerBase
{
    private readonly IGstReportService _gstReportService;

    public GstController(IGstReportService gstReportService)
    {
        _gstReportService = gstReportService;
    }

    [HttpGet("gstr1")]
    public async Task<ActionResult<ApiResponse<Gstr1ReportDto>>> GetGstr1(
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] Guid? branchId,
        CancellationToken ct)
    {
        var from = fromDate ?? DateTime.UtcNow.AddDays(-30);
        var to = toDate ?? DateTime.UtcNow;
        return Ok(await _gstReportService.GenerateGstr1ReportAsync(from, to, branchId, ct));
    }
}

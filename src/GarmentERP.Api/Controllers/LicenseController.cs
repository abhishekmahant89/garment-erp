using GarmentERP.Application.DTOs;
using GarmentERP.Application.Interfaces;
using GarmentERP.Domain.Entities;
using GarmentERP.Infrastructure.Data;
using GarmentERP.Shared.Models;
using Microsoft.AspNetCore.Mvc;

namespace GarmentERP.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class LicenseController : ControllerBase
{
    private readonly ILicenseValidator _licenseValidator;
    private readonly IErrorTelemetryReporter _telemetryReporter;
    private readonly GarmentDbContextBase _context;

    public LicenseController(
        ILicenseValidator licenseValidator,
        IErrorTelemetryReporter telemetryReporter,
        GarmentDbContextBase context)
    {
        _licenseValidator = licenseValidator;
        _telemetryReporter = telemetryReporter;
        _context = context;
    }

    [HttpPost("validate")]
    public async Task<ActionResult<ApiResponse<LicenseValidationResponse>>> Validate([FromBody] LicenseValidationRequest request, CancellationToken ct)
    {
        var result = await _licenseValidator.ValidateAsync(request.LicenseKey, ct);
        return Ok(ApiResponse<LicenseValidationResponse>.Ok(result));
    }

    [HttpGet("fingerprint")]
    public ActionResult<ApiResponse<string>> GetFingerprint()
    {
        var fingerprint = _licenseValidator.GetMachineFingerprint();
        return Ok(ApiResponse<string>.Ok(fingerprint));
    }

    [HttpPost("telemetry/report")]
    public async Task<ActionResult<ApiResponse<string>>> ReportError([FromBody] ErrorTelemetryRequest request, CancellationToken ct)
    {
        var telemetry = new ErrorTelemetry
        {
            Id = Guid.NewGuid(),
            MachineName = request.MachineFingerprint,
            AppVersion = request.AppVersion,
            ExceptionType = request.ExceptionType,
            Message = $"[Key: {request.LicenseKey}] {request.Message}",
            StackTrace = request.StackTrace,
            SyncStatusSummary = request.SyncStatusSummary,
            OccurredAt = request.OccurredAt,
            ReportedToLicenseServer = true,
            ReportedAt = DateTime.UtcNow
        };

        await _context.ErrorTelemetries.AddAsync(telemetry, ct);
        await _context.SaveChangesAsync(ct);

        return Ok(ApiResponse<string>.Ok("Error telemetry successfully recorded at license server."));
    }
}

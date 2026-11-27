using GarmentERP.Application.DTOs;
using GarmentERP.Application.Interfaces;
using GarmentERP.Shared.Models;
using Microsoft.AspNetCore.Mvc;

namespace GarmentERP.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class BackupController : ControllerBase
{
    private readonly IBackupService _backupService;

    public BackupController(IBackupService backupService)
    {
        _backupService = backupService;
    }

    [HttpPost("create")]
    public async Task<ActionResult<ApiResponse<BackupResultDto>>> CreateBackup([FromQuery] string? folder, CancellationToken ct)
    {
        var targetDir = string.IsNullOrWhiteSpace(folder) ? Path.Combine(AppContext.BaseDirectory, "backups") : folder;
        var result = await _backupService.CreateDatabaseBackupAsync(targetDir, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("verify-integrity")]
    public async Task<ActionResult<ApiResponse<bool>>> VerifyIntegrity(CancellationToken ct)
    {
        return Ok(await _backupService.VerifyDatabaseIntegrityAsync(ct));
    }
}

using GarmentERP.Application.DTOs;
using GarmentERP.Application.Interfaces;
using GarmentERP.Domain.Entities;
using GarmentERP.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace GarmentERP.Infrastructure.SyncAndLicense;

public class ErrorTelemetryReporter : IErrorTelemetryReporter
{
    private readonly GarmentDbContextBase _context;
    private readonly ILicenseValidator _licenseValidator;
    private readonly ITelegramNotificationService _telegramService;

    public ErrorTelemetryReporter(
        GarmentDbContextBase context,
        ILicenseValidator licenseValidator,
        ITelegramNotificationService telegramService)
    {
        _context = context;
        _licenseValidator = licenseValidator;
        _telegramService = telegramService;
    }

    public async Task LogExceptionAsync(Exception ex, string? contextInfo = null, CancellationToken ct = default)
    {
        try
        {
            var errorDetail = ex.InnerException != null ? $"{ex.Message} -> Inner: {ex.InnerException.Message}" : ex.Message;
            var telemetry = new ErrorTelemetry
            {
                Id = Guid.NewGuid(),
                MachineName = Environment.MachineName,
                AppVersion = "2.1.0",
                ExceptionType = ex.GetType().FullName ?? "System.Exception",
                Message = string.IsNullOrEmpty(contextInfo) ? errorDetail : $"[{contextInfo}] {errorDetail}",
                StackTrace = ex.StackTrace ?? string.Empty,
                SyncStatusSummary = "AutoCaptured",
                OccurredAt = DateTime.UtcNow,
                ReportedToLicenseServer = false
            };

            await _context.ErrorTelemetries.AddAsync(telemetry, ct);
            await _context.SaveChangesAsync(ct);

            // Send real-time Telegram notification alert
            await _telegramService.NotifyExceptionTelemetryAsync(
                telemetry.ExceptionType,
                telemetry.Message,
                contextInfo ?? "POS Engine",
                ct
            );
        }
        catch
        {
            // Fail-safe to avoid crash loop inside exception handler
        }
    }

    public async Task<int> FlushPendingTelemetryAsync(CancellationToken ct = default)
    {
        var pending = await _context.ErrorTelemetries
            .Where(e => !e.ReportedToLicenseServer)
            .Take(20)
            .ToListAsync(ct);

        if (!pending.Any()) return 0;

        foreach (var item in pending)
        {
            item.ReportedToLicenseServer = true;
            item.ReportedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync(ct);
        return pending.Count;
    }
}

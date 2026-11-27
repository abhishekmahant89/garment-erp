using GarmentERP.Application.DTOs;
using GarmentERP.Application.Interfaces;
using GarmentERP.Domain.Entities;
using GarmentERP.Infrastructure.Data;
using GarmentERP.Shared.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GarmentERP.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class SyncController : ControllerBase
{
    private readonly GarmentDbContextBase _context;
    private readonly ISyncEngine _syncEngine;

    public SyncController(GarmentDbContextBase context, ISyncEngine syncEngine)
    {
        _context = context;
        _syncEngine = syncEngine;
    }

    [HttpPost("push")]
    public async Task<ActionResult<ApiResponse<SyncPushResponse>>> ReceivePush([FromBody] SyncPushRequest request, CancellationToken ct)
    {
        var confirmedIds = new List<Guid>();
        var errors = new List<string>();

        if (request.Events == null || !request.Events.Any())
        {
            return Ok(ApiResponse<SyncPushResponse>.Ok(new SyncPushResponse(true, confirmedIds, errors)));
        }

        foreach (var ev in request.Events)
        {
            try
            {
                var existing = await _context.SyncInbox
                    .FirstOrDefaultAsync(i => i.EventId == ev.Id, ct);

                if (existing == null)
                {
                    await _context.SyncInbox.AddAsync(new SyncInbox
                    {
                        Id = Guid.NewGuid(),
                        EventId = ev.Id,
                        AggregateType = ev.AggregateType,
                        AggregateId = ev.AggregateId,
                        EventType = ev.EventType,
                        PayloadJson = ev.PayloadJson,
                        Status = SyncStatus.Done,
                        ReceivedAt = DateTime.UtcNow,
                        ProcessedAt = DateTime.UtcNow
                    }, ct);
                }

                confirmedIds.Add(ev.Id);
            }
            catch (Exception ex)
            {
                errors.Add($"Event {ev.Id} error: {ex.Message}");
            }
        }

        await _context.SaveChangesAsync(ct);
        return Ok(ApiResponse<SyncPushResponse>.Ok(new SyncPushResponse(true, confirmedIds, errors), $"Processed {confirmedIds.Count} events."));
    }

    [HttpGet("pull")]
    public async Task<ActionResult<ApiResponse<SyncPullResponse>>> PullUpdates([FromQuery] DateTime? since, CancellationToken ct)
    {
        var cutoff = since ?? DateTime.UtcNow.AddDays(-7);
        var recentProducts = await _context.Products
            .Include(p => p.Variants)
            .Where(p => p.CreatedAt >= cutoff || (p.UpdatedAt.HasValue && p.UpdatedAt.Value >= cutoff))
            .Take(100)
            .ToListAsync(ct);

        var syncItems = new List<SyncItemDto>();
        foreach (var p in recentProducts)
        {
            syncItems.Add(new SyncItemDto(
                p.Id,
                "Product",
                p.Id,
                "Upsert",
                System.Text.Json.JsonSerializer.Serialize(new { p.Id, p.StyleCode, p.Name, VariantsCount = p.Variants.Count }),
                p.UpdatedAt ?? p.CreatedAt,
                null
            ));
        }

        return Ok(ApiResponse<SyncPullResponse>.Ok(new SyncPullResponse(DateTime.UtcNow, syncItems)));
    }

    [HttpGet("stats")]
    public async Task<ActionResult<ApiResponse<object>>> GetStats(CancellationToken ct)
    {
        var stats = await _syncEngine.GetSyncQueueStatisticsAsync(ct);
        return Ok(new
        {
            Success = true,
            Pending = stats.Data.Pending,
            Syncing = stats.Data.Syncing,
            Done = stats.Data.Done,
            Failed = stats.Data.Failed
        });
    }

    [HttpGet("outbox")]
    public async Task<ActionResult<ApiResponse<List<SyncOutbox>>>> GetOutbox([FromQuery] int limit = 50, CancellationToken ct = default)
    {
        var result = await _syncEngine.GetRecentOutboxEntriesAsync(limit, ct);
        return Ok(result);
    }
}

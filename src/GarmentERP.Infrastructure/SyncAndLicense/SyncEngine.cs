using System.Net.Http.Json;
using System.Text.Json;
using GarmentERP.Application.DTOs;
using GarmentERP.Application.Interfaces;
using GarmentERP.Domain.Entities;
using GarmentERP.Infrastructure.Data;
using GarmentERP.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace GarmentERP.Infrastructure.SyncAndLicense;

public class SyncEngine : ISyncEngine
{
    private readonly GarmentDbContextBase _context;
    private readonly HttpClient _httpClient;
    private readonly ILicenseValidator _licenseValidator;

    public SyncEngine(GarmentDbContextBase context, HttpClient httpClient, ILicenseValidator licenseValidator)
    {
        _context = context;
        _httpClient = httpClient;
        _licenseValidator = licenseValidator;
    }

    public async Task<ApiResponse<(int Pending, int Syncing, int Done, int Failed)>> GetSyncQueueStatisticsAsync(CancellationToken ct = default)
    {
        var groups = await _context.SyncOutbox
            .GroupBy(s => s.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        int pending = groups.FirstOrDefault(g => g.Status == SyncStatus.Pending)?.Count ?? 0;
        int syncing = groups.FirstOrDefault(g => g.Status == SyncStatus.Syncing)?.Count ?? 0;
        int done = groups.FirstOrDefault(g => g.Status == SyncStatus.Done)?.Count ?? 0;
        int failed = groups.FirstOrDefault(g => g.Status == SyncStatus.Failed)?.Count ?? 0;

        return ApiResponse<(int, int, int, int)>.Ok((pending, syncing, done, failed));
    }

    public async Task<ApiResponse<List<SyncOutbox>>> GetRecentOutboxEntriesAsync(int limit = 50, CancellationToken ct = default)
    {
        var list = await _context.SyncOutbox
            .OrderByDescending(s => s.CreatedAt)
            .Take(limit)
            .ToListAsync(ct);

        return ApiResponse<List<SyncOutbox>>.Ok(list);
    }

    public async Task<ApiResponse<SyncPushResponse>> PushOutboxEventsAsync(CancellationToken ct = default)
    {
        // 1. Fetch items that are PENDING or FAILED with retry < 5
        var pendingItems = await _context.SyncOutbox
            .Where(s => s.Status == SyncStatus.Pending || (s.Status == SyncStatus.Failed && s.RetryCount < 5))
            .OrderBy(s => s.CreatedAt)
            .Take(50)
            .ToListAsync(ct);

        if (!pendingItems.Any())
        {
            return ApiResponse<SyncPushResponse>.Ok(new SyncPushResponse(true, new List<Guid>(), new List<string>()), "No items to sync.");
        }

        // 2. Mark them as SYNCING in DB
        foreach (var item in pendingItems)
        {
            item.Status = SyncStatus.Syncing;
        }
        await _context.SaveChangesAsync(ct);

        var defaultBranch = await _context.Branches.FirstOrDefaultAsync(ct);
        var license = await _context.LicenseInfos.FirstOrDefaultAsync(ct);

        var syncDtos = pendingItems.Select(p => new SyncItemDto(
            p.Id,
            p.AggregateType,
            p.AggregateId,
            p.EventType,
            p.PayloadJson,
            p.CreatedAt,
            p.Checksum
        )).ToList();

        var request = new SyncPushRequest(
            _licenseValidator.GetMachineFingerprint(),
            license?.LicenseKey ?? "DEFAULT-LICENSE",
            defaultBranch?.Id ?? Guid.Empty,
            syncDtos
        );

        try
        {
            // Attempt to push to Central API
            var response = await _httpClient.PostAsJsonAsync("/api/v1/sync/push", request, ct);
            if (response.IsSuccessStatusCode)
            {
                var pushResult = await response.Content.ReadFromJsonAsync<ApiResponse<SyncPushResponse>>(cancellationToken: ct);
                var confirmedIds = pushResult?.Data?.ConfirmedEventIds ?? syncDtos.Select(s => s.Id).ToList();

                foreach (var item in pendingItems)
                {
                    if (confirmedIds.Contains(item.Id))
                    {
                        item.Status = SyncStatus.Done;
                        item.ProcessedAt = DateTime.UtcNow;
                        item.LastError = null;
                    }
                    else
                    {
                        item.Status = SyncStatus.Failed;
                        item.RetryCount++;
                    }
                }

                await _context.SaveChangesAsync(ct);
                return ApiResponse<SyncPushResponse>.Ok(pushResult?.Data ?? new SyncPushResponse(true, confirmedIds, new List<string>()), "Pushed outbox items successfully.");
            }
            else
            {
                // Network / Server HTTP error: revert to PENDING or FAILED with incremented retry
                var errorText = await response.Content.ReadAsStringAsync(ct);
                foreach (var item in pendingItems)
                {
                    item.Status = SyncStatus.Failed;
                    item.RetryCount++;
                    item.LastError = $"HTTP {(int)response.StatusCode}: {errorText}";
                }
                await _context.SaveChangesAsync(ct);
                return ApiResponse<SyncPushResponse>.Fail($"Central server responded with error: {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            // Network outage (Offline billing mode active)
            foreach (var item in pendingItems)
            {
                item.Status = SyncStatus.Pending; // keep as pending to retry when connection restores
                item.LastError = $"Network unreachable: {ex.Message}";
            }
            await _context.SaveChangesAsync(ct);
            return ApiResponse<SyncPushResponse>.Fail($"Offline Mode: Outbox remains safely queued. Network error: {ex.Message}");
        }
    }

    public async Task<ApiResponse<int>> PullMasterDataDeltasAsync(CancellationToken ct = default)
    {
        try
        {
            var lastSyncTime = await _context.SyncInbox
                .OrderByDescending(i => i.ReceivedAt)
                .Select(i => (DateTime?)i.ReceivedAt)
                .FirstOrDefaultAsync(ct) ?? DateTime.UtcNow.AddYears(-1);

            var response = await _httpClient.GetAsync($"/api/v1/sync/pull?since={lastSyncTime:O}", ct);
            if (!response.IsSuccessStatusCode)
            {
                return ApiResponse<int>.Fail($"Failed to pull updates from server: {response.StatusCode}");
            }

            var result = await response.Content.ReadFromJsonAsync<ApiResponse<SyncPullResponse>>(cancellationToken: ct);
            var changes = result?.Data?.Changes;

            if (changes == null || !changes.Any())
            {
                return ApiResponse<int>.Ok(0, "Local database is up to date.");
            }

            foreach (var change in changes)
            {
                var inbox = new SyncInbox
                {
                    Id = Guid.NewGuid(),
                    EventId = change.Id,
                    AggregateType = change.AggregateType,
                    AggregateId = change.AggregateId,
                    EventType = change.EventType,
                    PayloadJson = change.PayloadJson,
                    Status = SyncStatus.Pending,
                    ReceivedAt = DateTime.UtcNow
                };
                await _context.SyncInbox.AddAsync(inbox, ct);
            }

            await _context.SaveChangesAsync(ct);
            return ApiResponse<int>.Ok(changes.Count, $"Pulled {changes.Count} updates from Central Server.");
        }
        catch (Exception ex)
        {
            return ApiResponse<int>.Fail($"Could not pull from server (Terminal is offline): {ex.Message}");
        }
    }
}

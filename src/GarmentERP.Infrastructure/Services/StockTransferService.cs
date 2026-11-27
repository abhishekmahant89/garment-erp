using System.Text.Json;
using GarmentERP.Application.DTOs;
using GarmentERP.Application.Interfaces;
using GarmentERP.Domain.Entities;
using GarmentERP.Infrastructure.Data;
using GarmentERP.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace GarmentERP.Infrastructure.Services;

public class StockTransferService : IStockTransferService
{
    private readonly GarmentDbContextBase _context;

    public StockTransferService(GarmentDbContextBase context)
    {
        _context = context;
    }

    public async Task<ApiResponse<StockTransferDto>> CreateTransferDispatchAsync(CreateStockTransferRequest request, CancellationToken ct = default)
    {
        if (request.SourceWarehouseId == request.DestinationWarehouseId)
            return ApiResponse<StockTransferDto>.Fail("Source and Destination warehouses must be different.");

        var srcWh = await _context.Warehouses.FindAsync(new object[] { request.SourceWarehouseId }, ct);
        var destWh = await _context.Warehouses.FindAsync(new object[] { request.DestinationWarehouseId }, ct);

        if (srcWh == null || destWh == null)
            return ApiResponse<StockTransferDto>.Fail("Invalid source or destination warehouse specified.");

        if (request.Items == null || !request.Items.Any())
            return ApiResponse<StockTransferDto>.Fail("No items selected for transfer.");

        var transferNo = $"TRF-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..6].ToUpperInvariant()}";

        await using var transaction = await _context.Database.BeginTransactionAsync(ct);
        try
        {
            var transfer = new StockTransfer
            {
                Id = Guid.NewGuid(),
                TransferNumber = transferNo,
                SourceWarehouseId = request.SourceWarehouseId,
                DestinationWarehouseId = request.DestinationWarehouseId,
                DispatchedAt = DateTime.UtcNow,
                DispatchedBy = request.DispatchedBy,
                Notes = request.Notes,
                Status = TransferStatus.InTransit
            };

            await _context.StockTransfers.AddAsync(transfer, ct);

            foreach (var item in request.Items)
            {
                var srcStock = await _context.Stocks
                    .FirstOrDefaultAsync(s => s.VariantId == item.VariantId && s.WarehouseId == request.SourceWarehouseId, ct);

                if (srcStock == null || srcStock.AvailableQuantity < item.Quantity)
                {
                    await transaction.RollbackAsync(ct);
                    return ApiResponse<StockTransferDto>.Fail($"Insufficient stock in source warehouse for variant ID '{item.VariantId}'.");
                }

                // Deduct from source warehouse
                srcStock.Quantity -= item.Quantity;

                await _context.StockTransferItems.AddAsync(new StockTransferItem
                {
                    Id = Guid.NewGuid(),
                    StockTransferId = transfer.Id,
                    VariantId = item.VariantId,
                    QuantityDispatched = item.Quantity,
                    QuantityReceived = 0
                }, ct);

                // Log TransferOut movement
                await _context.StockMovements.AddAsync(new StockMovement
                {
                    Id = Guid.NewGuid(),
                    VariantId = item.VariantId,
                    WarehouseId = request.SourceWarehouseId,
                    MovementType = StockMovementType.TransferOut,
                    Quantity = -item.Quantity,
                    ReferenceNumber = transferNo,
                    Notes = $"Transfer Out to {destWh.Name}"
                }, ct);
            }

            // Sync outbox
            await _context.SyncOutbox.AddAsync(new SyncOutbox
            {
                Id = Guid.NewGuid(),
                AggregateType = "StockTransfer",
                AggregateId = transfer.Id,
                EventType = "Dispatched",
                PayloadJson = JsonSerializer.Serialize(new { transfer.TransferNumber, transfer.SourceWarehouseId, transfer.DestinationWarehouseId }),
                Status = SyncStatus.Pending
            }, ct);

            await _context.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            var dto = new StockTransferDto(
                transfer.Id,
                transfer.TransferNumber,
                srcWh.Name,
                destWh.Name,
                transfer.Status,
                transfer.DispatchedAt,
                null,
                request.Items.Count
            );

            return ApiResponse<StockTransferDto>.Ok(dto, "Stock transfer dispatched and marked In-Transit.");
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(ct);
            return ApiResponse<StockTransferDto>.Fail($"Dispatch failed: {ex.Message}");
        }
    }

    public async Task<ApiResponse<StockTransferDto>> ConfirmTransferReceiveAsync(Guid transferId, string receivedBy, CancellationToken ct = default)
    {
        var transfer = await _context.StockTransfers
            .Include(t => t.SourceWarehouse)
            .Include(t => t.DestinationWarehouse)
            .Include(t => t.Items)
            .FirstOrDefaultAsync(t => t.Id == transferId, ct);

        if (transfer == null)
            return ApiResponse<StockTransferDto>.Fail("Stock transfer record not found.");

        if (transfer.Status != TransferStatus.InTransit)
            return ApiResponse<StockTransferDto>.Fail($"Transfer cannot be received: Current status is {transfer.Status}.");

        await using var transaction = await _context.Database.BeginTransactionAsync(ct);
        try
        {
            transfer.Status = TransferStatus.Received;
            transfer.ReceivedAt = DateTime.UtcNow;
            transfer.ReceivedBy = receivedBy;

            foreach (var item in transfer.Items)
            {
                item.QuantityReceived = item.QuantityDispatched;

                var destStock = await _context.Stocks
                    .FirstOrDefaultAsync(s => s.VariantId == item.VariantId && s.WarehouseId == transfer.DestinationWarehouseId, ct);

                if (destStock == null)
                {
                    destStock = new Stock
                    {
                        Id = Guid.NewGuid(),
                        VariantId = item.VariantId,
                        WarehouseId = transfer.DestinationWarehouseId,
                        BranchId = transfer.DestinationWarehouse?.BranchId ?? Guid.Empty,
                        Quantity = item.QuantityReceived,
                        ReservedQuantity = 0
                    };
                    await _context.Stocks.AddAsync(destStock, ct);
                }
                else
                {
                    destStock.Quantity += item.QuantityReceived;
                }

                // Log TransferIn movement
                await _context.StockMovements.AddAsync(new StockMovement
                {
                    Id = Guid.NewGuid(),
                    VariantId = item.VariantId,
                    WarehouseId = transfer.DestinationWarehouseId,
                    MovementType = StockMovementType.TransferIn,
                    Quantity = item.QuantityReceived,
                    ReferenceNumber = transfer.TransferNumber,
                    Notes = $"Transfer In from {transfer.SourceWarehouse?.Name}"
                }, ct);
            }

            // Sync outbox
            await _context.SyncOutbox.AddAsync(new SyncOutbox
            {
                Id = Guid.NewGuid(),
                AggregateType = "StockTransfer",
                AggregateId = transfer.Id,
                EventType = "Received",
                PayloadJson = JsonSerializer.Serialize(new { transfer.TransferNumber, transfer.DestinationWarehouseId, ReceivedBy = receivedBy }),
                Status = SyncStatus.Pending
            }, ct);

            await _context.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            var dto = new StockTransferDto(
                transfer.Id,
                transfer.TransferNumber,
                transfer.SourceWarehouse?.Name ?? string.Empty,
                transfer.DestinationWarehouse?.Name ?? string.Empty,
                transfer.Status,
                transfer.DispatchedAt,
                transfer.ReceivedAt,
                transfer.Items.Count
            );

            return ApiResponse<StockTransferDto>.Ok(dto, "Stock transfer received and destination inventory updated.");
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(ct);
            return ApiResponse<StockTransferDto>.Fail($"Receipt confirmation error: {ex.Message}");
        }
    }

    public async Task<ApiResponse<List<StockTransferDto>>> GetPendingTransfersAsync(CancellationToken ct = default)
    {
        var list = await _context.StockTransfers
            .Include(t => t.SourceWarehouse)
            .Include(t => t.DestinationWarehouse)
            .Include(t => t.Items)
            .Where(t => t.Status == TransferStatus.InTransit)
            .OrderByDescending(t => t.DispatchedAt)
            .Select(t => new StockTransferDto(
                t.Id,
                t.TransferNumber,
                t.SourceWarehouse != null ? t.SourceWarehouse.Name : string.Empty,
                t.DestinationWarehouse != null ? t.DestinationWarehouse.Name : string.Empty,
                t.Status,
                t.DispatchedAt,
                t.ReceivedAt,
                t.Items.Count
            ))
            .ToListAsync(ct);

        return ApiResponse<List<StockTransferDto>>.Ok(list);
    }
}

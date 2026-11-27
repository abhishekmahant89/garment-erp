using System.Text.Json;
using GarmentERP.Application.DTOs;
using GarmentERP.Application.Interfaces;
using GarmentERP.Domain.Entities;
using GarmentERP.Domain.Services;
using GarmentERP.Infrastructure.Data;
using GarmentERP.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace GarmentERP.Infrastructure.Services;

public class PosBillingService : IPosBillingService
{
    private readonly GarmentDbContextBase _context;
    private readonly IGstCalculator _gstCalculator;

    public PosBillingService(GarmentDbContextBase context, IGstCalculator gstCalculator)
    {
        _context = context;
        _gstCalculator = gstCalculator;
    }

    public async Task<ApiResponse<SaleResponseDto>> ProcessSaleAsync(CreateSaleRequest request, CancellationToken ct = default)
    {
        if (request.Items == null || !request.Items.Any())
        {
            return ApiResponse<SaleResponseDto>.Fail("Sale cannot be processed with zero items.");
        }

        // Verify shift is open
        var shift = await _context.CashierShifts.FindAsync(new object[] { request.CashierShiftId }, ct);
        if (shift == null || shift.Status != ShiftStatus.Open)
        {
            return ApiResponse<SaleResponseDto>.Fail("Cannot bill without an active open cashier shift.");
        }

        // Load variants with HSN details
        var variantIds = request.Items.Select(i => i.VariantId).Distinct().ToList();
        var variants = await _context.ProductVariants
            .Include(v => v.Product)
                .ThenInclude(p => p!.HsnCode)
            .Include(v => v.Size)
            .Include(v => v.Color)
            .Where(v => variantIds.Contains(v.Id))
            .ToDictionaryAsync(v => v.Id, ct);

        // Build list for GST Calculation Engine
        var calcInput = new List<(decimal unitPrice, decimal quantity, decimal discountAmount, decimal baseGstRate, decimal? thresholdAmount, decimal? highGstRate)>();
        foreach (var itemReq in request.Items)
        {
            if (!variants.TryGetValue(itemReq.VariantId, out var variant))
            {
                return ApiResponse<SaleResponseDto>.Fail($"Variant ID '{itemReq.VariantId}' was not found.");
            }

            var hsn = variant.Product?.HsnCode;
            var baseGst = hsn?.GstRate ?? 5.0m;
            var threshold = hsn?.ThresholdAmount;
            var highGst = hsn?.HighGstRate;

            calcInput.Add((itemReq.UnitPrice, itemReq.Quantity, itemReq.DiscountAmount, baseGst, threshold, highGst));
        }

        // Execute Tax & Net Calculations
        var invoiceCalc = _gstCalculator.CalculateInvoice(calcInput, request.InvoiceDiscountAmount, request.IsInterState);

        // Generate Invoice Number: e.g. INV-{Year}-{Month}-{Day}-{TickCount}
        var invoiceNumber = $"INV-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..6].ToUpperInvariant()}";

        // Begin DB Transaction
        await using var transaction = await _context.Database.BeginTransactionAsync(ct);
        try
        {
            var sale = new Sale
            {
                Id = Guid.NewGuid(),
                InvoiceNumber = invoiceNumber,
                BranchId = request.BranchId,
                CashierShiftId = request.CashierShiftId,
                CustomerId = request.CustomerId,
                SubTotal = invoiceCalc.SubTotal,
                DiscountAmount = invoiceCalc.TotalDiscount,
                TaxAmount = invoiceCalc.TotalTax,
                CgstAmount = invoiceCalc.TotalCgst,
                SgstAmount = invoiceCalc.TotalSgst,
                IgstAmount = invoiceCalc.TotalIgst,
                RoundOff = invoiceCalc.RoundOff,
                TotalAmount = invoiceCalc.GrandTotal,
                Status = SaleStatus.Completed,
                Notes = request.Notes
            };

            await _context.Sales.AddAsync(sale, ct);

            var saleItemDtos = new List<SaleItemDto>();

            // Find default warehouse for stock reduction
            var warehouse = await _context.Warehouses
                .FirstOrDefaultAsync(w => w.BranchId == request.BranchId, ct)
                ?? await _context.Warehouses.FirstOrDefaultAsync(ct);

            for (int i = 0; i < request.Items.Count; i++)
            {
                var itemReq = request.Items[i];
                var calculatedLine = invoiceCalc.Items[i];
                var variant = variants[itemReq.VariantId];

                var saleItem = new SaleItem
                {
                    Id = Guid.NewGuid(),
                    SaleId = sale.Id,
                    VariantId = variant.Id,
                    SKU = variant.SKU,
                    Barcode = variant.Barcode,
                    ProductName = variant.Product?.Name ?? "Garment Item",
                    SizeName = variant.Size?.Name ?? string.Empty,
                    ColorName = variant.Color?.Name ?? string.Empty,
                    HsnCode = variant.Product?.HsnCode?.Code ?? "6205",
                    Quantity = itemReq.Quantity,
                    UnitPrice = itemReq.UnitPrice,
                    MRP = variant.MRP,
                    DiscountAmount = calculatedLine.ItemDiscount,
                    TaxableAmount = calculatedLine.TaxableAmount,
                    TaxRate = calculatedLine.TaxRate,
                    CgstAmount = calculatedLine.CgstAmount,
                    SgstAmount = calculatedLine.SgstAmount,
                    IgstAmount = calculatedLine.IgstAmount,
                    TotalAmount = calculatedLine.TotalAmount
                };

                await _context.SaleItems.AddAsync(saleItem, ct);

                // Reduce inventory & log movement
                if (warehouse != null)
                {
                    var stock = await _context.Stocks
                        .FirstOrDefaultAsync(s => s.VariantId == variant.Id && s.WarehouseId == warehouse.Id, ct);

                    if (stock != null)
                    {
                        stock.Quantity -= itemReq.Quantity;
                    }

                    await _context.StockMovements.AddAsync(new StockMovement
                    {
                        Id = Guid.NewGuid(),
                        VariantId = variant.Id,
                        WarehouseId = warehouse.Id,
                        MovementType = StockMovementType.Sale,
                        Quantity = -itemReq.Quantity,
                        UnitCost = variant.CostPrice,
                        ReferenceNumber = invoiceNumber,
                        Notes = $"POS Invoice Sale: {invoiceNumber}"
                    }, ct);
                }

                saleItemDtos.Add(new SaleItemDto(
                    variant.Id,
                    variant.SKU,
                    variant.Barcode,
                    saleItem.ProductName,
                    saleItem.SizeName,
                    saleItem.ColorName,
                    saleItem.HsnCode,
                    saleItem.Quantity,
                    saleItem.UnitPrice,
                    saleItem.MRP,
                    saleItem.DiscountAmount,
                    saleItem.TaxRate,
                    calculatedLine.TotalTax,
                    saleItem.TotalAmount
                ));
            }

            // Save Payments
            if (request.Payments != null && request.Payments.Any())
            {
                foreach (var p in request.Payments)
                {
                    await _context.Payments.AddAsync(new Payment
                    {
                        Id = Guid.NewGuid(),
                        SaleId = sale.Id,
                        Method = p.Method,
                        Amount = p.Amount,
                        ReferenceTransactionId = p.ReferenceTransactionId,
                        PaidAt = DateTime.UtcNow
                    }, ct);

                    if (p.Method == PaymentMethod.Cash)
                    {
                        shift.ExpectedCash += p.Amount;
                    }
                }
            }

            // Write Outbox Entry for synchronization (Status = Pending)
            var outboxPayload = JsonSerializer.Serialize(new
            {
                SaleId = sale.Id,
                InvoiceNumber = sale.InvoiceNumber,
                sale.TotalAmount,
                sale.CgstAmount,
                sale.SgstAmount,
                sale.IgstAmount,
                ItemsCount = request.Items.Count,
                CreatedAt = sale.CreatedAt
            });

            await _context.SyncOutbox.AddAsync(new SyncOutbox
            {
                Id = Guid.NewGuid(),
                AggregateType = "Sale",
                AggregateId = sale.Id,
                EventType = "Created",
                PayloadJson = outboxPayload,
                Status = SyncStatus.Pending,
                RetryCount = 0,
                CreatedAt = DateTime.UtcNow
            }, ct);

            await _context.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            // Automatically post balanced Double-Entry Accounting Journal Voucher
            try
            {
                var accountingService = new AccountingService(_context);
                await accountingService.PostSaleJournalAsync(sale, ct);
            }
            catch
            {
                // Non-blocking fallback
            }

            var responseDto = new SaleResponseDto(
                sale.Id,
                sale.InvoiceNumber,
                sale.SubTotal,
                sale.DiscountAmount,
                invoiceCalc.TaxableAmount,
                sale.CgstAmount,
                sale.SgstAmount,
                sale.IgstAmount,
                sale.RoundOff,
                sale.TotalAmount,
                sale.CreatedAt,
                saleItemDtos
            );

            return ApiResponse<SaleResponseDto>.Ok(responseDto, "Sale processed and outbox queued successfully.");
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(ct);
            return ApiResponse<SaleResponseDto>.Fail($"Transaction failed: {ex.Message}");
        }
    }

    public async Task<ApiResponse<Guid>> HoldSaleAsync(string holdTag, CreateSaleRequest request, CancellationToken ct = default)
    {
        var holdSale = new Sale
        {
            Id = Guid.NewGuid(),
            InvoiceNumber = $"HOLD-{DateTime.UtcNow.Ticks}",
            BranchId = request.BranchId,
            CashierShiftId = request.CashierShiftId,
            CustomerId = request.CustomerId,
            Status = SaleStatus.Held,
            HoldTag = string.IsNullOrWhiteSpace(holdTag) ? $"Cart {DateTime.Now:HH:mm}" : holdTag,
            Notes = JsonSerializer.Serialize(request)
        };

        await _context.Sales.AddAsync(holdSale, ct);
        await _context.SaveChangesAsync(ct);

        return ApiResponse<Guid>.Ok(holdSale.Id, $"Bill parked successfully under tag '{holdSale.HoldTag}'.");
    }

    public async Task<ApiResponse<CreateSaleRequest>> ResumeSaleAsync(Guid holdId, CancellationToken ct = default)
    {
        var heldSale = await _context.Sales.FirstOrDefaultAsync(s => s.Id == holdId && s.Status == SaleStatus.Held, ct);
        if (heldSale == null || string.IsNullOrEmpty(heldSale.Notes))
        {
            return ApiResponse<CreateSaleRequest>.Fail("Held bill not found or expired.");
        }

        var request = JsonSerializer.Deserialize<CreateSaleRequest>(heldSale.Notes);
        if (request == null)
        {
            return ApiResponse<CreateSaleRequest>.Fail("Could not deserialize held bill.");
        }

        // Remove the held record
        _context.Sales.Remove(heldSale);
        await _context.SaveChangesAsync(ct);

        return ApiResponse<CreateSaleRequest>.Ok(request, "Held bill resumed.");
    }

    public async Task<ApiResponse<List<HoldBillDto>>> GetHeldSalesAsync(CancellationToken ct = default)
    {
        var heldSales = await _context.Sales
            .Where(s => s.Status == SaleStatus.Held)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(ct);

        var list = new List<HoldBillDto>();
        foreach (var s in heldSales)
        {
            int itemCount = 0;
            decimal estTotal = 0;
            if (!string.IsNullOrEmpty(s.Notes))
            {
                try
                {
                    var req = JsonSerializer.Deserialize<CreateSaleRequest>(s.Notes);
                    itemCount = req?.Items?.Count ?? 0;
                    estTotal = req?.Items?.Sum(i => i.Quantity * i.UnitPrice) ?? 0;
                }
                catch { }
            }

            list.Add(new HoldBillDto(
                s.Id,
                s.HoldTag ?? "Parked Bill",
                s.CreatedAt,
                estTotal,
                itemCount,
                s.Notes ?? "{}"
            ));
        }

        return ApiResponse<List<HoldBillDto>>.Ok(list);
    }

    public async Task<ApiResponse<CashierShift>> OpenShiftAsync(Guid branchId, string counterNo, Guid cashierId, string cashierName, decimal floatAmount, CancellationToken ct = default)
    {
        var existingOpen = await _context.CashierShifts
            .FirstOrDefaultAsync(s => s.CashierUserId == cashierId && s.Status == ShiftStatus.Open, ct);

        if (existingOpen != null)
        {
            return ApiResponse<CashierShift>.Ok(existingOpen, "Active shift already exists for this cashier.");
        }

        var shift = new CashierShift
        {
            Id = Guid.NewGuid(),
            BranchId = branchId,
            CounterNumber = counterNo,
            CashierUserId = cashierId,
            CashierName = cashierName,
            OpenedAt = DateTime.UtcNow,
            StartingFloat = floatAmount,
            ExpectedCash = floatAmount,
            Status = ShiftStatus.Open
        };

        await _context.CashierShifts.AddAsync(shift, ct);
        await _context.SaveChangesAsync(ct);

        return ApiResponse<CashierShift>.Ok(shift, "Shift opened successfully.");
    }

    public async Task<ApiResponse<CashierShift>> CloseShiftAsync(Guid shiftId, decimal actualCash, string? notes, CancellationToken ct = default)
    {
        var shift = await _context.CashierShifts.FindAsync(new object[] { shiftId }, ct);
        if (shift == null)
            return ApiResponse<CashierShift>.Fail("Shift not found.");

        shift.ClosedAt = DateTime.UtcNow;
        shift.ActualCash = actualCash;
        shift.Status = ShiftStatus.Closed;
        shift.Notes = notes;

        // Queue Outbox for Shift Closure
        await _context.SyncOutbox.AddAsync(new SyncOutbox
        {
            Id = Guid.NewGuid(),
            AggregateType = "CashierShift",
            AggregateId = shift.Id,
            EventType = "Closed",
            PayloadJson = JsonSerializer.Serialize(new { shift.Id, shift.CounterNumber, shift.CashierName, shift.StartingFloat, shift.ExpectedCash, shift.ActualCash, shift.Discrepancy }),
            Status = SyncStatus.Pending
        }, ct);

        await _context.SaveChangesAsync(ct);
        return ApiResponse<CashierShift>.Ok(shift, "Shift closed successfully with Day-End summary.");
    }
}

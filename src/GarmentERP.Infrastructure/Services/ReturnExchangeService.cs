using System.Text.Json;
using GarmentERP.Application.DTOs;
using GarmentERP.Application.Interfaces;
using GarmentERP.Domain.Entities;
using GarmentERP.Infrastructure.Data;
using GarmentERP.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace GarmentERP.Infrastructure.Services;

public class ReturnExchangeService : IReturnExchangeService
{
    private readonly GarmentDbContextBase _context;
    private readonly IPosBillingService _billingService;

    public ReturnExchangeService(GarmentDbContextBase context, IPosBillingService billingService)
    {
        _context = context;
        _billingService = billingService;
    }

    public async Task<ApiResponse<ReturnExchangeResponseDto>> ProcessReturnExchangeAsync(ProcessReturnExchangeRequest request, CancellationToken ct = default)
    {
        var originalSale = await _context.Sales
            .Include(s => s.Items)
            .Include(s => s.Customer)
            .FirstOrDefaultAsync(s => s.Id == request.OriginalSaleId, ct);

        if (originalSale == null)
        {
            return ApiResponse<ReturnExchangeResponseDto>.Fail("Original sales invoice not found.");
        }

        // Return Policy check: 14 days
        if ((DateTime.UtcNow - originalSale.CreatedAt).TotalDays > 14)
        {
            return ApiResponse<ReturnExchangeResponseDto>.Fail("Return window expired: original invoice is older than the 14-day policy limit.");
        }

        if (request.ReturnItems == null || !request.ReturnItems.Any())
        {
            return ApiResponse<ReturnExchangeResponseDto>.Fail("Must provide at least one item to return.");
        }

        var warehouse = await _context.Warehouses.FirstOrDefaultAsync(w => w.BranchId == request.BranchId, ct)
                     ?? await _context.Warehouses.FirstOrDefaultAsync(ct);

        await using var transaction = await _context.Database.BeginTransactionAsync(ct);
        try
        {
            var returnNumber = $"RET-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..6].ToUpperInvariant()}";
            decimal totalReturnValue = 0;

            var returnInvoice = new ReturnInvoice
            {
                Id = Guid.NewGuid(),
                ReturnNumber = returnNumber,
                OriginalSaleId = originalSale.Id,
                BranchId = request.BranchId,
                CashierShiftId = request.CashierShiftId,
                Reason = "Customer Exchange / Return"
            };

            await _context.ReturnInvoices.AddAsync(returnInvoice, ct);

            foreach (var retItem in request.ReturnItems)
            {
                var lineRefund = retItem.Quantity * retItem.UnitRefundPrice;
                totalReturnValue += lineRefund;

                await _context.ReturnItems.AddAsync(new ReturnItem
                {
                    Id = Guid.NewGuid(),
                    ReturnInvoiceId = returnInvoice.Id,
                    VariantId = retItem.VariantId,
                    Quantity = retItem.Quantity,
                    RefundAmount = lineRefund,
                    Condition = "Resellable"
                }, ct);

                // Re-stock inventory
                if (warehouse != null)
                {
                    var stock = await _context.Stocks
                        .FirstOrDefaultAsync(s => s.VariantId == retItem.VariantId && s.WarehouseId == warehouse.Id, ct);

                    if (stock != null)
                    {
                        stock.Quantity += retItem.Quantity;
                    }

                    await _context.StockMovements.AddAsync(new StockMovement
                    {
                        Id = Guid.NewGuid(),
                        VariantId = retItem.VariantId,
                        WarehouseId = warehouse.Id,
                        MovementType = StockMovementType.CustomerReturn,
                        Quantity = retItem.Quantity,
                        ReferenceNumber = returnNumber,
                        Notes = $"Customer Return from Invoice {originalSale.InvoiceNumber}"
                    }, ct);
                }
            }

            returnInvoice.TotalRefundAmount = totalReturnValue;

            decimal totalExchangeValue = 0;
            string? newInvoiceNo = null;
            string? creditNoteNo = null;
            decimal netPayableOrRefund = 0;

            // Handle Exchange Items if present
            if (request.ExchangeItems != null && request.ExchangeItems.Any())
            {
                var exchangeGross = request.ExchangeItems.Sum(i => i.Quantity * i.UnitPrice);
                totalExchangeValue = exchangeGross;

                if (totalExchangeValue >= totalReturnValue)
                {
                    // Customer pays difference
                    netPayableOrRefund = totalExchangeValue - totalReturnValue;
                    var saleRequest = new CreateSaleRequest(
                        request.BranchId,
                        request.CashierShiftId,
                        originalSale.CustomerId,
                        request.ExchangeItems,
                        request.Payments ?? new List<PaymentRequest>(),
                        InvoiceDiscountAmount: totalReturnValue, // offset return value as discount credit
                        IsInterState: false,
                        Notes: $"Exchange against Return {returnNumber}"
                    );

                    var saleResult = await _billingService.ProcessSaleAsync(saleRequest, ct);
                    if (saleResult.Success && saleResult.Data != null)
                    {
                        newInvoiceNo = saleResult.Data.InvoiceNumber;
                    }
                }
                else
                {
                    // Return Value > Exchange Value: Issue Credit Note for difference
                    var creditAmount = totalReturnValue - totalExchangeValue;
                    creditNoteNo = $"CN-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..6].ToUpperInvariant()}";

                    var creditNote = new CreditNote
                    {
                        Id = Guid.NewGuid(),
                        NoteNumber = creditNoteNo,
                        CustomerId = originalSale.CustomerId ?? Guid.Empty,
                        OriginalSaleId = originalSale.Id,
                        Amount = creditAmount,
                        BalanceAmount = creditAmount,
                        ExpiryDate = DateTime.UtcNow.AddDays(60)
                    };

                    await _context.CreditNotes.AddAsync(creditNote, ct);
                    netPayableOrRefund = -creditAmount;
                }
            }
            else
            {
                // Pure Return (No immediate exchange item): Issue full Store Credit Note
                creditNoteNo = $"CN-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..6].ToUpperInvariant()}";
                var creditNote = new CreditNote
                {
                    Id = Guid.NewGuid(),
                    NoteNumber = creditNoteNo,
                    CustomerId = originalSale.CustomerId ?? Guid.Empty,
                    OriginalSaleId = originalSale.Id,
                    Amount = totalReturnValue,
                    BalanceAmount = totalReturnValue,
                    ExpiryDate = DateTime.UtcNow.AddDays(60)
                };

                await _context.CreditNotes.AddAsync(creditNote, ct);
                netPayableOrRefund = -totalReturnValue;
            }

            // Sync Outbox event
            await _context.SyncOutbox.AddAsync(new SyncOutbox
            {
                Id = Guid.NewGuid(),
                AggregateType = "ReturnInvoice",
                AggregateId = returnInvoice.Id,
                EventType = "Created",
                PayloadJson = JsonSerializer.Serialize(new { ReturnNumber = returnNumber, TotalRefundAmount = totalReturnValue, NewInvoice = newInvoiceNo, CreditNote = creditNoteNo }),
                Status = SyncStatus.Pending,
                CreatedAt = DateTime.UtcNow
            }, ct);

            await _context.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            var response = new ReturnExchangeResponseDto(
                returnNumber,
                newInvoiceNo,
                totalReturnValue,
                totalExchangeValue,
                netPayableOrRefund,
                creditNoteNo,
                DateTime.UtcNow
            );

            return ApiResponse<ReturnExchangeResponseDto>.Ok(response, "Return / Exchange processed successfully.");
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(ct);
            return ApiResponse<ReturnExchangeResponseDto>.Fail($"Return processing error: {ex.Message}");
        }
    }

    public async Task<ApiResponse<CreditNote?>> ValidateCreditNoteAsync(string creditNoteNumber, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(creditNoteNumber))
            return ApiResponse<CreditNote?>.Fail("Credit note number required.");

        var note = await _context.CreditNotes
            .Include(c => c.Customer)
            .FirstOrDefaultAsync(c => c.NoteNumber == creditNoteNumber.Trim(), ct);

        if (note == null)
            return ApiResponse<CreditNote?>.Fail("Credit note not found.");

        if (note.IsRedeemed)
            return ApiResponse<CreditNote?>.Fail("Credit note has already been fully redeemed.");

        if (DateTime.UtcNow > note.ExpiryDate)
            return ApiResponse<CreditNote?>.Fail($"Credit note expired on {note.ExpiryDate:dd-MMM-yyyy}.");

        return ApiResponse<CreditNote?>.Ok(note, $"Valid credit note with balance Rs.{note.BalanceAmount:N2}");
    }
}

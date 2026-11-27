using System.Text.Json;
using GarmentERP.Application.DTOs;
using GarmentERP.Application.Interfaces;
using GarmentERP.Domain.Entities;
using GarmentERP.Infrastructure.Data;
using GarmentERP.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace GarmentERP.Infrastructure.Services;

public class PurchaseService : IPurchaseService
{
    private readonly GarmentDbContextBase _context;

    public PurchaseService(GarmentDbContextBase context)
    {
        _context = context;
    }

    public async Task<ApiResponse<SupplierDto>> CreateSupplierAsync(CreateSupplierDto dto, CancellationToken ct = default)
    {
        var supplier = new Supplier
        {
            Id = Guid.NewGuid(),
            Name = dto.Name.Trim(),
            Code = dto.Code.Trim().ToUpperInvariant(),
            ContactPerson = dto.ContactPerson,
            Phone = dto.Phone,
            Email = dto.Email,
            Address = dto.Address,
            City = dto.City,
            State = dto.State,
            Gstin = dto.Gstin,
            CurrentBalance = 0
        };

        await _context.Suppliers.AddAsync(supplier, ct);
        await _context.SaveChangesAsync(ct);

        return ApiResponse<SupplierDto>.Ok(new SupplierDto(
            supplier.Id, supplier.Name, supplier.Code, supplier.ContactPerson,
            supplier.Phone, supplier.Email, supplier.City, supplier.Gstin, supplier.CurrentBalance
        ), "Supplier created successfully.");
    }

    public async Task<ApiResponse<List<SupplierDto>>> GetSuppliersAsync(CancellationToken ct = default)
    {
        var list = await _context.Suppliers
            .OrderBy(s => s.Name)
            .Select(s => new SupplierDto(
                s.Id, s.Name, s.Code, s.ContactPerson, s.Phone, s.Email, s.City, s.Gstin, s.CurrentBalance
            ))
            .ToListAsync(ct);

        return ApiResponse<List<SupplierDto>>.Ok(list);
    }

    public async Task<ApiResponse<PurchaseInvoiceDto>> ProcessPurchaseInvoiceAsync(CreatePurchaseInvoiceRequest request, CancellationToken ct = default)
    {
        var supplier = await _context.Suppliers.FindAsync(new object[] { request.SupplierId }, ct);
        if (supplier == null)
            return ApiResponse<PurchaseInvoiceDto>.Fail("Supplier not found.");

        if (request.Items == null || !request.Items.Any())
            return ApiResponse<PurchaseInvoiceDto>.Fail("Purchase invoice must contain at least one item.");

        var erpPoNumber = $"PO-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..6].ToUpperInvariant()}";

        await using var transaction = await _context.Database.BeginTransactionAsync(ct);
        try
        {
            decimal subTotal = 0;
            decimal totalTax = 0;

            var invoice = new PurchaseInvoice
            {
                Id = Guid.NewGuid(),
                ErpPurchaseNumber = erpPoNumber,
                InvoiceNumber = request.SupplierInvoiceNumber,
                SupplierId = request.SupplierId,
                BranchId = request.BranchId,
                WarehouseId = request.WarehouseId,
                InvoiceDate = request.InvoiceDate,
                ReceivedDate = DateTime.UtcNow,
                PaidAmount = request.PaidAmount,
                Notes = request.Notes,
                Status = PurchaseStatus.Received
            };

            await _context.PurchaseInvoices.AddAsync(invoice, ct);

            foreach (var item in request.Items)
            {
                var variant = await _context.ProductVariants.FindAsync(new object[] { item.VariantId }, ct);
                if (variant == null)
                    continue;

                // Update variant prices if updated on purchase
                variant.CostPrice = item.UnitCostPrice;
                variant.MRP = item.MRP;
                variant.SellingPrice = item.SellingPrice;

                var lineGross = item.QuantityReceived * item.UnitCostPrice;
                var lineTax = Math.Round(lineGross * (item.TaxRate / 100m), 2);
                var lineTotal = lineGross + lineTax;

                subTotal += lineGross;
                totalTax += lineTax;

                await _context.PurchaseItems.AddAsync(new PurchaseItem
                {
                    Id = Guid.NewGuid(),
                    PurchaseInvoiceId = invoice.Id,
                    VariantId = item.VariantId,
                    QuantityReceived = item.QuantityReceived,
                    UnitCostPrice = item.UnitCostPrice,
                    MRP = item.MRP,
                    SellingPrice = item.SellingPrice,
                    TaxRate = item.TaxRate,
                    TaxAmount = lineTax,
                    TotalAmount = lineTotal
                }, ct);

                // Add to inventory stock
                var stock = await _context.Stocks
                    .FirstOrDefaultAsync(s => s.VariantId == item.VariantId && s.WarehouseId == request.WarehouseId, ct);

                if (stock == null)
                {
                    stock = new Stock
                    {
                        Id = Guid.NewGuid(),
                        VariantId = item.VariantId,
                        WarehouseId = request.WarehouseId,
                        BranchId = request.BranchId,
                        Quantity = item.QuantityReceived,
                        ReservedQuantity = 0
                    };
                    await _context.Stocks.AddAsync(stock, ct);
                }
                else
                {
                    stock.Quantity += item.QuantityReceived;
                }

                // Log inventory movement
                await _context.StockMovements.AddAsync(new StockMovement
                {
                    Id = Guid.NewGuid(),
                    VariantId = item.VariantId,
                    WarehouseId = request.WarehouseId,
                    MovementType = StockMovementType.Purchase,
                    Quantity = item.QuantityReceived,
                    UnitCost = item.UnitCostPrice,
                    ReferenceNumber = erpPoNumber,
                    Notes = $"Purchase Receipt: {request.SupplierInvoiceNumber}"
                }, ct);
            }

            invoice.SubTotal = subTotal;
            invoice.TaxAmount = totalTax;
            invoice.TotalAmount = subTotal + totalTax;

            // Update supplier balance
            supplier.CurrentBalance += invoice.BalanceAmount;

            // Queue outbox event
            await _context.SyncOutbox.AddAsync(new SyncOutbox
            {
                Id = Guid.NewGuid(),
                AggregateType = "PurchaseInvoice",
                AggregateId = invoice.Id,
                EventType = "Created",
                PayloadJson = JsonSerializer.Serialize(new { invoice.ErpPurchaseNumber, SupplierInvoiceNumber = invoice.InvoiceNumber, invoice.TotalAmount, invoice.BalanceAmount }),
                Status = SyncStatus.Pending
            }, ct);

            await _context.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            var dto = new PurchaseInvoiceDto(
                invoice.Id,
                invoice.ErpPurchaseNumber,
                invoice.InvoiceNumber,
                supplier.Name,
                invoice.InvoiceDate,
                invoice.SubTotal,
                invoice.TaxAmount,
                invoice.TotalAmount,
                invoice.PaidAmount,
                invoice.BalanceAmount,
                invoice.Status,
                request.Items.Count
            );

            return ApiResponse<PurchaseInvoiceDto>.Ok(dto, "Purchase invoice processed and inventory received successfully.");
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(ct);
            return ApiResponse<PurchaseInvoiceDto>.Fail($"Error processing purchase invoice: {ex.Message}");
        }
    }

    public async Task<ApiResponse<List<PurchaseInvoiceDto>>> GetRecentPurchasesAsync(int limit = 50, CancellationToken ct = default)
    {
        var list = await _context.PurchaseInvoices
            .Include(p => p.Supplier)
            .Include(p => p.Items)
            .OrderByDescending(p => p.InvoiceDate)
            .Take(limit)
            .Select(p => new PurchaseInvoiceDto(
                p.Id,
                p.ErpPurchaseNumber,
                p.InvoiceNumber,
                p.Supplier != null ? p.Supplier.Name : string.Empty,
                p.InvoiceDate,
                p.SubTotal,
                p.TaxAmount,
                p.TotalAmount,
                p.PaidAmount,
                p.TotalAmount - p.PaidAmount,
                p.Status,
                p.Items.Count
            ))
            .ToListAsync(ct);

        return ApiResponse<List<PurchaseInvoiceDto>>.Ok(list);
    }
}

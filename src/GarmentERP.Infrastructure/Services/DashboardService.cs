using GarmentERP.Application.DTOs;
using GarmentERP.Application.Interfaces;
using GarmentERP.Domain.Entities;
using GarmentERP.Infrastructure.Data;
using GarmentERP.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace GarmentERP.Infrastructure.Services;

public class DashboardService : IDashboardService
{
    private readonly GarmentDbContextBase _context;

    public DashboardService(GarmentDbContextBase context)
    {
        _context = context;
    }

    public async Task<ApiResponse<StoreKpiSummary>> GetStoreKpisAsync(Guid branchId, CancellationToken ct = default)
    {
        var today = DateTime.UtcNow.Date;
        var tomorrow = today.AddDays(1);

        var todaySales = await _context.Sales
            .Include(s => s.Payments)
            .Where(s => s.BranchId == branchId && s.CreatedAt >= today && s.CreatedAt < tomorrow && s.Status == SaleStatus.Completed)
            .ToListAsync(ct);

        decimal netSales = todaySales.Sum(s => s.TotalAmount);
        int billsCount = todaySales.Count;

        var payments = todaySales.SelectMany(s => s.Payments).ToList();
        decimal cash = payments.Where(p => p.Method == PaymentMethod.Cash).Sum(p => p.Amount);
        decimal upi = payments.Where(p => p.Method == PaymentMethod.UPI).Sum(p => p.Amount);
        decimal card = payments.Where(p => p.Method == PaymentMethod.Card).Sum(p => p.Amount);

        // Low stock count
        var lowStockCount = await _context.Stocks
            .Include(s => s.Variant)
            .Where(s => s.BranchId == branchId && s.Variant != null && s.Quantity <= s.Variant.ReorderLevel)
            .CountAsync(ct);

        int totalVariants = await _context.ProductVariants.CountAsync(ct);

        var summary = new StoreKpiSummary(
            netSales,
            billsCount,
            cash,
            upi,
            card,
            lowStockCount,
            totalVariants
        );

        return ApiResponse<StoreKpiSummary>.Ok(summary);
    }

    public async Task<ApiResponse<List<LowStockAlertDto>>> GetLowStockAlertsAsync(Guid branchId, CancellationToken ct = default)
    {
        var list = await _context.Stocks
            .Include(s => s.Variant)
                .ThenInclude(v => v!.Product)
            .Include(s => s.Variant)
                .ThenInclude(v => v!.Size)
            .Include(s => s.Variant)
                .ThenInclude(v => v!.Color)
            .Where(s => s.BranchId == branchId && s.Variant != null && s.Quantity <= s.Variant.ReorderLevel)
            .OrderBy(s => s.Quantity)
            .Take(30)
            .Select(s => new LowStockAlertDto(
                s.VariantId,
                s.Variant!.SKU,
                s.Variant.Barcode,
                s.Variant.Product != null ? s.Variant.Product.Name : "Garment",
                s.Variant.Size != null ? s.Variant.Size.Name : string.Empty,
                s.Variant.Color != null ? s.Variant.Color.Name : string.Empty,
                s.Quantity,
                s.Variant.ReorderLevel
            ))
            .ToListAsync(ct);

        return ApiResponse<List<LowStockAlertDto>>.Ok(list);
    }

    public async Task<ApiResponse<List<TopSellingGarmentDto>>> GetTopSellingStylesAsync(Guid branchId, int limit = 5, CancellationToken ct = default)
    {
        var topStyles = await _context.SaleItems
            .Include(i => i.Sale)
            .Include(i => i.Variant)
                .ThenInclude(v => v!.Product)
            .Where(i => i.Sale != null && i.Sale.BranchId == branchId && i.Sale.Status == SaleStatus.Completed)
            .GroupBy(i => new { i.Variant!.Product!.StyleCode, i.Variant.Product.Name })
            .Select(g => new TopSellingGarmentDto(
                g.Key.StyleCode,
                g.Key.Name,
                (int)g.Sum(x => x.Quantity),
                g.Sum(x => x.TotalAmount)
            ))
            .OrderByDescending(t => t.UnitsSold)
            .Take(limit)
            .ToListAsync(ct);

        return ApiResponse<List<TopSellingGarmentDto>>.Ok(topStyles);
    }
}

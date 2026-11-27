using GarmentERP.Application.DTOs;
using GarmentERP.Application.Interfaces;
using GarmentERP.Domain.Entities;
using GarmentERP.Infrastructure.Data;
using GarmentERP.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace GarmentERP.Infrastructure.Services;

public class EnterpriseReportService : IEnterpriseReportService
{
    private readonly GarmentDbContextBase _context;

    public EnterpriseReportService(GarmentDbContextBase context)
    {
        _context = context;
    }

    public async Task<ApiResponse<DailySalesReport>> GetDailySalesReportAsync(Guid branchId, DateTime date, CancellationToken ct = default)
    {
        var start = date.Date;
        var end = start.AddDays(1);

        var sales = await _context.Sales
            .Include(s => s.Items)
            .Include(s => s.Payments)
            .Where(s => s.BranchId == branchId && s.CreatedAt >= start && s.CreatedAt < end && s.Status == SaleStatus.Completed)
            .ToListAsync(ct);

        int totalInvoices = sales.Count;
        decimal grossSales = sales.Sum(s => s.SubTotal);
        decimal totalDiscounts = sales.Sum(s => s.DiscountAmount);
        decimal netSales = sales.Sum(s => s.TotalAmount);
        decimal totalTax = sales.Sum(s => s.TaxAmount);

        var payments = sales.SelectMany(s => s.Payments).ToList();
        decimal cash = payments.Where(p => p.Method == PaymentMethod.Cash).Sum(p => p.Amount);
        decimal card = payments.Where(p => p.Method == PaymentMethod.Card).Sum(p => p.Amount);
        decimal upi = payments.Where(p => p.Method == PaymentMethod.UPI).Sum(p => p.Amount);
        int itemsSold = (int)sales.SelectMany(s => s.Items).Sum(i => i.Quantity);

        var report = new DailySalesReport(
            date.Date,
            totalInvoices,
            grossSales,
            totalDiscounts,
            netSales,
            totalTax,
            cash,
            card,
            upi,
            itemsSold
        );

        return ApiResponse<DailySalesReport>.Ok(report);
    }

    public async Task<ApiResponse<StockValuationReport>> GetStockValuationReportAsync(Guid? warehouseId = null, CancellationToken ct = default)
    {
        var query = _context.Stocks
            .Include(s => s.Variant)
            .AsQueryable();

        if (warehouseId.HasValue)
        {
            query = query.Where(s => s.WarehouseId == warehouseId.Value);
        }

        var stocks = await query.ToListAsync(ct);

        int uniqueSkus = stocks.Select(s => s.VariantId).Distinct().Count();
        decimal totalQty = stocks.Sum(s => s.Quantity);
        decimal totalCost = stocks.Sum(s => s.Quantity * (s.Variant?.CostPrice ?? 0));
        decimal totalRetail = stocks.Sum(s => s.Quantity * (s.Variant?.SellingPrice ?? 0));

        decimal marginPercentage = totalRetail > 0
            ? Math.Round(((totalRetail - totalCost) / totalRetail) * 100m, 2)
            : 0;

        var report = new StockValuationReport(
            uniqueSkus,
            totalQty,
            totalCost,
            totalRetail,
            marginPercentage
        );

        return ApiResponse<StockValuationReport>.Ok(report);
    }

    public async Task<ApiResponse<ProfitAndLossReport>> GetProfitAndLossReportAsync(DateTime fromDate, DateTime toDate, CancellationToken ct = default)
    {
        var start = fromDate.Date;
        var end = toDate.Date.AddDays(1);

        var sales = await _context.Sales
            .Include(s => s.Items)
                .ThenInclude(i => i.Variant)
            .Where(s => s.CreatedAt >= start && s.CreatedAt < end && s.Status == SaleStatus.Completed)
            .ToListAsync(ct);

        decimal totalRevenue = sales.Sum(s => s.TotalAmount - s.TaxAmount); // Net Revenue excluding collected GST

        decimal totalCogs = 0;
        foreach (var sale in sales)
        {
            foreach (var item in sale.Items)
            {
                var cost = item.Variant?.CostPrice ?? (item.UnitPrice * 0.5m);
                totalCogs += item.Quantity * cost;
            }
        }

        decimal grossProfit = totalRevenue - totalCogs;
        decimal grossMargin = totalRevenue > 0 ? Math.Round((grossProfit / totalRevenue) * 100m, 2) : 0;

        var expenses = await _context.Expenses
            .Where(e => e.ExpenseDate >= start && e.ExpenseDate < end)
            .SumAsync(e => e.Amount, ct);

        decimal netProfit = grossProfit - expenses;

        var report = new ProfitAndLossReport(
            fromDate,
            toDate,
            totalRevenue,
            totalCogs,
            grossProfit,
            grossMargin,
            expenses,
            netProfit
        );

        return ApiResponse<ProfitAndLossReport>.Ok(report);
    }
}

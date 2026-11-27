using GarmentERP.Application.DTOs;
using GarmentERP.Application.Interfaces;
using GarmentERP.Domain.Entities;
using GarmentERP.Infrastructure.Data;
using GarmentERP.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace GarmentERP.Infrastructure.Services;

public class GstReportService : IGstReportService
{
    private readonly GarmentDbContextBase _context;

    public GstReportService(GarmentDbContextBase context)
    {
        _context = context;
    }

    public async Task<ApiResponse<Gstr1ReportDto>> GenerateGstr1ReportAsync(DateTime fromDate, DateTime toDate, Guid? branchId = null, CancellationToken ct = default)
    {
        var start = fromDate.Date;
        var end = toDate.Date.AddDays(1);

        var query = _context.SaleItems
            .Include(i => i.Sale)
            .Where(i => i.Sale != null && i.Sale.CreatedAt >= start && i.Sale.CreatedAt < end && i.Sale.Status == SaleStatus.Completed);

        if (branchId.HasValue)
        {
            query = query.Where(i => i.Sale!.BranchId == branchId.Value);
        }

        var items = await query.ToListAsync(ct);

        // Group by HSN Code
        var hsnGroups = items
            .GroupBy(i => string.IsNullOrEmpty(i.HsnCode) ? "6205" : i.HsnCode)
            .Select(g => new Gstr1HsnSummaryRow(
                HsnCode: g.Key,
                Description: g.Key == "6109" ? "T-Shirts (Knitted)" : "Shirts / Apparel (Woven)",
                TotalQuantity: g.Sum(x => x.Quantity),
                TotalValue: g.Sum(x => x.TotalAmount),
                TaxableValue: g.Sum(x => x.TaxableAmount),
                CgstAmount: g.Sum(x => x.CgstAmount),
                SgstAmount: g.Sum(x => x.SgstAmount),
                IgstAmount: g.Sum(x => x.IgstAmount)
            ))
            .OrderBy(r => r.HsnCode)
            .ToList();

        decimal totalTaxable = hsnGroups.Sum(g => g.TaxableValue);
        decimal totalCgst = hsnGroups.Sum(g => g.CgstAmount);
        decimal totalSgst = hsnGroups.Sum(g => g.SgstAmount);
        decimal totalIgst = hsnGroups.Sum(g => g.IgstAmount);
        decimal totalTax = totalCgst + totalSgst + totalIgst;

        var report = new Gstr1ReportDto(
            fromDate,
            toDate,
            totalTaxable,
            totalCgst,
            totalSgst,
            totalIgst,
            totalTax,
            hsnGroups
        );

        return ApiResponse<Gstr1ReportDto>.Ok(report);
    }
}

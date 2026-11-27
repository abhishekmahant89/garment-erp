using System.Text;
using GarmentERP.Application.DTOs;
using GarmentERP.Application.Interfaces;
using GarmentERP.Domain.Entities;
using GarmentERP.Infrastructure.Data;
using GarmentERP.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace GarmentERP.Infrastructure.Services;

public class ShiftReportingService : IShiftReportingService
{
    private readonly GarmentDbContextBase _context;

    public ShiftReportingService(GarmentDbContextBase context)
    {
        _context = context;
    }

    public async Task<ApiResponse<ShiftZReportDto>> GenerateShiftZReportAsync(Guid shiftId, CancellationToken ct = default)
    {
        var shift = await _context.CashierShifts.FindAsync(new object[] { shiftId }, ct);
        if (shift == null)
            return ApiResponse<ShiftZReportDto>.Fail("Cashier shift not found.");

        var sales = await _context.Sales
            .Include(s => s.Payments)
            .Where(s => s.CashierShiftId == shiftId && s.Status == SaleStatus.Completed)
            .ToListAsync(ct);

        var payments = sales.SelectMany(s => s.Payments).ToList();

        decimal cashSales = payments.Where(p => p.Method == PaymentMethod.Cash).Sum(p => p.Amount);
        decimal cardSales = payments.Where(p => p.Method == PaymentMethod.Card).Sum(p => p.Amount);
        decimal upiSales = payments.Where(p => p.Method == PaymentMethod.UPI).Sum(p => p.Amount);
        decimal creditSales = payments.Where(p => p.Method == PaymentMethod.StoreCredit).Sum(p => p.Amount);

        var returns = await _context.ReturnInvoices
            .Where(r => r.CashierShiftId == shiftId)
            .SumAsync(r => r.TotalRefundAmount, ct);

        decimal netSales = sales.Sum(s => s.TotalAmount);
        decimal expectedCash = shift.StartingFloat + cashSales;
        decimal discrepancy = shift.ActualCash - expectedCash;

        var dto = new ShiftZReportDto(
            shift.Id,
            shift.CounterNumber,
            shift.CashierName,
            shift.OpenedAt,
            shift.ClosedAt ?? DateTime.UtcNow,
            shift.StartingFloat,
            cashSales,
            cardSales,
            upiSales,
            creditSales,
            returns,
            netSales,
            expectedCash,
            shift.ActualCash,
            discrepancy,
            sales.Count
        );

        return ApiResponse<ShiftZReportDto>.Ok(dto);
    }

    public string FormatZReportReceipt(ShiftZReportDto report)
    {
        int w = 48; // 80mm
        var sb = new StringBuilder();

        sb.AppendLine(Center("========================================", w));
        sb.AppendLine(Center("CASHIER SHIFT CLOSING (Z-REPORT)", w));
        sb.AppendLine(Center("========================================", w));
        sb.AppendLine($"Counter: {report.CounterNumber,-15} Cashier: {report.CashierName}");
        sb.AppendLine($"Opened : {report.OpenedAt:dd/MM/yyyy HH:mm}");
        sb.AppendLine($"Closed : {report.ClosedAt:dd/MM/yyyy HH:mm}");
        sb.AppendLine(new string('-', w));

        sb.AppendLine($"Total Bills Processed: {report.BillsCount,23}");
        sb.AppendLine($"Opening Float Cash   : Rs.{report.StartingFloat,18:N2}");
        sb.AppendLine(new string('-', w));

        sb.AppendLine("SALES BREAKDOWN BY PAYMENT MODE:");
        sb.AppendLine($"  Cash Tendered      : Rs.{report.CashSales,18:N2}");
        sb.AppendLine($"  Card Payments      : Rs.{report.CardSales,18:N2}");
        sb.AppendLine($"  UPI / QR Code      : Rs.{report.UpiSales,18:N2}");
        sb.AppendLine($"  Store Credit Used  : Rs.{report.StoreCreditSales,18:N2}");
        sb.AppendLine(new string('-', w));
        sb.AppendLine($"GROSS NET SALES      : Rs.{report.NetSales,18:N2}");
        sb.AppendLine($"Customer Returns     : Rs.{report.TotalReturns,18:N2}");
        sb.AppendLine(new string('=', w));

        sb.AppendLine("DRAWER RECONCILIATION:");
        sb.AppendLine($"  Expected Cash      : Rs.{report.ExpectedCashInDrawer,18:N2}");
        sb.AppendLine($"  Actual Counted Cash: Rs.{report.ActualCashCounted,18:N2}");
        sb.AppendLine(new string('-', w));

        var discTitle = report.Discrepancy >= 0 ? "SURPLUS (+)" : "SHORTAGE (-)";
        sb.AppendLine($"  Cash Discrepancy   : Rs.{report.Discrepancy,18:N2} ({discTitle})");
        sb.AppendLine(new string('=', w));
        sb.AppendLine();
        sb.AppendLine(Center("Cashier Signature: __________________", w));
        sb.AppendLine();
        sb.AppendLine(Center("Manager Signature: __________________", w));
        sb.AppendLine();

        return sb.ToString();
    }

    private static string Center(string text, int width)
    {
        if (text.Length >= width) return text[..width];
        int left = (width - text.Length) / 2;
        return text.PadLeft(left + text.Length).PadRight(width);
    }
}

using GarmentERP.Application.DTOs;
using GarmentERP.Domain.Entities;
using GarmentERP.Domain.Services;
using GarmentERP.Infrastructure.Data;
using GarmentERP.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GarmentERP.Tests;

public class GstAndAccountingTests
{
    private GarmentOfflineDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<GarmentOfflineDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        var context = new GarmentOfflineDbContext(options);
        context.Database.OpenConnection();
        context.Database.EnsureCreated();
        return context;
    }

    [Fact]
    public async Task Gstr1Report_AggregatesHsnSummariesCorrectly()
    {
        await using var context = CreateInMemoryDbContext();
        await DatabaseSeeder.SeedInitialDataAsync(context);

        var variant = await context.ProductVariants.FirstAsync();
        var branch = await context.Branches.FirstAsync();
        var shift = new CashierShift
        {
            Id = Guid.NewGuid(),
            BranchId = branch.Id,
            CounterNumber = "Counter-01",
            CashierUserId = Guid.NewGuid(),
            CashierName = "Cashier Test",
            Status = ShiftStatus.Open,
            StartingFloat = 1000m
        };
        await context.CashierShifts.AddAsync(shift);
        await context.SaveChangesAsync();

        var gstCalc = new GstCalculator();
        var billingService = new PosBillingService(context, gstCalc);

        // Process a sale with 3 items of HSN 6205
        var request = new CreateSaleRequest(
            branch.Id,
            shift.Id,
            null,
            new List<SaleItemRequest> { new(variant.Id, 3, variant.SellingPrice, 0, 0) },
            new List<PaymentRequest> { new(PaymentMethod.Cash, variant.SellingPrice * 3) }
        );
        var saleResult = await billingService.ProcessSaleAsync(request);
        Assert.True(saleResult.Success);

        var gstService = new GstReportService(context);
        var gstr1Result = await gstService.GenerateGstr1ReportAsync(DateTime.UtcNow.AddDays(-1), DateTime.UtcNow, branch.Id);

        Assert.True(gstr1Result.Success);
        Assert.NotNull(gstr1Result.Data);
        Assert.NotEmpty(gstr1Result.Data.HsnRows);
        Assert.True(gstr1Result.Data.TotalTaxableValue > 0);
        Assert.True(gstr1Result.Data.TotalTax > 0);
    }

    [Fact]
    public async Task Accounting_PostSaleJournal_ProducesBalancedVoucherAndTrialBalance()
    {
        await using var context = CreateInMemoryDbContext();
        await DatabaseSeeder.SeedInitialDataAsync(context);

        var variant = await context.ProductVariants.FirstAsync();
        var branch = await context.Branches.FirstAsync();
        var shift = new CashierShift
        {
            Id = Guid.NewGuid(),
            BranchId = branch.Id,
            CounterNumber = "Counter-01",
            CashierUserId = Guid.NewGuid(),
            CashierName = "Cashier Test",
            Status = ShiftStatus.Open,
            StartingFloat = 1000m
        };
        await context.CashierShifts.AddAsync(shift);
        await context.SaveChangesAsync();

        var gstCalc = new GstCalculator();
        var billingService = new PosBillingService(context, gstCalc);
        var accountingService = new AccountingService(context);

        // Process a sale
        var request = new CreateSaleRequest(
            branch.Id,
            shift.Id,
            null,
            new List<SaleItemRequest> { new(variant.Id, 2, variant.SellingPrice, 0, 0) },
            new List<PaymentRequest> { new(PaymentMethod.Cash, variant.SellingPrice * 2) }
        );
        var saleResult = await billingService.ProcessSaleAsync(request);
        Assert.True(saleResult.Success);

        var sale = await context.Sales.FindAsync(saleResult.Data!.SaleId);

        // Post Journal Voucher
        await accountingService.PostSaleJournalAsync(sale!);

        var voucher = await context.JournalVouchers
            .Include(v => v.Entries)
            .FirstOrDefaultAsync(v => v.ReferenceNumber == sale!.InvoiceNumber);

        Assert.NotNull(voucher);
        Assert.True(voucher.IsBalanced);
        Assert.Equal(voucher.TotalDebit, voucher.TotalCredit);

        // Check Trial Balance
        var trialBalanceResult = await accountingService.GetTrialBalanceAsync();
        Assert.True(trialBalanceResult.Success);
        Assert.True(trialBalanceResult.Data!.IsBalanced);
    }

    [Fact]
    public async Task Accounting_InterStateSale_PostsIgstAndMaintainsBalancedTrialBalance()
    {
        await using var context = CreateInMemoryDbContext();
        await DatabaseSeeder.SeedInitialDataAsync(context);

        var variant = await context.ProductVariants.FirstAsync();
        var branch = await context.Branches.FirstAsync();
        var shift = new CashierShift
        {
            Id = Guid.NewGuid(),
            BranchId = branch.Id,
            CounterNumber = "Counter-01",
            CashierUserId = Guid.NewGuid(),
            CashierName = "Cashier Test",
            Status = ShiftStatus.Open,
            StartingFloat = 1000m
        };
        await context.CashierShifts.AddAsync(shift);
        await context.SaveChangesAsync();

        var gstCalc = new GstCalculator();
        var billingService = new PosBillingService(context, gstCalc);
        var accountingService = new AccountingService(context);

        // Process an Inter-State sale (IsInterState = true)
        var request = new CreateSaleRequest(
            branch.Id,
            shift.Id,
            null,
            new List<SaleItemRequest> { new(variant.Id, 2, variant.SellingPrice, 0, 0) },
            new List<PaymentRequest> { new(PaymentMethod.UPI, variant.SellingPrice * 2) },
            InvoiceDiscountAmount: 0,
            IsInterState: true
        );
        var saleResult = await billingService.ProcessSaleAsync(request);
        Assert.True(saleResult.Success);

        var sale = await context.Sales.FindAsync(saleResult.Data!.SaleId);
        Assert.NotNull(sale);
        Assert.True(sale.IgstAmount > 0);
        Assert.Equal(0, sale.CgstAmount);
        Assert.Equal(0, sale.SgstAmount);

        var voucher = await context.JournalVouchers
            .Include(v => v.Entries)
            .FirstOrDefaultAsync(v => v.ReferenceNumber == sale.InvoiceNumber);

        Assert.NotNull(voucher);
        Assert.True(voucher.IsBalanced);
        Assert.Equal(voucher.TotalDebit, voucher.TotalCredit);

        // Verify IGST Voucher Entry exists
        Assert.Contains(voucher.Entries, e => e.CreditAmount == sale.IgstAmount);

        var tbResult = await accountingService.GetTrialBalanceAsync();
        Assert.True(tbResult.Success);
        Assert.True(tbResult.Data!.IsBalanced);
    }
}

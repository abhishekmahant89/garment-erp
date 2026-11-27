using GarmentERP.Application.DTOs;
using GarmentERP.Domain.Entities;
using GarmentERP.Domain.Services;
using GarmentERP.Infrastructure.Data;
using GarmentERP.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GarmentERP.Tests;

public class LoyaltyAndReturnTests
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
    public async Task Loyalty_AccrualAndRedemption_WorksCorrectly()
    {
        await using var context = CreateInMemoryDbContext();
        await DatabaseSeeder.SeedInitialDataAsync(context);

        var customer = await context.Customers.FirstAsync();
        var loyaltyService = new LoyaltyService(context);

        // 1. Accrue points on spend of Rs. 3,550 -> 35 points earned
        var accrueResult = await loyaltyService.AccruePointsAsync(customer.Id, null, 3550m);
        Assert.True(accrueResult.Success);
        Assert.Equal(35, accrueResult.Data!.PointsEarned);
        Assert.Equal(35, accrueResult.Data.NewTotalPoints);

        // 2. Try redeeming more than allowed by 25% cap on a Rs. 100 bill (max cap = 25 points)
        var cappedResult = await loyaltyService.RedeemPointsAsync(customer.Id, 30, 100m);
        Assert.False(cappedResult.Success); // Capped at 25 points

        // 3. Redeem valid points (20 points on Rs. 1000 bill)
        var redeemResult = await loyaltyService.RedeemPointsAsync(customer.Id, 20, 1000m);
        Assert.True(redeemResult.Success);
        Assert.Equal(20m, redeemResult.Data);

        var updatedCustomer = await context.Customers.FindAsync(customer.Id);
        Assert.Equal(15, updatedCustomer!.LoyaltyPoints); // 35 - 20 = 15
    }

    [Fact]
    public async Task ReturnExchange_RestocksInventory_AndIssuesCreditNote()
    {
        await using var context = CreateInMemoryDbContext();
        await DatabaseSeeder.SeedInitialDataAsync(context);

        var variant = await context.ProductVariants.FirstAsync();
        var branch = await context.Branches.FirstAsync();
        var customer = await context.Customers.FirstAsync();

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
        var returnService = new ReturnExchangeService(context, billingService);

        // 1. Process initial sale of 2 items
        var saleRequest = new CreateSaleRequest(
            branch.Id,
            shift.Id,
            customer.Id,
            new List<SaleItemRequest> { new(variant.Id, 2, variant.SellingPrice, 0, 0) },
            new List<PaymentRequest> { new(PaymentMethod.Cash, variant.SellingPrice * 2) }
        );

        var saleResult = await billingService.ProcessSaleAsync(saleRequest);
        Assert.True(saleResult.Success);
        var originalSaleId = saleResult.Data!.SaleId;

        var stockBeforeReturn = await context.Stocks.Where(s => s.VariantId == variant.Id).SumAsync(s => s.Quantity);

        // 2. Return 1 item (pure return)
        var returnRequest = new ProcessReturnExchangeRequest(
            OriginalSaleId: originalSaleId,
            BranchId: branch.Id,
            CashierShiftId: shift.Id,
            ReturnItems: new List<ReturnItemRequest>
            {
                new(variant.Id, 1, variant.SellingPrice, "Wrong fit")
            }
        );

        var returnResult = await returnService.ProcessReturnExchangeAsync(returnRequest);
        Assert.True(returnResult.Success);
        Assert.NotNull(returnResult.Data!.CreditNoteNumber);
        Assert.StartsWith("CN-", returnResult.Data.CreditNoteNumber);

        // Verify 1 item was added back to stock
        var stockAfterReturn = await context.Stocks.Where(s => s.VariantId == variant.Id).SumAsync(s => s.Quantity);
        Assert.Equal(stockBeforeReturn + 1, stockAfterReturn);

        // Verify Credit note was created and is active
        var creditNote = await context.CreditNotes.FirstOrDefaultAsync(c => c.NoteNumber == returnResult.Data.CreditNoteNumber);
        Assert.NotNull(creditNote);
        Assert.Equal(variant.SellingPrice, creditNote.Amount);
        Assert.False(creditNote.IsRedeemed);
    }
}

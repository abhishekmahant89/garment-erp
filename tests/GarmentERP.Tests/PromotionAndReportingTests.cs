using GarmentERP.Domain.Entities;
using GarmentERP.Infrastructure.Data;
using GarmentERP.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GarmentERP.Tests;

public class PromotionAndReportingTests
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
    public void PromotionEngine_EvaluatesDifferentPromotionsAccurately()
    {
        using var context = CreateInMemoryDbContext();
        var engine = new PromotionEngine(context);

        // 1. Flat Percentage Promotion (10% off above Rs. 2000)
        var promoPct = new Promotion
        {
            Name = "Festival 10%",
            Code = "FEST10",
            Type = PromotionType.FlatPercentage,
            DiscountValue = 10m,
            MinimumBillValue = 2000m,
            StartDate = DateTime.UtcNow.AddDays(-1),
            EndDate = DateTime.UtcNow.AddDays(5),
            IsActive = true
        };

        var resultPct = engine.EvaluatePromotion(promoPct, billSubTotal: 3000m, totalGarments: 2);
        Assert.True(resultPct.IsApplicable);
        Assert.Equal(300m, resultPct.TotalDiscountAmount);

        // 2. Buy 2 Get 1 Free (BuyQuantity: 2, FreeQuantity: 1)
        var promoBogo = new Promotion
        {
            Name = "Buy 2 Get 1 Free",
            Code = "B2G1",
            Type = PromotionType.BuyXGetYFree,
            BuyQuantity = 2,
            FreeQuantity = 1,
            StartDate = DateTime.UtcNow.AddDays(-1),
            EndDate = DateTime.UtcNow.AddDays(5),
            IsActive = true
        };

        // Buying 3 garments with subtotal 3000 (avg piece = 1000) -> 1 piece free = Rs. 1000 discount
        var resultBogo = engine.EvaluatePromotion(promoBogo, billSubTotal: 3000m, totalGarments: 3);
        Assert.True(resultBogo.IsApplicable);
        Assert.Equal(1000m, resultBogo.TotalDiscountAmount);
    }

    [Fact]
    public async Task EnterpriseReporting_ComputesStockValuationAndProfitLoss()
    {
        await using var context = CreateInMemoryDbContext();
        await DatabaseSeeder.SeedInitialDataAsync(context);

        var reportService = new EnterpriseReportService(context);

        // 1. Stock Valuation
        var valuationResult = await reportService.GetStockValuationReportAsync();
        Assert.True(valuationResult.Success);
        Assert.True(valuationResult.Data!.TotalUniqueSkus >= 25);
        Assert.True(valuationResult.Data.TotalQuantityOnHand > 0);
        Assert.True(valuationResult.Data.TotalCostValuation > 0);
        Assert.True(valuationResult.Data.TotalRetailValuation > valuationResult.Data.TotalCostValuation);
        Assert.True(valuationResult.Data.PotentialGrossProfitMargin > 0);

        // 2. P&L Report for date range
        var pnlResult = await reportService.GetProfitAndLossReportAsync(DateTime.UtcNow.AddDays(-7), DateTime.UtcNow);
        Assert.True(pnlResult.Success);
        Assert.NotNull(pnlResult.Data);
    }
}

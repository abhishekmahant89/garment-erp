using GarmentERP.Application.DTOs;
using GarmentERP.Domain.Entities;
using GarmentERP.Domain.Services;
using GarmentERP.Infrastructure.Data;
using GarmentERP.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GarmentERP.Tests;

public class PosBillingTests
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
    public async Task ProcessSale_CreatesSale_ReducesStock_AndQueuesSyncOutboxAsPending()
    {
        // Arrange
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
            StartingFloat = 1000m,
            ExpectedCash = 1000m
        };
        await context.CashierShifts.AddAsync(shift);
        await context.SaveChangesAsync();

        var initialStock = await context.Stocks.Where(s => s.VariantId == variant.Id).SumAsync(s => s.Quantity);

        var gstCalc = new GstCalculator();
        var billingService = new PosBillingService(context, gstCalc);

        var request = new CreateSaleRequest(
            branch.Id,
            shift.Id,
            null,
            new List<SaleItemRequest>
            {
                new(variant.Id, 2, variant.SellingPrice, 0, 0)
            },
            new List<PaymentRequest>
            {
                new(PaymentMethod.Cash, variant.SellingPrice * 2)
            }
        );

        // Act
        var result = await billingService.ProcessSaleAsync(request);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.StartsWith("INV-", result.Data.InvoiceNumber);

        // Verify stock deducted by 2
        var updatedStock = await context.Stocks.Where(s => s.VariantId == variant.Id).SumAsync(s => s.Quantity);
        Assert.Equal(initialStock - 2, updatedStock);

        // Verify SyncOutbox queued with status PENDING (POSRES DataSync requirement)
        var outbox = await context.SyncOutbox
            .FirstOrDefaultAsync(o => o.AggregateId == result.Data.SaleId);

        Assert.NotNull(outbox);
        Assert.Equal("Sale", outbox.AggregateType);
        Assert.Equal(SyncStatus.Pending, outbox.Status);
    }
}

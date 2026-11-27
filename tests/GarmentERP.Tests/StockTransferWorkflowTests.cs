using GarmentERP.Application.DTOs;
using GarmentERP.Domain.Entities;
using GarmentERP.Infrastructure.Data;
using GarmentERP.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GarmentERP.Tests;

public class StockTransferWorkflowTests
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
    public async Task TransferWorkflow_DispatchesDeductsSource_AndReceivesAtDestination()
    {
        await using var context = CreateInMemoryDbContext();
        await DatabaseSeeder.SeedInitialDataAsync(context);

        var variant = await context.ProductVariants.FirstAsync();
        var branch = await context.Branches.FirstAsync();
        var sourceWh = await context.Warehouses.FirstAsync();

        // Create Destination Warehouse (e.g. Warehouse 02 / Backstore)
        var destWh = new Warehouse
        {
            Id = Guid.NewGuid(),
            BranchId = branch.Id,
            Code = "WH02",
            Name = "Backstore Godown",
            LocationType = "BackStore"
        };
        await context.Warehouses.AddAsync(destWh);
        await context.SaveChangesAsync();

        var initialSourceStock = await context.Stocks.Where(s => s.VariantId == variant.Id && s.WarehouseId == sourceWh.Id).SumAsync(s => s.Quantity);

        var transferService = new StockTransferService(context);

        // 1. Dispatch 5 items from Source to Destination
        var dispatchRequest = new CreateStockTransferRequest(
            SourceWarehouseId: sourceWh.Id,
            DestinationWarehouseId: destWh.Id,
            Items: new List<StockTransferItemInputDto>
            {
                new(variant.Id, 5)
            },
            DispatchedBy: "Manager Ramesh"
        );

        var dispatchResult = await transferService.CreateTransferDispatchAsync(dispatchRequest);
        Assert.True(dispatchResult.Success);
        Assert.Equal(TransferStatus.InTransit, dispatchResult.Data!.Status);

        // Verify source stock deducted by 5
        var sourceStockAfterDispatch = await context.Stocks.Where(s => s.VariantId == variant.Id && s.WarehouseId == sourceWh.Id).SumAsync(s => s.Quantity);
        Assert.Equal(initialSourceStock - 5, sourceStockAfterDispatch);

        // 2. Destination confirms Receipt
        var receiveResult = await transferService.ConfirmTransferReceiveAsync(dispatchResult.Data.Id, "Storekeeper Suresh");
        Assert.True(receiveResult.Success);
        Assert.Equal(TransferStatus.Received, receiveResult.Data!.Status);

        // Verify destination stock received 5 items
        var destStock = await context.Stocks.Where(s => s.VariantId == variant.Id && s.WarehouseId == destWh.Id).SumAsync(s => s.Quantity);
        Assert.Equal(5, destStock);
    }
}

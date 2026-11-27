using GarmentERP.Application.DTOs;
using GarmentERP.Domain.Entities;
using GarmentERP.Infrastructure.Data;
using GarmentERP.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GarmentERP.Tests;

public class PurchasesAndInventoryIntakeTests
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
    public async Task ProcessPurchaseInvoice_ReceivesStock_UpdatesPricesAndSupplierBalance()
    {
        await using var context = CreateInMemoryDbContext();
        await DatabaseSeeder.SeedInitialDataAsync(context);

        var variant = await context.ProductVariants.FirstAsync();
        var branch = await context.Branches.FirstAsync();
        var warehouse = await context.Warehouses.FirstAsync();

        var purchaseService = new PurchaseService(context);

        // 1. Create a Supplier
        var supplierResult = await purchaseService.CreateSupplierAsync(new CreateSupplierDto(
            Name: "Raymond Luxury Textiles",
            Code: "RAYMOND",
            ContactPerson: "Sunil Jain",
            Phone: "9822011223",
            Email: "sunil@raymond.in",
            Address: "Thane Industrial Estate",
            City: "Mumbai",
            State: "Maharashtra",
            Gstin: "27AAAAA1234A1Z1"
        ));
        Assert.True(supplierResult.Success);
        var supplierId = supplierResult.Data!.Id;

        var initialStock = await context.Stocks.Where(s => s.VariantId == variant.Id && s.WarehouseId == warehouse.Id).SumAsync(s => s.Quantity);

        // 2. Process Purchase Invoice for 50 pieces @ Rs. 700 each + 5% tax
        var request = new CreatePurchaseInvoiceRequest(
            SupplierInvoiceNumber: "BILL-2026-9901",
            SupplierId: supplierId,
            BranchId: branch.Id,
            WarehouseId: warehouse.Id,
            InvoiceDate: DateTime.UtcNow,
            Items: new List<PurchaseItemInputDto>
            {
                new(variant.Id, 50, 700m, 1799m, 1499m, 5.0m)
            },
            PaidAmount: 15000m // Partial payment
        );

        var invoiceResult = await purchaseService.ProcessPurchaseInvoiceAsync(request);
        Assert.True(invoiceResult.Success);
        Assert.NotNull(invoiceResult.Data);

        // Verify Gross: 50 * 700 = 35000, Tax: 1750, Total: 36750, Paid: 15000, Balance: 21750
        Assert.Equal(35000m, invoiceResult.Data.SubTotal);
        Assert.Equal(1750m, invoiceResult.Data.TaxAmount);
        Assert.Equal(36750m, invoiceResult.Data.TotalAmount);
        Assert.Equal(21750m, invoiceResult.Data.BalanceAmount);

        // Verify stock incremented by 50
        var newStock = await context.Stocks.Where(s => s.VariantId == variant.Id && s.WarehouseId == warehouse.Id).SumAsync(s => s.Quantity);
        Assert.Equal(initialStock + 50, newStock);

        // Verify variant cost price updated to 700 and MRP to 1799
        var updatedVariant = await context.ProductVariants.FindAsync(variant.Id);
        Assert.Equal(700m, updatedVariant!.CostPrice);
        Assert.Equal(1799m, updatedVariant.MRP);

        // Verify supplier outstanding balance updated to 21750
        var updatedSupplier = await context.Suppliers.FindAsync(supplierId);
        Assert.Equal(21750m, updatedSupplier!.CurrentBalance);
    }
}

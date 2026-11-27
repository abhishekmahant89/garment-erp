using GarmentERP.Domain.Entities;
using GarmentERP.Domain.Services;
using Microsoft.EntityFrameworkCore;

namespace GarmentERP.Infrastructure.Data;

public static class DatabaseSeeder
{
    public static async Task SeedInitialDataAsync(GarmentDbContextBase context)
    {
        await context.Database.EnsureCreatedAsync();

        if (await context.Categories.AnyAsync())
        {
            return; // Already seeded
        }

        // 1. Categories
        var catMens = new Category { Id = Guid.NewGuid(), Name = "Men's Clothing", Code = "MENS", Description = "Apparel for Men" };
        var catWomens = new Category { Id = Guid.NewGuid(), Name = "Women's Clothing", Code = "WOMENS", Description = "Apparel for Women" };
        var catKids = new Category { Id = Guid.NewGuid(), Name = "Kids' Clothing", Code = "KIDS", Description = "Clothing for Children & Infants" };
        var catFootwear = new Category { Id = Guid.NewGuid(), Name = "Footwear & Accessories", Code = "FOOTWEAR", Description = "Shoes, Belts, Wallets" };
        await context.Categories.AddRangeAsync(catMens, catWomens, catKids, catFootwear);

        // 2. Brands
        var brandLevis = new Brand { Id = Guid.NewGuid(), Name = "UrbanDenim", Code = "UD", Description = "Premium Casual & Denim Wear" };
        var brandZara = new Brand { Id = Guid.NewGuid(), Name = "VogueAura", Code = "VA", Description = "Contemporary Fashion" };
        await context.Brands.AddRangeAsync(brandLevis, brandZara);

        // 3. Sizes (Standard Garment Sizes + Inseam/Waist)
        var sizeS = new Size { Id = Guid.NewGuid(), Code = "S", Name = "Small (38)", DisplayOrder = 1 };
        var sizeM = new Size { Id = Guid.NewGuid(), Code = "M", Name = "Medium (40)", DisplayOrder = 2 };
        var sizeL = new Size { Id = Guid.NewGuid(), Code = "L", Name = "Large (42)", DisplayOrder = 3 };
        var sizeXL = new Size { Id = Guid.NewGuid(), Code = "XL", Name = "Extra Large (44)", DisplayOrder = 4 };
        var sizeXXL = new Size { Id = Guid.NewGuid(), Code = "XXL", Name = "Double Extra Large (46)", DisplayOrder = 5 };
        await context.Sizes.AddRangeAsync(sizeS, sizeM, sizeL, sizeXL, sizeXXL);

        // 4. Colors
        var colBlack = new Color { Id = Guid.NewGuid(), Code = "BLK", Name = "Jet Black", HexValue = "#111111" };
        var colWhite = new Color { Id = Guid.NewGuid(), Code = "WHT", Name = "Crisp White", HexValue = "#FFFFFF" };
        var colBlue = new Color { Id = Guid.NewGuid(), Code = "BLU", Name = "Navy Blue", HexValue = "#001F3F" };
        var colRed = new Color { Id = Guid.NewGuid(), Code = "RED", Name = "Ruby Red", HexValue = "#B22222" };
        var colOlive = new Color { Id = Guid.NewGuid(), Code = "OLV", Name = "Military Olive", HexValue = "#556B2F" };
        await context.Colors.AddRangeAsync(colBlack, colWhite, colBlue, colRed, colOlive);

        // 5. HSN Codes with Indian Garment GST Rules
        var hsnShirt = new HsnCode
        {
            Id = Guid.NewGuid(),
            Code = "6205",
            Description = "Men's or Boys' Shirts (Woven)",
            GstRate = 5.0m,
            ThresholdAmount = 1000m,
            HighGstRate = 12.0m
        };
        var hsnTshirt = new HsnCode
        {
            Id = Guid.NewGuid(),
            Code = "6109",
            Description = "T-shirts, singlets and other vests, knitted or crocheted",
            GstRate = 5.0m,
            ThresholdAmount = 1000m,
            HighGstRate = 12.0m
        };
        await context.HsnCodes.AddRangeAsync(hsnShirt, hsnTshirt);

        // 6. Default Branch & Warehouse
        var defaultBranch = new Branch
        {
            Id = Guid.NewGuid(),
            Code = "BR01",
            Name = "Grand Fashion Flagship Store",
            Address = "MG Road, Central Commercial Complex",
            City = "Bangalore",
            State = "Karnataka",
            Pincode = "560001",
            Phone = "+91 98765 43210",
            Email = "store01@garmenterp.com",
            Gstin = "29AAAAA0000A1Z5",
            IsHeadOffice = true
        };
        await context.Branches.AddAsync(defaultBranch);

        var defaultWarehouse = new Warehouse
        {
            Id = Guid.NewGuid(),
            BranchId = defaultBranch.Id,
            Code = "WH01",
            Name = "Main Showroom Floor",
            LocationType = "StoreFloor"
        };
        await context.Warehouses.AddAsync(defaultWarehouse);

        // 7. Default Customer (Walk-in)
        var walkinCustomer = new Customer
        {
            Id = Guid.NewGuid(),
            Name = "Walk-in Retail Customer",
            Phone = "9999999999",
            Email = "walkin@store.local"
        };
        await context.Customers.AddAsync(walkinCustomer);

        // 8. Demo Product with 25 Variants (5 sizes x 5 colors) as requested
        var demoProduct = new Product
        {
            Id = Guid.NewGuid(),
            StyleCode = "SHIRT-SLM-2026",
            Name = "Men's Luxury Cotton Casual Shirt",
            Description = "100% Giza Cotton Slim Fit Casual Shirt with Spread Collar",
            CategoryId = catMens.Id,
            BrandId = brandLevis.Id,
            HsnCodeId = hsnShirt.Id,
            Gender = "Men",
            Fabric = "100% Cotton",
            Season = "Spring/Summer",
            HasVariants = true
        };
        await context.Products.AddAsync(demoProduct);

        var matrixGenerator = new VariantMatrixGenerator();
        var allSizes = new[] { sizeS, sizeM, sizeL, sizeXL, sizeXXL };
        var allColors = new[] { colBlack, colWhite, colBlue, colRed, colOlive };

        var variants = matrixGenerator.GenerateMatrix(
            demoProduct,
            allSizes,
            allColors,
            baseCostPrice: 650m,
            baseSellingPrice: 1299m,
            mrp: 1499m,
            fit: "Slim Fit"
        );

        await context.ProductVariants.AddRangeAsync(variants);

        // Seed stock for each variant (e.g. 20 pieces each)
        foreach (var variant in variants)
        {
            var stock = new Stock
            {
                Id = Guid.NewGuid(),
                VariantId = variant.Id,
                WarehouseId = defaultWarehouse.Id,
                BranchId = defaultBranch.Id,
                Quantity = 20,
                ReservedQuantity = 0
            };
            await context.Stocks.AddAsync(stock);
        }

        // 9. Initial License Key Info
        var license = new LicenseInfo
        {
            Id = Guid.NewGuid(),
            LicenseKey = "DEMO-GARMENT-2026-ACTIVE",
            RegisteredBusinessName = "Grand Fashion Flagship Store",
            MachineFingerprint = "DEV-MACHINE-FINGERPRINT",
            AllowedTerminals = 5,
            ExpiryDate = DateTime.UtcNow.AddYears(1),
            GracePeriodDays = 15,
            IsActive = true
        };
        await context.LicenseInfos.AddAsync(license);

        // 10. Default Chart of Accounts for Retail Garment Accounting
        var accCash = new LedgerAccount { Id = Guid.NewGuid(), AccountCode = "1010", AccountName = "Cash in Hand", Type = AccountType.Asset };
        var accUpi = new LedgerAccount { Id = Guid.NewGuid(), AccountCode = "1020", AccountName = "UPI & Bank Clearing", Type = AccountType.Asset };
        var accCard = new LedgerAccount { Id = Guid.NewGuid(), AccountCode = "1030", AccountName = "Card Clearing Account", Type = AccountType.Asset };
        var accInventory = new LedgerAccount { Id = Guid.NewGuid(), AccountCode = "1050", AccountName = "Merchandise Inventory Asset", Type = AccountType.Asset };
        var accCgst = new LedgerAccount { Id = Guid.NewGuid(), AccountCode = "2020", AccountName = "Output CGST Payable", Type = AccountType.Liability };
        var accSgst = new LedgerAccount { Id = Guid.NewGuid(), AccountCode = "2030", AccountName = "Output SGST Payable", Type = AccountType.Liability };
        var accIgst = new LedgerAccount { Id = Guid.NewGuid(), AccountCode = "2040", AccountName = "Output IGST Payable", Type = AccountType.Liability };
        var accSales = new LedgerAccount { Id = Guid.NewGuid(), AccountCode = "4010", AccountName = "Apparel Retail Sales Revenue", Type = AccountType.Revenue };
        var accCogs = new LedgerAccount { Id = Guid.NewGuid(), AccountCode = "5010", AccountName = "Cost of Goods Sold (COGS)", Type = AccountType.Expense };
        var accExpenses = new LedgerAccount { Id = Guid.NewGuid(), AccountCode = "6010", AccountName = "Store Operating Expenses", Type = AccountType.Expense };
        await context.LedgerAccounts.AddRangeAsync(accCash, accUpi, accCard, accInventory, accCgst, accSgst, accIgst, accSales, accCogs, accExpenses);

        // 11. Default Staff Users with Salted Hash
        var adminUser = new StaffUser
        {
            Id = Guid.NewGuid(),
            Username = "admin",
            FullName = "Store Administrator",
            Role = UserRole.SuperAdmin,
            Email = "admin@garmenterp.com",
            Phone = "9988776655",
            BranchId = defaultBranch.Id,
            PasswordHash = "admin123_hash",
            PasswordSalt = "salt"
        };

        var cashierUser = new StaffUser
        {
            Id = Guid.NewGuid(),
            Username = "cashier1",
            FullName = "Rahul Verma",
            Role = UserRole.Cashier,
            Email = "rahul@garmenterp.com",
            Phone = "9988776644",
            BranchId = defaultBranch.Id,
            PasswordHash = "cashier123_hash",
            PasswordSalt = "salt"
        };
        await context.StaffUsers.AddRangeAsync(adminUser, cashierUser);

        await context.SaveChangesAsync();
    }
}

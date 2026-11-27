using GarmentERP.Application.DTOs;
using GarmentERP.Application.Interfaces;
using GarmentERP.Domain.Entities;
using GarmentERP.Domain.Services;
using GarmentERP.Infrastructure.Data;
using GarmentERP.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace GarmentERP.Infrastructure.Services;

public class ProductCatalogService : IProductCatalogService
{
    private readonly GarmentDbContextBase _context;
    private readonly IVariantMatrixGenerator _matrixGenerator;

    public ProductCatalogService(GarmentDbContextBase context, IVariantMatrixGenerator matrixGenerator)
    {
        _context = context;
        _matrixGenerator = matrixGenerator;
    }

    public async Task<ApiResponse<ProductDto>> CreateProductAsync(CreateProductDto dto, CancellationToken ct = default)
    {
        if (await _context.Products.AnyAsync(p => p.StyleCode == dto.StyleCode, ct))
        {
            return ApiResponse<ProductDto>.Fail($"A product with style code '{dto.StyleCode}' already exists.");
        }

        var product = new Product
        {
            Id = Guid.NewGuid(),
            StyleCode = dto.StyleCode.Trim().ToUpperInvariant(),
            Name = dto.Name.Trim(),
            Description = dto.Description,
            CategoryId = dto.CategoryId,
            BrandId = dto.BrandId,
            HsnCodeId = dto.HsnCodeId,
            Gender = dto.Gender,
            Fabric = dto.Fabric,
            Season = dto.Season,
            HasVariants = true
        };

        await _context.Products.AddAsync(product, ct);
        await _context.SaveChangesAsync(ct);

        var category = await _context.Categories.FindAsync(new object[] { product.CategoryId }, ct);
        var brand = await _context.Brands.FindAsync(new object[] { product.BrandId }, ct);
        var hsn = await _context.HsnCodes.FindAsync(new object[] { product.HsnCodeId }, ct);

        var result = new ProductDto(
            product.Id,
            product.StyleCode,
            product.Name,
            product.Description,
            product.CategoryId,
            category?.Name ?? string.Empty,
            product.BrandId,
            brand?.Name ?? string.Empty,
            product.HsnCodeId,
            hsn?.Code ?? string.Empty,
            hsn?.GstRate ?? 0,
            product.Gender,
            product.Fabric,
            product.Season,
            product.HasVariants,
            new List<ProductVariantDto>()
        );

        return ApiResponse<ProductDto>.Ok(result, "Product created successfully");
    }

    public async Task<ApiResponse<List<ProductVariantDto>>> GenerateMatrixVariantsAsync(CreateVariantMatrixRequest request, CancellationToken ct = default)
    {
        var product = await _context.Products
            .Include(p => p.Category)
            .Include(p => p.Brand)
            .FirstOrDefaultAsync(p => p.Id == request.ProductId, ct);

        if (product == null)
        {
            return ApiResponse<List<ProductVariantDto>>.Fail("Product not found.");
        }

        var sizes = await _context.Sizes
            .Where(s => request.SizeIds.Contains(s.Id))
            .ToListAsync(ct);

        var colors = await _context.Colors
            .Where(c => request.ColorIds.Contains(c.Id))
            .ToListAsync(ct);

        if (!sizes.Any() || !colors.Any())
        {
            return ApiResponse<List<ProductVariantDto>>.Fail("Must select at least one Size and one Color to generate matrix variants.");
        }

        var variants = _matrixGenerator.GenerateMatrix(
            product,
            sizes,
            colors,
            request.CostPrice,
            request.SellingPrice,
            request.MRP,
            request.Fit
        );

        // Filter out any existing duplicates
        var existingSkus = await _context.ProductVariants
            .Where(v => v.ProductId == product.Id)
            .Select(v => v.SKU)
            .ToHashSetAsync(ct);

        var newVariants = variants.Where(v => !existingSkus.Contains(v.SKU)).ToList();

        if (newVariants.Any())
        {
            await _context.ProductVariants.AddRangeAsync(newVariants, ct);

            // Also initialize default stock record for default warehouse
            var defaultWarehouse = await _context.Warehouses.FirstOrDefaultAsync(ct);
            var defaultBranch = await _context.Branches.FirstOrDefaultAsync(ct);

            if (defaultWarehouse != null && defaultBranch != null)
            {
                foreach (var variant in newVariants)
                {
                    await _context.Stocks.AddAsync(new Stock
                    {
                        Id = Guid.NewGuid(),
                        VariantId = variant.Id,
                        WarehouseId = defaultWarehouse.Id,
                        BranchId = defaultBranch.Id,
                        Quantity = 0,
                        ReservedQuantity = 0
                    }, ct);
                }
            }

            // Record Outbox event for variants creation
            await _context.SyncOutbox.AddAsync(new SyncOutbox
            {
                Id = Guid.NewGuid(),
                AggregateType = "ProductVariantMatrix",
                AggregateId = product.Id,
                EventType = "Created",
                PayloadJson = System.Text.Json.JsonSerializer.Serialize(newVariants.Select(v => new { v.Id, v.SKU, v.Barcode, v.SellingPrice, v.MRP })),
                Status = SyncStatus.Pending
            }, ct);

            await _context.SaveChangesAsync(ct);
        }

        var resultDtos = newVariants.Select(v => new ProductVariantDto(
            v.Id,
            v.ProductId,
            v.SKU,
            v.Barcode,
            v.SizeId,
            v.Size?.Code ?? string.Empty,
            v.Size?.Name ?? string.Empty,
            v.ColorId,
            v.Color?.Code ?? string.Empty,
            v.Color?.Name ?? string.Empty,
            v.Fit,
            v.CostPrice,
            v.SellingPrice,
            v.MRP,
            v.ReorderLevel,
            0
        )).ToList();

        return ApiResponse<List<ProductVariantDto>>.Ok(resultDtos, $"Generated {resultDtos.Count} variants successfully.");
    }

    public async Task<ApiResponse<ProductVariantDto?>> FindVariantByBarcodeAsync(string barcodeOrSku, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(barcodeOrSku))
            return ApiResponse<ProductVariantDto?>.Fail("Barcode/SKU cannot be empty.");

        var term = barcodeOrSku.Trim();
        var variant = await _context.ProductVariants
            .Include(v => v.Product)
            .Include(v => v.Size)
            .Include(v => v.Color)
            .Include(v => v.Stocks)
            .FirstOrDefaultAsync(v => v.Barcode == term || v.SKU == term, ct);

        if (variant == null)
            return ApiResponse<ProductVariantDto?>.Ok(null, "No variant found matching barcode or SKU.");

        var totalStock = variant.Stocks.Sum(s => s.AvailableQuantity);

        var dto = new ProductVariantDto(
            variant.Id,
            variant.ProductId,
            variant.SKU,
            variant.Barcode,
            variant.SizeId,
            variant.Size?.Code ?? string.Empty,
            variant.Size?.Name ?? string.Empty,
            variant.ColorId,
            variant.Color?.Code ?? string.Empty,
            variant.Color?.Name ?? string.Empty,
            variant.Fit,
            variant.CostPrice,
            variant.SellingPrice,
            variant.MRP,
            variant.ReorderLevel,
            totalStock
        );

        return ApiResponse<ProductVariantDto?>.Ok(dto, "Variant found.");
    }

    public async Task<ApiResponse<List<ProductVariantDto>>> SearchVariantsAsync(string searchTerm, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
            return ApiResponse<List<ProductVariantDto>>.Ok(new List<ProductVariantDto>());

        var term = searchTerm.Trim().ToLowerInvariant();

        var query = _context.ProductVariants
            .Include(v => v.Product)
            .Include(v => v.Size)
            .Include(v => v.Color)
            .Include(v => v.Stocks)
            .Where(v => v.Barcode.ToLower().Contains(term) ||
                        v.SKU.ToLower().Contains(term) ||
                        v.Product!.Name.ToLower().Contains(term) ||
                        v.Product.StyleCode.ToLower().Contains(term) ||
                        v.Color!.Name.ToLower().Contains(term) ||
                        v.Size!.Name.ToLower().Contains(term))
            .Take(50);

        var list = await query.ToListAsync(ct);

        var dtos = list.Select(v => new ProductVariantDto(
            v.Id,
            v.ProductId,
            v.SKU,
            v.Barcode,
            v.SizeId,
            v.Size?.Code ?? string.Empty,
            v.Size?.Name ?? string.Empty,
            v.ColorId,
            v.Color?.Code ?? string.Empty,
            v.Color?.Name ?? string.Empty,
            v.Fit,
            v.CostPrice,
            v.SellingPrice,
            v.MRP,
            v.ReorderLevel,
            v.Stocks.Sum(s => s.AvailableQuantity)
        )).ToList();

        return ApiResponse<List<ProductVariantDto>>.Ok(dtos);
    }

    public async Task<ApiResponse<List<CategoryDto>>> GetCategoriesAsync(CancellationToken ct = default)
    {
        var list = await _context.Categories
            .Select(c => new CategoryDto(c.Id, c.Name, c.Code, c.Description))
            .ToListAsync(ct);
        return ApiResponse<List<CategoryDto>>.Ok(list);
    }

    public async Task<ApiResponse<List<SizeDto>>> GetSizesAsync(CancellationToken ct = default)
    {
        var list = await _context.Sizes
            .OrderBy(s => s.DisplayOrder)
            .Select(s => new SizeDto(s.Id, s.Code, s.Name, s.CategoryType, s.DisplayOrder))
            .ToListAsync(ct);
        return ApiResponse<List<SizeDto>>.Ok(list);
    }

    public async Task<ApiResponse<List<ColorDto>>> GetColorsAsync(CancellationToken ct = default)
    {
        var list = await _context.Colors
            .OrderBy(c => c.Name)
            .Select(c => new ColorDto(c.Id, c.Code, c.Name, c.HexValue))
            .ToListAsync(ct);
        return ApiResponse<List<ColorDto>>.Ok(list);
    }
}

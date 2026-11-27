namespace GarmentERP.Application.DTOs;

public record CategoryDto(Guid Id, string Name, string Code, string? Description);
public record BrandDto(Guid Id, string Name, string Code, string? Description);
public record SizeDto(Guid Id, string Code, string Name, string CategoryType, int DisplayOrder);
public record ColorDto(Guid Id, string Code, string Name, string HexValue);
public record HsnCodeDto(Guid Id, string Code, string Description, decimal GstRate, decimal? ThresholdAmount, decimal? HighGstRate);

public record ProductVariantDto(
    Guid Id,
    Guid ProductId,
    string SKU,
    string Barcode,
    Guid SizeId,
    string SizeCode,
    string SizeName,
    Guid ColorId,
    string ColorCode,
    string ColorName,
    string? Fit,
    decimal CostPrice,
    decimal SellingPrice,
    decimal MRP,
    int ReorderLevel,
    decimal CurrentStock
);

public record ProductDto(
    Guid Id,
    string StyleCode,
    string Name,
    string? Description,
    Guid CategoryId,
    string CategoryName,
    Guid BrandId,
    string BrandName,
    Guid HsnCodeId,
    string HsnCode,
    decimal GstRate,
    string Gender,
    string Fabric,
    string Season,
    bool HasVariants,
    IReadOnlyList<ProductVariantDto> Variants
);

public record CreateProductDto(
    string StyleCode,
    string Name,
    string? Description,
    Guid CategoryId,
    Guid BrandId,
    Guid HsnCodeId,
    string Gender,
    string Fabric,
    string Season
);

public record CreateVariantMatrixRequest(
    Guid ProductId,
    List<Guid> SizeIds,
    List<Guid> ColorIds,
    decimal CostPrice,
    decimal SellingPrice,
    decimal MRP,
    string Fit = "Regular"
);

using GarmentERP.Domain.Common;

namespace GarmentERP.Domain.Entities;

public class Product : BaseEntity
{
    public string StyleCode { get; set; } = string.Empty; // e.g. SHIRT-SLM-2026
    public string Name { get; set; } = string.Empty; // Men's Casual Slim Fit Shirt
    public string? Description { get; set; }
    public Guid CategoryId { get; set; }
    public Category? Category { get; set; }
    public Guid BrandId { get; set; }
    public Brand? Brand { get; set; }
    public Guid HsnCodeId { get; set; }
    public HsnCode? HsnCode { get; set; }
    public string Gender { get; set; } = "Men"; // Men, Women, Kids, Unisex
    public string Fabric { get; set; } = "Cotton";
    public string Season { get; set; } = "All Season";
    public bool HasVariants { get; set; } = true;

    public ICollection<ProductVariant> Variants { get; set; } = new List<ProductVariant>();
}

public class ProductVariant : BaseEntity
{
    public Guid ProductId { get; set; }
    public Product? Product { get; set; }

    public string SKU { get; set; } = string.Empty; // e.g. SHIRT-SLM-BLK-M
    public string Barcode { get; set; } = string.Empty; // Unique EAN / Code-128
    
    public Guid SizeId { get; set; }
    public Size? Size { get; set; }

    public Guid ColorId { get; set; }
    public Color? Color { get; set; }

    public string? Fit { get; set; } // Slim, Regular, Relaxed
    public decimal CostPrice { get; set; }
    public decimal SellingPrice { get; set; }
    public decimal MRP { get; set; }
    public int ReorderLevel { get; set; } = 5;

    public ICollection<Stock> Stocks { get; set; } = new List<Stock>();
}

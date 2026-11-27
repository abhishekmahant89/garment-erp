using GarmentERP.Domain.Common;

namespace GarmentERP.Domain.Entities;

public class Category : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? ParentCategoryId { get; set; }
    public Category? ParentCategory { get; set; }
    public ICollection<Category> SubCategories { get; set; } = new List<Category>();
    public ICollection<Product> Products { get; set; } = new List<Product>();
}

public class Brand : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public ICollection<Product> Products { get; set; } = new List<Product>();
}

public class Size : BaseEntity
{
    public string Code { get; set; } = string.Empty; // S, M, L, XL, 30, 32, etc.
    public string Name { get; set; } = string.Empty; // Small, Medium, Large, Waist 30
    public string CategoryType { get; set; } = "Apparel"; // Apparel, Footwear, Accessories
    public int DisplayOrder { get; set; } = 0;
}

public class Color : BaseEntity
{
    public string Code { get; set; } = string.Empty; // BLK, WHT, NVY, RED
    public string Name { get; set; } = string.Empty; // Black, White, Navy Blue, Red
    public string HexValue { get; set; } = "#000000";
}

public class HsnCode : BaseEntity
{
    public string Code { get; set; } = string.Empty; // 6109 (T-Shirts), 6203 (Suits/Trousers)
    public string Description { get; set; } = string.Empty;
    public decimal GstRate { get; set; } = 5.0m; // 5%, 12%, 18%
    public decimal? ThresholdAmount { get; set; } = 1000m; // For apparel slab differentiation
    public decimal? HighGstRate { get; set; } = 12.0m; // If price exceeds threshold
}

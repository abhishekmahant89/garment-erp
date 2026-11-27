using GarmentERP.Domain.Entities;

namespace GarmentERP.Domain.Services;

public interface IVariantMatrixGenerator
{
    IReadOnlyList<ProductVariant> GenerateMatrix(
        Product product,
        IEnumerable<Size> sizes,
        IEnumerable<Color> colors,
        decimal baseCostPrice,
        decimal baseSellingPrice,
        decimal mrp,
        string fit = "Regular");
}

public class VariantMatrixGenerator : IVariantMatrixGenerator
{
    public IReadOnlyList<ProductVariant> GenerateMatrix(
        Product product,
        IEnumerable<Size> sizes,
        IEnumerable<Color> colors,
        decimal baseCostPrice,
        decimal baseSellingPrice,
        decimal mrp,
        string fit = "Regular")
    {
        var variants = new List<ProductVariant>();
        var random = new Random();

        foreach (var color in colors)
        {
            foreach (var size in sizes)
            {
                var sku = $"{product.StyleCode.Trim().ToUpperInvariant()}-{color.Code.Trim().ToUpperInvariant()}-{size.Code.Trim().ToUpperInvariant()}";
                
                // Generate a deterministic or pseudo-unique 12-digit barcode body, then calculate EAN-13 check digit
                var uniqueSeed = Math.Abs(HashCode.Combine(product.StyleCode, color.Code, size.Code)) % 100000000;
                var raw12 = $"890{uniqueSeed:D9}";
                var checkDigit = CalculateEan13CheckDigit(raw12);
                var barcode = $"{raw12}{checkDigit}";

                var variant = new ProductVariant
                {
                    Id = Guid.NewGuid(),
                    ProductId = product.Id,
                    Product = product,
                    SizeId = size.Id,
                    Size = size,
                    ColorId = color.Id,
                    Color = color,
                    SKU = sku,
                    Barcode = barcode,
                    Fit = fit,
                    CostPrice = baseCostPrice,
                    SellingPrice = baseSellingPrice,
                    MRP = mrp,
                    ReorderLevel = 5,
                    IsActive = true
                };

                variants.Add(variant);
            }
        }

        return variants;
    }

    public static int CalculateEan13CheckDigit(string raw12)
    {
        if (raw12.Length != 12) return 0;
        int sum = 0;
        for (int i = 0; i < 12; i++)
        {
            int digit = raw12[i] - '0';
            sum += (i % 2 == 0) ? digit : digit * 3;
        }
        int remainder = sum % 10;
        return (remainder == 0) ? 0 : 10 - remainder;
    }
}

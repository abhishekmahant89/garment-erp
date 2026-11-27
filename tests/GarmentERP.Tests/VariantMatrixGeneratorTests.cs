using GarmentERP.Domain.Entities;
using GarmentERP.Domain.Services;
using Xunit;

namespace GarmentERP.Tests;

public class VariantMatrixGeneratorTests
{
    [Fact]
    public void GenerateMatrix_With5SizesAnd5Colors_ProducesExactly25Variants()
    {
        // Arrange
        var generator = new VariantMatrixGenerator();
        var product = new Product
        {
            Id = Guid.NewGuid(),
            StyleCode = "SHIRT-SLM",
            Name = "Men's Luxury Cotton Shirt"
        };

        var sizes = new List<Size>
        {
            new() { Id = Guid.NewGuid(), Code = "S", Name = "Small" },
            new() { Id = Guid.NewGuid(), Code = "M", Name = "Medium" },
            new() { Id = Guid.NewGuid(), Code = "L", Name = "Large" },
            new() { Id = Guid.NewGuid(), Code = "XL", Name = "Extra Large" },
            new() { Id = Guid.NewGuid(), Code = "XXL", Name = "Double XL" },
        };

        var colors = new List<Color>
        {
            new() { Id = Guid.NewGuid(), Code = "BLK", Name = "Black" },
            new() { Id = Guid.NewGuid(), Code = "WHT", Name = "White" },
            new() { Id = Guid.NewGuid(), Code = "BLU", Name = "Blue" },
            new() { Id = Guid.NewGuid(), Code = "RED", Name = "Red" },
            new() { Id = Guid.NewGuid(), Code = "OLV", Name = "Olive" },
        };

        // Act
        var variants = generator.GenerateMatrix(product, sizes, colors, 600m, 1200m, 1500m, "Slim Fit");

        // Assert
        Assert.Equal(25, variants.Count);
        Assert.All(variants, v =>
        {
            Assert.NotEmpty(v.SKU);
            Assert.NotEmpty(v.Barcode);
            Assert.Equal(1200m, v.SellingPrice);
            Assert.Equal(1500m, v.MRP);
            Assert.StartsWith("SHIRT-SLM-", v.SKU);
        });

        // Ensure all SKUs are distinct
        var uniqueSkus = variants.Select(v => v.SKU).Distinct().Count();
        Assert.Equal(25, uniqueSkus);

        // Ensure all Barcodes are distinct
        var uniqueBarcodes = variants.Select(v => v.Barcode).Distinct().Count();
        Assert.Equal(25, uniqueBarcodes);
    }
}

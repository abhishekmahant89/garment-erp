using GarmentERP.Domain.Services;
using Xunit;

namespace GarmentERP.Tests;

public class GstCalculatorTests
{
    private readonly GstCalculator _calculator = new();

    [Fact]
    public void CalculateItemTax_IntraState_BelowThreshold_Applies5PercentSplit()
    {
        // 1 piece @ 900 (<= 1000 threshold), base 5%, high 12%
        var result = _calculator.CalculateItemTax(
            unitPrice: 900m,
            quantity: 1,
            discountAmount: 0,
            baseGstRate: 5.0m,
            thresholdAmount: 1000m,
            highGstRate: 12.0m,
            isInterState: false);

        Assert.Equal(5.0m, result.TaxRate);
        Assert.Equal(2.5m, result.CgstRate);
        Assert.Equal(2.5m, result.SgstRate);
        Assert.Equal(0m, result.IgstRate);
        Assert.Equal(22.50m, result.CgstAmount);
        Assert.Equal(22.50m, result.SgstAmount);
        Assert.Equal(45.00m, result.TotalTax);
        Assert.Equal(945.00m, result.TotalAmount);
    }

    [Fact]
    public void CalculateItemTax_IntraState_AboveThreshold_Applies12PercentSplit()
    {
        // 1 piece @ 1500 (> 1000 threshold), garment slab switches to 12%
        var result = _calculator.CalculateItemTax(
            unitPrice: 1500m,
            quantity: 1,
            discountAmount: 0,
            baseGstRate: 5.0m,
            thresholdAmount: 1000m,
            highGstRate: 12.0m,
            isInterState: false);

        Assert.Equal(12.0m, result.TaxRate);
        Assert.Equal(6.0m, result.CgstRate);
        Assert.Equal(6.0m, result.SgstRate);
        Assert.Equal(90.00m, result.CgstAmount);
        Assert.Equal(90.00m, result.SgstAmount);
        Assert.Equal(180.00m, result.TotalTax);
        Assert.Equal(1680.00m, result.TotalAmount);
    }

    [Fact]
    public void CalculateItemTax_InterState_AppliesFullIgst()
    {
        // Inter-state supply: IGST only
        var result = _calculator.CalculateItemTax(
            unitPrice: 1500m,
            quantity: 1,
            discountAmount: 0,
            baseGstRate: 5.0m,
            thresholdAmount: 1000m,
            highGstRate: 12.0m,
            isInterState: true);

        Assert.Equal(12.0m, result.TaxRate);
        Assert.Equal(0m, result.CgstRate);
        Assert.Equal(0m, result.SgstRate);
        Assert.Equal(12.0m, result.IgstRate);
        Assert.Equal(180.00m, result.IgstAmount);
        Assert.Equal(180.00m, result.TotalTax);
    }

    [Fact]
    public void CalculateInvoice_WithDiscountAndRoundOff_CalculatesCorrectGrandTotal()
    {
        var items = new List<(decimal, decimal, decimal, decimal, decimal?, decimal?)>
        {
            (1299m, 2, 100m, 5.0m, 1000m, 12.0m), // 2598 - 100 = 2498 taxable (each 1249 > 1000 -> 12% GST)
        };

        var invoice = _calculator.CalculateInvoice(items, invoiceLevelDiscount: 200m, isInterState: false);

        Assert.True(invoice.GrandTotal > 0);
        Assert.Equal(invoice.NetAmount + invoice.RoundOff, invoice.GrandTotal);
    }
}

namespace GarmentERP.Domain.Services;

public record GstItemCalculationResult(
    decimal UnitPrice,
    decimal Quantity,
    decimal ItemDiscount,
    decimal TaxableAmount,
    decimal TaxRate,
    decimal CgstRate,
    decimal CgstAmount,
    decimal SgstRate,
    decimal SgstAmount,
    decimal IgstRate,
    decimal IgstAmount,
    decimal TotalTax,
    decimal TotalAmount
);

public record InvoiceCalculationResult(
    decimal SubTotal,
    decimal TotalDiscount,
    decimal TaxableAmount,
    decimal TotalCgst,
    decimal TotalSgst,
    decimal TotalIgst,
    decimal TotalTax,
    decimal NetAmount,
    decimal RoundOff,
    decimal GrandTotal,
    IReadOnlyList<GstItemCalculationResult> Items
);

public interface IGstCalculator
{
    GstItemCalculationResult CalculateItemTax(
        decimal unitPrice,
        decimal quantity,
        decimal discountAmount,
        decimal baseGstRate,
        decimal? thresholdAmount,
        decimal? highGstRate,
        bool isInterState);

    InvoiceCalculationResult CalculateInvoice(
        IEnumerable<(decimal unitPrice, decimal quantity, decimal discountAmount, decimal baseGstRate, decimal? thresholdAmount, decimal? highGstRate)> items,
        decimal invoiceLevelDiscount,
        bool isInterState);
}

public class GstCalculator : IGstCalculator
{
    public GstItemCalculationResult CalculateItemTax(
        decimal unitPrice,
        decimal quantity,
        decimal discountAmount,
        decimal baseGstRate,
        decimal? thresholdAmount,
        decimal? highGstRate,
        bool isInterState)
    {
        var lineGross = unitPrice * quantity;
        var taxable = Math.Max(0, lineGross - discountAmount);

        // Garment slab rule: check per-piece effective price
        var perUnitEffectivePrice = quantity > 0 ? (taxable / quantity) : 0;
        var appliedGstRate = baseGstRate;

        if (thresholdAmount.HasValue && highGstRate.HasValue && perUnitEffectivePrice > thresholdAmount.Value)
        {
            appliedGstRate = highGstRate.Value;
        }

        decimal cgstRate = 0, sgstRate = 0, igstRate = 0;
        decimal cgstAmount = 0, sgstAmount = 0, igstAmount = 0;

        if (isInterState)
        {
            igstRate = appliedGstRate;
            igstAmount = Math.Round(taxable * (igstRate / 100m), 2, MidpointRounding.AwayFromZero);
        }
        else
        {
            cgstRate = appliedGstRate / 2m;
            sgstRate = appliedGstRate / 2m;
            cgstAmount = Math.Round(taxable * (cgstRate / 100m), 2, MidpointRounding.AwayFromZero);
            sgstAmount = Math.Round(taxable * (sgstRate / 100m), 2, MidpointRounding.AwayFromZero);
        }

        var totalTax = cgstAmount + sgstAmount + igstAmount;
        var totalAmount = taxable + totalTax;

        return new GstItemCalculationResult(
            unitPrice,
            quantity,
            discountAmount,
            taxable,
            appliedGstRate,
            cgstRate,
            cgstAmount,
            sgstRate,
            sgstAmount,
            igstRate,
            igstAmount,
            totalTax,
            totalAmount);
    }

    public InvoiceCalculationResult CalculateInvoice(
        IEnumerable<(decimal unitPrice, decimal quantity, decimal discountAmount, decimal baseGstRate, decimal? thresholdAmount, decimal? highGstRate)> items,
        decimal invoiceLevelDiscount,
        bool isInterState)
    {
        var itemList = items.ToList();
        var rawSubTotal = itemList.Sum(x => x.unitPrice * x.quantity);
        var totalItemDiscounts = itemList.Sum(x => x.discountAmount);
        var subTotalAfterItemDiscounts = Math.Max(0, rawSubTotal - totalItemDiscounts);

        // Distribute invoice level discount proportionally
        var calculatedItems = new List<GstItemCalculationResult>();
        foreach (var item in itemList)
        {
            var lineGrossAfterItemDiscount = (item.unitPrice * item.quantity) - item.discountAmount;
            var proportionalInvoiceDiscount = (subTotalAfterItemDiscounts > 0 && invoiceLevelDiscount > 0)
                ? Math.Round(invoiceLevelDiscount * (lineGrossAfterItemDiscount / subTotalAfterItemDiscounts), 2, MidpointRounding.AwayFromZero)
                : 0m;

            var totalLineDiscount = item.discountAmount + proportionalInvoiceDiscount;
            var lineResult = CalculateItemTax(
                item.unitPrice,
                item.quantity,
                totalLineDiscount,
                item.baseGstRate,
                item.thresholdAmount,
                item.highGstRate,
                isInterState);

            calculatedItems.Add(lineResult);
        }

        var totalTaxable = calculatedItems.Sum(x => x.TaxableAmount);
        var totalCgst = calculatedItems.Sum(x => x.CgstAmount);
        var totalSgst = calculatedItems.Sum(x => x.SgstAmount);
        var totalIgst = calculatedItems.Sum(x => x.IgstAmount);
        var totalTax = totalCgst + totalSgst + totalIgst;
        var netAmount = totalTaxable + totalTax;

        // Round to nearest integer (standard in Indian retail)
        var grandTotal = Math.Round(netAmount, 0, MidpointRounding.AwayFromZero);
        var roundOff = grandTotal - netAmount;

        return new InvoiceCalculationResult(
            rawSubTotal,
            totalItemDiscounts + invoiceLevelDiscount,
            totalTaxable,
            totalCgst,
            totalSgst,
            totalIgst,
            totalTax,
            netAmount,
            roundOff,
            grandTotal,
            calculatedItems);
    }
}

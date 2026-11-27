using GarmentERP.Application.DTOs;
using GarmentERP.Reporting.Invoices;
using GarmentERP.Reporting.Thermal;
using Xunit;

namespace GarmentERP.Tests;

public class ReceiptPrintingTests
{
    private readonly EscPosReceiptBuilder _printer = new();
    private readonly A4InvoiceGenerator _a4Generator = new();

    private readonly StoreReceiptHeader _header = new(
        "GRAND FASHION STORE",
        "Flagship Store",
        "Commercial Complex, MG Road",
        "9876543210",
        "29AAAAA0000A1Z5"
    );

    private readonly SaleResponseDto _sampleSale = new(
        Guid.NewGuid(),
        "INV-20261127-TEST01",
        2598m,
        100m,
        2498m,
        62.45m,
        62.45m,
        0,
        0.10m,
        2623m,
        DateTime.UtcNow,
        new List<SaleItemDto>
        {
            new(Guid.NewGuid(), "SHIRT-SLM-BLK-M", "8901234567890", "Men's Cotton Shirt", "M", "Black", "6205", 2, 1299m, 1499m, 100m, 5.0m, 124.90m, 2622.90m)
        }
    );

    [Fact]
    public void GeneratePlainTextReceipt_IncludesHeaderStoreGstinAndItems()
    {
        var receipt80 = _printer.GeneratePlainTextReceipt(_sampleSale, _header, null, ThermalPaperWidth.Width80mm);
        Assert.Contains("GRAND FASHION STORE", receipt80);
        Assert.Contains("29AAAAA0000A1Z5", receipt80);
        Assert.Contains("INV-20261127-TEST01", receipt80);
        Assert.Contains("Men's Cotton Shirt", receipt80);
        Assert.Contains("Rs.2,623.00", receipt80);

        var receipt58 = _printer.GeneratePlainTextReceipt(_sampleSale, _header, null, ThermalPaperWidth.Width58mm);
        Assert.Contains("GRAND FASHION", receipt58);
        Assert.Contains("Rs.2,623.00", receipt58);
    }

    [Fact]
    public void GenerateEscPosBytes_ContainsDrawerKickAndCutCommands()
    {
        var bytes = _printer.GenerateEscPosBytes(_sampleSale, _header, null, ThermalPaperWidth.Width80mm, openCashDrawer: true);

        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 100);

        // Check ESC @ (init): 0x1B, 0x40
        Assert.Equal(0x1B, bytes[0]);
        Assert.Equal(0x40, bytes[1]);

        // Check ESC p (drawer kick): 0x1B, 0x70
        Assert.Equal(0x1B, bytes[2]);
        Assert.Equal(0x70, bytes[3]);

        // Check Cut command at the end: 0x1D, 0x56 (GS V)
        Assert.Equal(0x1D, bytes[^4]);
        Assert.Equal(0x56, bytes[^3]);
    }

    [Fact]
    public void GenerateA4HtmlInvoice_ProducesValidHtmlWithTaxTable()
    {
        var html = _a4Generator.GenerateA4HtmlInvoice(_sampleSale, _header);

        Assert.Contains("<!DOCTYPE html>", html);
        Assert.Contains("TAX INVOICE", html);
        Assert.Contains("INV-20261127-TEST01", html);
        Assert.Contains("29AAAAA0000A1Z5", html);
        Assert.Contains("Rs.2,623.00", html);
    }
}

using System.Text;
using GarmentERP.Application.DTOs;
using GarmentERP.Domain.Entities;

namespace GarmentERP.Reporting.Thermal;

public enum ThermalPaperWidth
{
    Width58mm = 32, // 32 characters per line
    Width80mm = 48  // 48 characters per line
}

public record StoreReceiptHeader(
    string StoreName,
    string BranchName,
    string Address,
    string Phone,
    string Gstin,
    string FssaiOrStateCode = "29-KARNATAKA"
);

public interface IReceiptPrinterService
{
    string GeneratePlainTextReceipt(SaleResponseDto sale, StoreReceiptHeader header, List<PaymentRequest>? payments = null, ThermalPaperWidth width = ThermalPaperWidth.Width80mm);
    byte[] GenerateEscPosBytes(SaleResponseDto sale, StoreReceiptHeader header, List<PaymentRequest>? payments = null, ThermalPaperWidth width = ThermalPaperWidth.Width80mm, bool openCashDrawer = true);
}

public class EscPosReceiptBuilder : IReceiptPrinterService
{
    // Standard ESC/POS Command Constants
    private static readonly byte[] EscInit = new byte[] { 0x1B, 0x40 }; // ESC @
    private static readonly byte[] EscAlignLeft = new byte[] { 0x1B, 0x61, 0x00 }; // ESC a 0
    private static readonly byte[] EscAlignCenter = new byte[] { 0x1B, 0x61, 0x01 }; // ESC a 1
    private static readonly byte[] EscAlignRight = new byte[] { 0x1B, 0x61, 0x02 }; // ESC a 2
    private static readonly byte[] EscBoldOn = new byte[] { 0x1B, 0x45, 0x01 }; // ESC E 1
    private static readonly byte[] EscBoldOff = new byte[] { 0x1B, 0x45, 0x00 }; // ESC E 0
    private static readonly byte[] EscDoubleSize = new byte[] { 0x1D, 0x21, 0x11 }; // GS ! 0x11
    private static readonly byte[] EscNormalSize = new byte[] { 0x1D, 0x21, 0x00 }; // GS ! 0x00
    private static readonly byte[] EscDrawerKick = new byte[] { 0x1B, 0x70, 0x00, 0x19, 0xFA }; // ESC p 0 25 250
    private static readonly byte[] EscCutPaper = new byte[] { 0x1D, 0x56, 0x42, 0x00 }; // GS V 66 0

    public string GeneratePlainTextReceipt(
        SaleResponseDto sale,
        StoreReceiptHeader header,
        List<PaymentRequest>? payments = null,
        ThermalPaperWidth width = ThermalPaperWidth.Width80mm)
    {
        int w = (int)width;
        var sb = new StringBuilder();

        // Header
        sb.AppendLine(Center(header.StoreName, w));
        sb.AppendLine(Center(header.BranchName, w));
        sb.AppendLine(Center(header.Address, w));
        sb.AppendLine(Center($"Ph: {header.Phone}", w));
        sb.AppendLine(Center($"GSTIN: {header.Gstin}", w));
        sb.AppendLine(new string('=', w));

        // Invoice Meta
        sb.AppendLine(FormatTwoColumn($"Invoice: {sale.InvoiceNumber}", $"Date: {sale.CreatedAt:dd/MM/yy HH:mm}", w));
        sb.AppendLine(new string('-', w));

        // Columns
        if (width == ThermalPaperWidth.Width80mm)
        {
            sb.AppendLine(PadRow("Item Description", "Qty", "Price", "Total", w));
        }
        else
        {
            sb.AppendLine(PadRow58("Item", "Qty", "Total", w));
        }
        sb.AppendLine(new string('-', w));

        // Items
        foreach (var item in sale.Items)
        {
            var title = $"{item.ProductName} ({item.SizeName}/{item.ColorName})";
            if (width == ThermalPaperWidth.Width80mm)
            {
                if (title.Length > 22) title = title[..21] + ".";
                sb.AppendLine(PadRow(title, $"{item.Quantity:0}", $"Rs.{item.UnitPrice:0.00}", $"Rs.{item.TotalAmount:0.00}", w));
            }
            else
            {
                if (title.Length > 16) title = title[..15] + ".";
                sb.AppendLine(PadRow58(title, $"{item.Quantity:0}", $"Rs.{item.TotalAmount:0.00}", w));
            }

            if (item.DiscountAmount > 0)
            {
                sb.AppendLine($"  (Discount Applied: Rs.{item.DiscountAmount:0.00})");
            }
        }
        sb.AppendLine(new string('-', w));

        // Totals
        sb.AppendLine(FormatTwoColumn("Sub Total:", $"Rs.{sale.SubTotal:N2}", w));
        if (sale.DiscountAmount > 0)
        {
            sb.AppendLine(FormatTwoColumn("Total Discount:", $"-Rs.{sale.DiscountAmount:N2}", w));
        }
        sb.AppendLine(FormatTwoColumn("CGST Amount:", $"Rs.{sale.CgstAmount:N2}", w));
        sb.AppendLine(FormatTwoColumn("SGST Amount:", $"Rs.{sale.SgstAmount:N2}", w));
        if (sale.IgstAmount > 0)
        {
            sb.AppendLine(FormatTwoColumn("IGST Amount:", $"Rs.{sale.IgstAmount:N2}", w));
        }
        if (sale.RoundOff != 0)
        {
            sb.AppendLine(FormatTwoColumn("Round Off:", $"Rs.{sale.RoundOff:N2}", w));
        }
        sb.AppendLine(new string('=', w));

        // Grand Total
        sb.AppendLine(FormatTwoColumn("GRAND TOTAL:", $"Rs.{sale.TotalAmount:N2}", w));
        sb.AppendLine(new string('=', w));

        // Payment Tender Breakdown
        if (payments != null && payments.Any())
        {
            sb.AppendLine("PAYMENT DETAILS:");
            foreach (var p in payments)
            {
                sb.AppendLine(FormatTwoColumn($"  {p.Method}:", $"Rs.{p.Amount:N2}", w));
            }
            sb.AppendLine(new string('-', w));
        }

        // Return Policy & Footer
        sb.AppendLine(Center("Exchange within 14 days with tag & bill.", w));
        sb.AppendLine(Center("No cash refund. Credit notes valid 60 days.", w));
        sb.AppendLine(Center("*** THANK YOU FOR SHOPPING! ***", w));
        sb.AppendLine();

        return sb.ToString();
    }

    public byte[] GenerateEscPosBytes(
        SaleResponseDto sale,
        StoreReceiptHeader header,
        List<PaymentRequest>? payments = null,
        ThermalPaperWidth width = ThermalPaperWidth.Width80mm,
        bool openCashDrawer = true)
    {
        using var ms = new MemoryStream();

        // 1. Initialize
        ms.Write(EscInit);

        // 2. Open Cash Drawer (if requested)
        if (openCashDrawer)
        {
            ms.Write(EscDrawerKick);
        }

        // 3. Header in Center & Bold
        ms.Write(EscAlignCenter);
        ms.Write(EscDoubleSize);
        ms.Write(EscBoldOn);
        WriteText(ms, $"{header.StoreName}\n");
        ms.Write(EscNormalSize);
        ms.Write(EscBoldOff);

        WriteText(ms, $"{header.BranchName}\n");
        WriteText(ms, $"{header.Address}\n");
        WriteText(ms, $"Ph: {header.Phone}\n");
        WriteText(ms, $"GSTIN: {header.Gstin}\n");

        int w = (int)width;
        WriteText(ms, new string('=', w) + "\n");

        // 4. Invoice Details
        ms.Write(EscAlignLeft);
        WriteText(ms, FormatTwoColumn($"Invoice: {sale.InvoiceNumber}", $"Date: {sale.CreatedAt:dd/MM/yy HH:mm}", w) + "\n");
        WriteText(ms, new string('-', w) + "\n");

        // 5. Items
        if (width == ThermalPaperWidth.Width80mm)
        {
            ms.Write(EscBoldOn);
            WriteText(ms, PadRow("Item Description", "Qty", "Price", "Total", w) + "\n");
            ms.Write(EscBoldOff);
        }
        else
        {
            ms.Write(EscBoldOn);
            WriteText(ms, PadRow58("Item", "Qty", "Total", w) + "\n");
            ms.Write(EscBoldOff);
        }
        WriteText(ms, new string('-', w) + "\n");

        foreach (var item in sale.Items)
        {
            var title = $"{item.ProductName} ({item.SizeName}/{item.ColorName})";
            if (width == ThermalPaperWidth.Width80mm)
            {
                if (title.Length > 22) title = title[..21] + ".";
                WriteText(ms, PadRow(title, $"{item.Quantity:0}", $"{item.UnitPrice:0.00}", $"{item.TotalAmount:0.00}", w) + "\n");
            }
            else
            {
                if (title.Length > 16) title = title[..15] + ".";
                WriteText(ms, PadRow58(title, $"{item.Quantity:0}", $"{item.TotalAmount:0.00}", w) + "\n");
            }
        }
        WriteText(ms, new string('-', w) + "\n");

        // 6. Totals
        WriteText(ms, FormatTwoColumn("Sub Total:", $"Rs.{sale.SubTotal:N2}", w) + "\n");
        if (sale.DiscountAmount > 0)
        {
            WriteText(ms, FormatTwoColumn("Total Discount:", $"-Rs.{sale.DiscountAmount:N2}", w) + "\n");
        }
        WriteText(ms, FormatTwoColumn("CGST Amount:", $"Rs.{sale.CgstAmount:N2}", w) + "\n");
        WriteText(ms, FormatTwoColumn("SGST Amount:", $"Rs.{sale.SgstAmount:N2}", w) + "\n");
        if (sale.IgstAmount > 0)
        {
            WriteText(ms, FormatTwoColumn("IGST Amount:", $"Rs.{sale.IgstAmount:N2}", w) + "\n");
        }
        WriteText(ms, new string('=', w) + "\n");

        // Grand Total Double Size
        ms.Write(EscBoldOn);
        ms.Write(EscDoubleSize);
        WriteText(ms, FormatTwoColumn("GRAND TOTAL:", $"Rs.{sale.TotalAmount:N2}", w / 2) + "\n");
        ms.Write(EscNormalSize);
        ms.Write(EscBoldOff);
        WriteText(ms, new string('=', w) + "\n");

        // Footer
        ms.Write(EscAlignCenter);
        WriteText(ms, "Exchange within 14 days with tag & bill.\n");
        WriteText(ms, "*** THANK YOU FOR SHOPPING! ***\n\n\n\n");

        // 7. Cut Paper
        ms.Write(EscCutPaper);

        return ms.ToArray();
    }

    private static void WriteText(MemoryStream ms, string text)
    {
        var bytes = Encoding.ASCII.GetBytes(text);
        ms.Write(bytes, 0, bytes.Length);
    }

    private static string Center(string text, int width)
    {
        if (text.Length >= width) return text[..width];
        int left = (width - text.Length) / 2;
        return text.PadLeft(left + text.Length).PadRight(width);
    }

    private static string FormatTwoColumn(string left, string right, int width)
    {
        int space = width - left.Length - right.Length;
        if (space < 1) space = 1;
        return left + new string(' ', space) + right;
    }

    private static string PadRow(string col1, string col2, string col3, string col4, int width)
    {
        // For 80mm (48 chars): 24 chars, 4 chars, 9 chars, 11 chars
        return $"{col1.PadRight(23)} {col2.PadLeft(4)} {col3.PadLeft(8)} {col4.PadLeft(10)}";
    }

    private static string PadRow58(string col1, string col2, string col3, int width)
    {
        // For 58mm (32 chars): 17 chars, 4 chars, 9 chars
        return $"{col1.PadRight(17)} {col2.PadLeft(4)} {col3.PadLeft(9)}";
    }
}

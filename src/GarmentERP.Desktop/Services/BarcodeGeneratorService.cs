using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace GarmentERP.Desktop.Services;

public class BarcodeGeneratorService
{
    // Code 128 Pattern Dictionary (Subset B) - standard for alphanumeric retail SKUs
    private static readonly string[] Code128Patterns = new string[]
    {
        "212222", "222122", "222221", "121223", "121322", "131222", "122213", "122312", "132212", "221213", // 0-9
        "221312", "231212", "112232", "122132", "122231", "113222", "123122", "123221", "223211", "221132", // 10-19
        "221231", "213212", "223112", "312131", "311222", "321122", "321221", "312212", "322112", "322211", // 20-29
        "212123", "212321", "232121", "111323", "131123", "131321", "112313", "132113", "132311", "211313", // 30-39
        "231113", "231311", "112133", "112331", "132131", "113123", "113321", "133121", "313121", "211331", // 40-49
        "231131", "213113", "213311", "213131", "311123", "311321", "331121", "312113", "312311", "332111", // 50-59
        "314111", "221411", "431111", "111224", "111422", "121124", "121421", "141122", "141221", "112214", // 60-69
        "112412", "122114", "122411", "142112", "142211", "241211", "221114", "413111", "241112", "134111", // 70-79
        "111242", "121142", "121241", "114212", "124112", "124211", "411212", "421112", "421211", "212141", // 80-89
        "214121", "412121", "111143", "111341", "131141", "114113", "114311", "411113", "411311", "113141", // 90-99
        "114131", "311141", "411131", "211412", "211214", "211232", "2331112"                                 // 100-106
    };

    private const int StartCodeB = 104;
    private const int StopCode = 106;

    /// <summary>
    /// Encodes alphanumeric text into Code 128 bar pattern of widths
    /// </summary>
    public static string EncodeToCode128Pattern(string text)
    {
        if (string.IsNullOrEmpty(text)) text = "SKU-0000";

        var patterns = new List<string>();
        int checksum = StartCodeB;

        patterns.Add(Code128Patterns[StartCodeB]);

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            int code = c - 32;
            if (code < 0 || code > 95) code = 0; // fallback to space

            patterns.Add(Code128Patterns[code]);
            checksum += (i + 1) * code;
        }

        int checkDigit = checksum % 103;
        patterns.Add(Code128Patterns[checkDigit]);
        patterns.Add(Code128Patterns[StopCode]);

        return string.Concat(patterns);
    }

    /// <summary>
    /// Generates visual Barcode BitmapSource for any Product SKU or EAN-13 code
    /// </summary>
    public BitmapSource GenerateBarcodeBitmap(string text, int width = 340, int height = 90)
    {
        var pattern = EncodeToCode128Pattern(text);
        var visual = new DrawingVisual();

        using (var dc = visual.RenderOpen())
        {
            // White Background for scan readability
            dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, width, height));

            // Calculate module width
            int totalModules = 0;
            foreach (char ch in pattern)
            {
                totalModules += ch - '0';
            }

            double margin = 20;
            double availableWidth = width - (margin * 2);
            double moduleWidth = availableWidth / totalModules;
            double barHeight = height - 32;

            double currentX = margin;
            bool isBar = true;

            foreach (char ch in pattern)
            {
                int barModules = ch - '0';
                double w = barModules * moduleWidth;

                if (isBar)
                {
                    dc.DrawRectangle(Brushes.Black, null, new Rect(currentX, 10, w, barHeight));
                }

                currentX += w;
                isBar = !isBar;
            }

            // Draw Human Readable Text underneath
            var formattedText = new FormattedText(
                text,
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface(new FontFamily("Consolas, Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
                12,
                Brushes.Black,
                1.0
            );

            double textX = Math.Max(0, (width - formattedText.Width) / 2);
            dc.DrawText(formattedText, new Point(textX, height - 20));
        }

        var rtb = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(visual);
        rtb.Freeze();
        return rtb;
    }

    /// <summary>
    /// Generates a Retail Garment Hang Tag Label (50mm x 35mm format) with Brand, SKU, Size, Color, MRP, and Barcode
    /// </summary>
    public BitmapSource GenerateGarmentHangTagLabel(
        string storeName,
        string productName,
        string sku,
        string sizeName,
        string colorName,
        decimal mrp,
        decimal sellingPrice,
        string barcode)
    {
        int w = 420;
        int h = 300;
        var visual = new DrawingVisual();

        using (var dc = visual.RenderOpen())
        {
            // Clean white card background with subtle border
            dc.DrawRectangle(Brushes.White, new Pen(new SolidColorBrush(Color.FromRgb(203, 213, 225)), 1.5), new Rect(2, 2, w - 4, h - 4));

            // Top Header: Store Name
            var headerText = new FormattedText(
                string.IsNullOrWhiteSpace(storeName) ? "GRAND FASHION STORE" : storeName.ToUpperInvariant(),
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
                13,
                new SolidColorBrush(Color.FromRgb(30, 41, 59)),
                1.0
            );
            dc.DrawText(headerText, new Point((w - headerText.Width) / 2, 14));

            // Divider Line
            dc.DrawLine(new Pen(new SolidColorBrush(Color.FromRgb(226, 232, 240)), 1), new Point(16, 36), new Point(w - 16, 36));

            // Product Title
            var titleText = new FormattedText(
                productName.Length > 28 ? productName[..27] + "..." : productName,
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
                14,
                Brushes.Black,
                1.0
            );
            dc.DrawText(titleText, new Point(20, 46));

            // SKU & Spec Row
            var skuText = new FormattedText(
                $"SKU: {sku}",
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal),
                11,
                new SolidColorBrush(Color.FromRgb(71, 85, 105)),
                1.0
            );
            dc.DrawText(skuText, new Point(20, 68));

            var variantText = new FormattedText(
                $"SIZE: {sizeName}  |  COLOR: {colorName}",
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
                11,
                new SolidColorBrush(Color.FromRgb(15, 23, 42)),
                1.0
            );
            dc.DrawText(variantText, new Point(20, 86));

            // Price Pill: MRP (strikethrough) vs Selling Price
            var mrpText = new FormattedText(
                $"MRP: ₹{mrp:N2}",
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal),
                11,
                new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                1.0
            );
            dc.DrawText(mrpText, new Point(20, 108));
            // Strikethrough line
            dc.DrawLine(new Pen(new SolidColorBrush(Color.FromRgb(239, 68, 68)), 1.2), new Point(18, 114), new Point(20 + mrpText.Width, 114));

            var priceText = new FormattedText(
                $"OFFER: ₹{sellingPrice:N2}",
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
                15,
                new SolidColorBrush(Color.FromRgb(16, 185, 129)),
                1.0
            );
            dc.DrawText(priceText, new Point(130, 105));

            var taxText = new FormattedText(
                "(Incl. of all taxes)",
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface(new FontFamily("Segoe UI"), FontStyles.Italic, FontWeights.Normal, FontStretches.Normal),
                9,
                new SolidColorBrush(Color.FromRgb(100, 116, 139)),
                1.0
            );
            dc.DrawText(taxText, new Point(275, 110));

            // Draw Barcode inside the tag
            var codeToDraw = string.IsNullOrWhiteSpace(barcode) ? sku : barcode;
            var pattern = EncodeToCode128Pattern(codeToDraw);

            int totalModules = 0;
            foreach (char ch in pattern) totalModules += ch - '0';

            double barStartY = 135;
            double barH = 100;
            double margin = 26;
            double availW = w - (margin * 2);
            double modW = availW / totalModules;

            double currX = margin;
            bool isBar = true;

            foreach (char ch in pattern)
            {
                int bMod = ch - '0';
                double curW = bMod * modW;
                if (isBar)
                {
                    dc.DrawRectangle(Brushes.Black, null, new Rect(currX, barStartY, curW, barH));
                }
                currX += curW;
                isBar = !isBar;
            }

            // Barcode text below bars
            var codeText = new FormattedText(
                codeToDraw,
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface(new FontFamily("Consolas"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
                12,
                Brushes.Black,
                1.0
            );
            dc.DrawText(codeText, new Point((w - codeText.Width) / 2, barStartY + barH + 6));

            // Footer note
            var footText = new FormattedText(
                "Keep Tag for Returns & Exchange within 15 Days",
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal),
                9,
                new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                1.0
            );
            dc.DrawText(footText, new Point((w - footText.Width) / 2, h - 22));
        }

        var rtb = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(visual);
        rtb.Freeze();
        return rtb;
    }
}

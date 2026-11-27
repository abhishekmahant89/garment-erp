using System.Text;
using GarmentERP.Application.DTOs;
using GarmentERP.Reporting.Thermal;

namespace GarmentERP.Reporting.Invoices;

public interface IA4InvoiceGenerator
{
    string GenerateA4HtmlInvoice(SaleResponseDto sale, StoreReceiptHeader header, CustomerDto? customer = null, List<PaymentRequest>? payments = null);
}

public class A4InvoiceGenerator : IA4InvoiceGenerator
{
    public string GenerateA4HtmlInvoice(SaleResponseDto sale, StoreReceiptHeader header, CustomerDto? customer = null, List<PaymentRequest>? payments = null)
    {
        var sb = new StringBuilder();

        sb.Append($@"<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <title>Tax Invoice - {sale.InvoiceNumber}</title>
    <style>
        @page {{ size: A4; margin: 15mm; }}
        body {{ font-family: 'Segoe UI', Arial, sans-serif; font-size: 11pt; color: #1e293b; margin: 0; padding: 0; }}
        .header {{ border-bottom: 2px solid #0f172a; padding-bottom: 12px; margin-bottom: 15px; display: flex; justify-content: space-between; }}
        .store-title {{ font-size: 20pt; font-weight: bold; color: #0f172a; }}
        .invoice-title {{ font-size: 16pt; font-weight: bold; text-align: right; color: #2563eb; }}
        .meta-grid {{ display: flex; justify-content: space-between; margin-bottom: 20px; }}
        .meta-col {{ width: 48%; }}
        .meta-box {{ border: 1px solid #e2e8f0; border-radius: 6px; padding: 12px; background: #f8fafc; }}
        .meta-box h4 {{ margin: 0 0 6px 0; font-size: 11pt; color: #475569; text-transform: uppercase; }}
        table {{ width: 100%; border-collapse: collapse; margin-top: 15px; }}
        th {{ background: #0f172a; color: #ffffff; padding: 8px; font-size: 9pt; text-align: left; }}
        td {{ border-bottom: 1px solid #e2e8f0; padding: 8px; font-size: 9.5pt; }}
        .text-right {{ text-align: right; }}
        .text-center {{ text-align: center; }}
        .totals-section {{ margin-top: 15px; display: flex; justify-content: flex-end; }}
        .totals-table {{ width: 340px; }}
        .totals-table td {{ padding: 4px 8px; }}
        .grand-total {{ font-size: 14pt; font-weight: bold; color: #16a34a; background: #f0fdf4; }}
        .footer {{ margin-top: 40px; border-top: 1px solid #e2e8f0; padding-top: 15px; display: flex; justify-content: space-between; font-size: 9pt; color: #64748b; }}
        .signatory {{ text-align: center; width: 200px; border-top: 1px dashed #94a3b8; padding-top: 5px; margin-top: 30px; }}
    </style>
</head>
<body>
    <div class=""header"">
        <div>
            <div class=""store-title"">{header.StoreName}</div>
            <div>{header.BranchName}</div>
            <div>{header.Address}</div>
            <div>Phone: {header.Phone} | GSTIN: <strong>{header.Gstin}</strong></div>
        </div>
        <div>
            <div class=""invoice-title"">TAX INVOICE</div>
            <div>Invoice No: <strong>{sale.InvoiceNumber}</strong></div>
            <div>Date: {sale.CreatedAt:dd-MMM-yyyy HH:mm}</div>
            <div>Place of Supply: {header.FssaiOrStateCode}</div>
        </div>
    </div>

    <div class=""meta-grid"">
        <div class=""meta-col"">
            <div class=""meta-box"">
                <h4>Billed To (Customer)</h4>
                <div>Name: <strong>{(customer != null ? customer.Name : "Walk-in Retail Customer")}</strong></div>
                <div>Phone: {(customer != null ? customer.Phone : "N/A")}</div>
                <div>GSTIN: {(customer != null && !string.IsNullOrEmpty(customer.Gstin) ? customer.Gstin : "URP (Unregistered)")}</div>
            </div>
        </div>
        <div class=""meta-col"">
            <div class=""meta-box"">
                <h4>Payment & POS Information</h4>
                <div>Counter: Counter-01 | Cashier: Active Staff</div>
                <div>Payment Mode: {(payments != null && payments.Any() ? string.Join(", ", payments.Select(p => p.Method.ToString())) : "Cash")}</div>
                <div>Status: <strong>PAID & COMPLETED</strong></div>
            </div>
        </div>
    </div>

    <table>
        <thead>
            <tr>
                <th class=""text-center"" style=""width: 30px;"">#</th>
                <th>Item Description</th>
                <th class=""text-center"" style=""width: 70px;"">HSN</th>
                <th class=""text-right"" style=""width: 50px;"">Qty</th>
                <th class=""text-right"" style=""width: 75px;"">MRP</th>
                <th class=""text-right"" style=""width: 75px;"">Rate</th>
                <th class=""text-right"" style=""width: 65px;"">Disc.</th>
                <th class=""text-center"" style=""width: 55px;"">GST %</th>
                <th class=""text-right"" style=""width: 85px;"">Amount</th>
            </tr>
        </thead>
        <tbody>");

        int idx = 1;
        foreach (var item in sale.Items)
        {
            sb.Append($@"
            <tr>
                <td class=""text-center"">{idx++}</td>
                <td><strong>{item.ProductName}</strong><br/><small style=""color:#64748b;"">Size: {item.SizeName} | Color: {item.ColorName} | SKU: {item.SKU}</small></td>
                <td class=""text-center"">{item.HsnCode}</td>
                <td class=""text-right"">{item.Quantity:0}</td>
                <td class=""text-right"">Rs.{item.MRP:N2}</td>
                <td class=""text-right"">Rs.{item.UnitPrice:N2}</td>
                <td class=""text-right"">Rs.{item.DiscountAmount:N2}</td>
                <td class=""text-center"">{item.TaxRate}%</td>
                <td class=""text-right""><strong>Rs.{item.TotalAmount:N2}</strong></td>
            </tr>");
        }

        sb.Append($@"
        </tbody>
    </table>

    <div class=""totals-section"">
        <table class=""totals-table"">
            <tr>
                <td>Sub Total:</td>
                <td class=""text-right"">Rs.{sale.SubTotal:N2}</td>
            </tr>
            {(sale.DiscountAmount > 0 ? $@"<tr><td>Discount:</td><td class=""text-right"" style=""color:#ef4444;"">- Rs.{sale.DiscountAmount:N2}</td></tr>" : "")}
            <tr>
                <td>Taxable Value:</td>
                <td class=""text-right"">Rs.{sale.TaxableAmount:N2}</td>
            </tr>
            <tr>
                <td>CGST:</td>
                <td class=""text-right"">Rs.{sale.CgstAmount:N2}</td>
            </tr>
            <tr>
                <td>SGST:</td>
                <td class=""text-right"">Rs.{sale.SgstAmount:N2}</td>
            </tr>
            {(sale.IgstAmount > 0 ? $@"<tr><td>IGST:</td><td class=""text-right"">Rs.{sale.IgstAmount:N2}</td></tr>" : "")}
            {(sale.RoundOff != 0 ? $@"<tr><td>Round Off:</td><td class=""text-right"">Rs.{sale.RoundOff:N2}</td></tr>" : "")}
            <tr class=""grand-total"">
                <td>NET PAYABLE:</td>
                <td class=""text-right"">Rs.{sale.TotalAmount:N2}</td>
            </tr>
        </table>
    </div>

    <div class=""footer"">
        <div style=""width: 60%;"">
            <strong>Terms &amp; Conditions:</strong><br/>
            1. Goods once sold can be exchanged within 14 days with original price tag and invoice.<br/>
            2. No cash refunds. Store credit will be issued valid for 60 days.<br/>
            3. Subject to local state jurisdiction.
        </div>
        <div style=""width: 30%; text-align: right;"">
            <div>For {header.StoreName}</div>
            <div class=""signatory"">Authorized Signatory</div>
        </div>
    </div>
</body>
</html>");

        return sb.ToString();
    }
}

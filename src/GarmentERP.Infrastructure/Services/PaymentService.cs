using GarmentERP.Application.DTOs;
using GarmentERP.Application.Interfaces;
using GarmentERP.Domain.Entities;

namespace GarmentERP.Infrastructure.Services;

public class PaymentService : IPaymentService
{
    public UpiPaymentIntentDto GenerateUpiQrPayload(string vpa, string payeeName, decimal amount, string invoiceNumber)
    {
        // Standard NPCI UPI URI Specification
        var cleanVpa = Uri.EscapeDataString(vpa.Trim());
        var cleanName = Uri.EscapeDataString(payeeName.Trim());
        var cleanInvoice = Uri.EscapeDataString(invoiceNumber.Trim());
        var formattedAmount = $"{amount:0.00}";

        var uriString = $"upi://pay?pa={cleanVpa}&pn={cleanName}&am={formattedAmount}&cu=INR&tn=Invoice%20{cleanInvoice}";

        return new UpiPaymentIntentDto(
            vpa,
            payeeName,
            amount,
            invoiceNumber,
            uriString
        );
    }

    public SplitPaymentValidationResult ValidateSplitTender(decimal netPayable, IEnumerable<PaymentRequest> payments)
    {
        var paymentList = payments?.ToList() ?? new List<PaymentRequest>();
        var totalTendered = paymentList.Sum(p => p.Amount);

        if (totalTendered >= netPayable)
        {
            var change = totalTendered - netPayable;
            return new SplitPaymentValidationResult(
                IsSufficient: true,
                TotalTendered: totalTendered,
                DueAmount: 0,
                ChangeReturnAmount: change,
                Message: change > 0
                    ? $"Payment complete. Return Rs.{change:N2} cash change to customer."
                    : "Payment settled exactly."
            );
        }

        var remainingDue = netPayable - totalTendered;
        return new SplitPaymentValidationResult(
            IsSufficient: false,
            TotalTendered: totalTendered,
            DueAmount: remainingDue,
            ChangeReturnAmount: 0,
            Message: $"Insufficient payment tendered. Remaining due: Rs.{remainingDue:N2}"
        );
    }
}

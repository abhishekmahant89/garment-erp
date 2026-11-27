using GarmentERP.Application.DTOs;
using GarmentERP.Domain.Entities;
using GarmentERP.Infrastructure.Services;
using Xunit;

namespace GarmentERP.Tests;

public class SplitPaymentAndUpiTests
{
    private readonly PaymentService _paymentService = new();

    [Fact]
    public void GenerateUpiQrPayload_CreatesValidNpciUri()
    {
        var result = _paymentService.GenerateUpiQrPayload("fashionstore@hdfcbank", "Grand Fashion Store", 2499.50m, "INV-10023");

        Assert.Equal("fashionstore@hdfcbank", result.Vpa);
        Assert.Equal(2499.50m, result.Amount);
        Assert.StartsWith("upi://pay?pa=fashionstore%40hdfcbank", result.QrPayloadString);
        Assert.Contains("am=2499.50", result.QrPayloadString);
        Assert.Contains("cu=INR", result.QrPayloadString);
    }

    [Fact]
    public void ValidateSplitTender_CalculatesChangeAndShortageCorrectly()
    {
        decimal netPayable = 2000m;

        // Case 1: Split Exact (Cash 1000 + UPI 1000)
        var exactPayments = new List<PaymentRequest>
        {
            new(PaymentMethod.Cash, 1000m),
            new(PaymentMethod.UPI, 1000m)
        };
        var exactResult = _paymentService.ValidateSplitTender(netPayable, exactPayments);
        Assert.True(exactResult.IsSufficient);
        Assert.Equal(0, exactResult.ChangeReturnAmount);

        // Case 2: Surplus Cash (Customer gives 2500 for a 2000 bill)
        var surplusPayments = new List<PaymentRequest>
        {
            new(PaymentMethod.Cash, 2500m)
        };
        var surplusResult = _paymentService.ValidateSplitTender(netPayable, surplusPayments);
        Assert.True(surplusResult.IsSufficient);
        Assert.Equal(500m, surplusResult.ChangeReturnAmount);

        // Case 3: Shortage (Customer gave only 1500)
        var shortagePayments = new List<PaymentRequest>
        {
            new(PaymentMethod.Cash, 1000m),
            new(PaymentMethod.Card, 500m)
        };
        var shortageResult = _paymentService.ValidateSplitTender(netPayable, shortagePayments);
        Assert.False(shortageResult.IsSufficient);
        Assert.Equal(500m, shortageResult.DueAmount);
    }
}

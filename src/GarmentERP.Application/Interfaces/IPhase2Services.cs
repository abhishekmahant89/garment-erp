using GarmentERP.Application.DTOs;
using GarmentERP.Domain.Entities;
using GarmentERP.Shared.Models;

namespace GarmentERP.Application.Interfaces;

public interface IPaymentService
{
    UpiPaymentIntentDto GenerateUpiQrPayload(string vpa, string payeeName, decimal amount, string invoiceNumber);
    SplitPaymentValidationResult ValidateSplitTender(decimal netPayable, IEnumerable<PaymentRequest> payments);
}

public interface ILoyaltyService
{
    Task<ApiResponse<LoyaltyAccrualResult>> AccruePointsAsync(Guid customerId, Guid? saleId, decimal eligibleSpend, CancellationToken ct = default);
    Task<ApiResponse<decimal>> RedeemPointsAsync(Guid customerId, int pointsToRedeem, decimal currentBillTotal, CancellationToken ct = default);
}

public interface IReturnExchangeService
{
    Task<ApiResponse<ReturnExchangeResponseDto>> ProcessReturnExchangeAsync(ProcessReturnExchangeRequest request, CancellationToken ct = default);
    Task<ApiResponse<CreditNote?>> ValidateCreditNoteAsync(string creditNoteNumber, CancellationToken ct = default);
}

public interface IShiftReportingService
{
    Task<ApiResponse<ShiftZReportDto>> GenerateShiftZReportAsync(Guid shiftId, CancellationToken ct = default);
    string FormatZReportReceipt(ShiftZReportDto report);
}

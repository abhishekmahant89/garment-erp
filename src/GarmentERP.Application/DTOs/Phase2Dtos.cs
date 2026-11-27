using GarmentERP.Domain.Entities;

namespace GarmentERP.Application.DTOs;

public record UpiPaymentIntentDto(
    string Vpa,
    string PayeeName,
    decimal Amount,
    string InvoiceNumber,
    string QrPayloadString
);

public record SplitPaymentValidationResult(
    bool IsSufficient,
    decimal TotalTendered,
    decimal DueAmount,
    decimal ChangeReturnAmount,
    string Message
);

public record LoyaltyAccrualResult(
    int PointsEarned,
    int NewTotalPoints,
    decimal EquivalentValue
);

public record ReturnItemRequest(
    Guid VariantId,
    decimal Quantity,
    decimal UnitRefundPrice,
    string Reason
);

public record ProcessReturnExchangeRequest(
    Guid OriginalSaleId,
    Guid BranchId,
    Guid CashierShiftId,
    List<ReturnItemRequest> ReturnItems,
    List<SaleItemRequest>? ExchangeItems = null,
    List<PaymentRequest>? Payments = null
);

public record ReturnExchangeResponseDto(
    string ReturnNumber,
    string? NewInvoiceNumber,
    decimal TotalReturnedValue,
    decimal TotalExchangeValue,
    decimal NetPayableOrRefund,
    string? CreditNoteNumber,
    DateTime ProcessedAt
);

public record ShiftZReportDto(
    Guid ShiftId,
    string CounterNumber,
    string CashierName,
    DateTime OpenedAt,
    DateTime ClosedAt,
    decimal StartingFloat,
    decimal CashSales,
    decimal CardSales,
    decimal UpiSales,
    decimal StoreCreditSales,
    decimal TotalReturns,
    decimal NetSales,
    decimal ExpectedCashInDrawer,
    decimal ActualCashCounted,
    decimal Discrepancy,
    int BillsCount
);

using GarmentERP.Domain.Entities;

namespace GarmentERP.Application.DTOs;

public record SaleItemRequest(
    Guid VariantId,
    decimal Quantity,
    decimal UnitPrice,
    decimal DiscountPercentage = 0,
    decimal DiscountAmount = 0
);

public record PaymentRequest(
    PaymentMethod Method,
    decimal Amount,
    string? ReferenceTransactionId = null
);

public record CreateSaleRequest(
    Guid BranchId,
    Guid CashierShiftId,
    Guid? CustomerId,
    List<SaleItemRequest> Items,
    List<PaymentRequest> Payments,
    decimal InvoiceDiscountAmount = 0,
    bool IsInterState = false,
    string? Notes = null
);

public record SaleResponseDto(
    Guid SaleId,
    string InvoiceNumber,
    decimal SubTotal,
    decimal DiscountAmount,
    decimal TaxableAmount,
    decimal CgstAmount,
    decimal SgstAmount,
    decimal IgstAmount,
    decimal RoundOff,
    decimal TotalAmount,
    DateTime CreatedAt,
    IReadOnlyList<SaleItemDto> Items
);

public record SaleItemDto(
    Guid VariantId,
    string SKU,
    string Barcode,
    string ProductName,
    string SizeName,
    string ColorName,
    string HsnCode,
    decimal Quantity,
    decimal UnitPrice,
    decimal MRP,
    decimal DiscountAmount,
    decimal TaxRate,
    decimal TaxAmount,
    decimal TotalAmount
);

public record HoldBillDto(
    Guid Id,
    string HoldTag,
    DateTime HeldAt,
    decimal EstimatedTotal,
    int ItemCount,
    string PayloadJson
);

public record CustomerDto(
    Guid Id,
    string Name,
    string Phone,
    string? Email,
    string? Gstin,
    string? Address,
    int LoyaltyPoints,
    decimal StoreCredit
);

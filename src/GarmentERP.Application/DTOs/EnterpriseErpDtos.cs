using GarmentERP.Domain.Entities;

namespace GarmentERP.Application.DTOs;

public record SupplierDto(
    Guid Id,
    string Name,
    string Code,
    string ContactPerson,
    string Phone,
    string Email,
    string City,
    string Gstin,
    decimal CurrentBalance
);

public record CreateSupplierDto(
    string Name,
    string Code,
    string ContactPerson,
    string Phone,
    string Email,
    string Address,
    string City,
    string State,
    string Gstin
);

public record PurchaseItemInputDto(
    Guid VariantId,
    decimal QuantityReceived,
    decimal UnitCostPrice,
    decimal MRP,
    decimal SellingPrice,
    decimal TaxRate
);

public record CreatePurchaseInvoiceRequest(
    string SupplierInvoiceNumber,
    Guid SupplierId,
    Guid BranchId,
    Guid WarehouseId,
    DateTime InvoiceDate,
    List<PurchaseItemInputDto> Items,
    decimal PaidAmount = 0,
    string? Notes = null
);

public record PurchaseInvoiceDto(
    Guid Id,
    string ErpPurchaseNumber,
    string SupplierInvoiceNumber,
    string SupplierName,
    DateTime InvoiceDate,
    decimal SubTotal,
    decimal TaxAmount,
    decimal TotalAmount,
    decimal PaidAmount,
    decimal BalanceAmount,
    PurchaseStatus Status,
    int ItemsCount
);

public record StockTransferItemInputDto(
    Guid VariantId,
    decimal Quantity
);

public record CreateStockTransferRequest(
    Guid SourceWarehouseId,
    Guid DestinationWarehouseId,
    List<StockTransferItemInputDto> Items,
    string DispatchedBy,
    string? Notes = null
);

public record StockTransferDto(
    Guid Id,
    string TransferNumber,
    string SourceWarehouseName,
    string DestinationWarehouseName,
    TransferStatus Status,
    DateTime DispatchedAt,
    DateTime? ReceivedAt,
    int TotalItems
);

public record CreatePromotionDto(
    string Name,
    string Code,
    PromotionType Type,
    decimal DiscountValue,
    decimal MinimumBillValue,
    int BuyQuantity,
    int FreeQuantity,
    Guid? TargetCategoryId,
    Guid? TargetBrandId,
    DateTime StartDate,
    DateTime EndDate,
    int? MaxUsageCount
);

public record PromotionEvaluationResult(
    bool IsApplicable,
    decimal TotalDiscountAmount,
    string PromotionName,
    string Message
);

public record CreateExpenseDto(
    Guid BranchId,
    Guid ExpenseCategoryId,
    Guid? CashierShiftId,
    decimal Amount,
    string PaidTo,
    PaymentMethod PaymentMode,
    string? Remarks
);

public record ExpenseDto(
    Guid Id,
    string CategoryName,
    decimal Amount,
    string PaidTo,
    PaymentMethod PaymentMode,
    DateTime ExpenseDate,
    string? Remarks
);

// Reports DTOs
public record DailySalesReport(
    DateTime Date,
    int TotalInvoices,
    decimal GrossSales,
    decimal TotalDiscounts,
    decimal NetSales,
    decimal TotalTaxCollected,
    decimal CashCollected,
    decimal CardCollected,
    decimal UpiCollected,
    int TotalItemsSold
);

public record StockValuationReport(
    int TotalUniqueSkus,
    decimal TotalQuantityOnHand,
    decimal TotalCostValuation,
    decimal TotalRetailValuation,
    decimal PotentialGrossProfitMargin
);

public record ProfitAndLossReport(
    DateTime FromDate,
    DateTime ToDate,
    decimal TotalSalesRevenue,
    decimal TotalCostOfGoodsSold,
    decimal GrossProfit,
    decimal GrossProfitMarginPercentage,
    decimal TotalOperatingExpenses,
    decimal NetProfitBeforeTax
);

using GarmentERP.Domain.Entities;

namespace GarmentERP.Application.DTOs;

public record StoreKpiSummary(
    decimal TodayNetSales,
    int TodayBillsCount,
    decimal TodayCashCollected,
    decimal TodayUpiCollected,
    decimal TodayCardCollected,
    int LowStockAlertsCount,
    int TotalActiveVariants
);

public record LowStockAlertDto(
    Guid VariantId,
    string SKU,
    string Barcode,
    string ProductName,
    string SizeName,
    string ColorName,
    decimal QuantityOnHand,
    int ReorderLevel
);

public record TopSellingGarmentDto(
    string StyleCode,
    string ProductName,
    int UnitsSold,
    decimal Revenue
);

public record Gstr1HsnSummaryRow(
    string HsnCode,
    string Description,
    decimal TotalQuantity,
    decimal TotalValue,
    decimal TaxableValue,
    decimal CgstAmount,
    decimal SgstAmount,
    decimal IgstAmount
);

public record Gstr1ReportDto(
    DateTime FromDate,
    DateTime ToDate,
    decimal TotalTaxableValue,
    decimal TotalCgst,
    decimal TotalSgst,
    decimal TotalIgst,
    decimal TotalTax,
    IReadOnlyList<Gstr1HsnSummaryRow> HsnRows
);

public record StaffLoginRequest(string Username, string Password);

public record StaffLoginResponse(
    bool Success,
    Guid UserId,
    string Username,
    string FullName,
    UserRole Role,
    Guid BranchId,
    string Token,
    string Message
);

public record CreateStaffUserRequest(
    string Username,
    string Password,
    string FullName,
    string Email,
    string Phone,
    UserRole Role,
    Guid BranchId
);

public record StaffUserDto(
    Guid Id,
    string Username,
    string FullName,
    string Email,
    string Phone,
    UserRole Role,
    DateTime? LastLoginAt
);

public record TrialBalanceRow(
    string AccountCode,
    string AccountName,
    AccountType Type,
    decimal DebitBalance,
    decimal CreditBalance
);

public record TrialBalanceReport(
    DateTime AsOfDate,
    IReadOnlyList<TrialBalanceRow> Rows,
    decimal TotalDebits,
    decimal TotalCredits,
    bool IsBalanced
);

public record BackupResultDto(
    bool Success,
    string BackupFilePath,
    long FileSizeBytes,
    DateTime CreatedAt,
    string Message
);

using GarmentERP.Application.DTOs;
using GarmentERP.Domain.Entities;
using GarmentERP.Shared.Models;

namespace GarmentERP.Application.Interfaces;

public interface IPurchaseService
{
    Task<ApiResponse<SupplierDto>> CreateSupplierAsync(CreateSupplierDto dto, CancellationToken ct = default);
    Task<ApiResponse<List<SupplierDto>>> GetSuppliersAsync(CancellationToken ct = default);
    Task<ApiResponse<PurchaseInvoiceDto>> ProcessPurchaseInvoiceAsync(CreatePurchaseInvoiceRequest request, CancellationToken ct = default);
    Task<ApiResponse<List<PurchaseInvoiceDto>>> GetRecentPurchasesAsync(int limit = 50, CancellationToken ct = default);
}

public interface IStockTransferService
{
    Task<ApiResponse<StockTransferDto>> CreateTransferDispatchAsync(CreateStockTransferRequest request, CancellationToken ct = default);
    Task<ApiResponse<StockTransferDto>> ConfirmTransferReceiveAsync(Guid transferId, string receivedBy, CancellationToken ct = default);
    Task<ApiResponse<List<StockTransferDto>>> GetPendingTransfersAsync(CancellationToken ct = default);
}

public interface IPromotionEngine
{
    Task<ApiResponse<Promotion>> CreatePromotionAsync(CreatePromotionDto dto, CancellationToken ct = default);
    Task<ApiResponse<List<Promotion>>> GetActivePromotionsAsync(CancellationToken ct = default);
    PromotionEvaluationResult EvaluatePromotion(Promotion promotion, decimal billSubTotal, int totalGarments);
}

public interface IExpenseService
{
    Task<ApiResponse<ExpenseDto>> RecordExpenseAsync(CreateExpenseDto dto, CancellationToken ct = default);
    Task<ApiResponse<List<ExpenseDto>>> GetDailyExpensesAsync(Guid branchId, DateTime date, CancellationToken ct = default);
}

public interface IEnterpriseReportService
{
    Task<ApiResponse<DailySalesReport>> GetDailySalesReportAsync(Guid branchId, DateTime date, CancellationToken ct = default);
    Task<ApiResponse<StockValuationReport>> GetStockValuationReportAsync(Guid? warehouseId = null, CancellationToken ct = default);
    Task<ApiResponse<ProfitAndLossReport>> GetProfitAndLossReportAsync(DateTime fromDate, DateTime toDate, CancellationToken ct = default);
}

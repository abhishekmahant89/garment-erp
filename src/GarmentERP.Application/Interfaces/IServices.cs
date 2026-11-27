using GarmentERP.Application.DTOs;
using GarmentERP.Domain.Entities;
using GarmentERP.Shared.Models;

namespace GarmentERP.Application.Interfaces;

public interface IProductCatalogService
{
    Task<ApiResponse<ProductDto>> CreateProductAsync(CreateProductDto dto, CancellationToken ct = default);
    Task<ApiResponse<List<ProductVariantDto>>> GenerateMatrixVariantsAsync(CreateVariantMatrixRequest request, CancellationToken ct = default);
    Task<ApiResponse<ProductVariantDto?>> FindVariantByBarcodeAsync(string barcodeOrSku, CancellationToken ct = default);
    Task<ApiResponse<List<ProductVariantDto>>> SearchVariantsAsync(string searchTerm, CancellationToken ct = default);
    Task<ApiResponse<List<CategoryDto>>> GetCategoriesAsync(CancellationToken ct = default);
    Task<ApiResponse<List<SizeDto>>> GetSizesAsync(CancellationToken ct = default);
    Task<ApiResponse<List<ColorDto>>> GetColorsAsync(CancellationToken ct = default);
}

public interface IPosBillingService
{
    Task<ApiResponse<SaleResponseDto>> ProcessSaleAsync(CreateSaleRequest request, CancellationToken ct = default);
    Task<ApiResponse<Guid>> HoldSaleAsync(string holdTag, CreateSaleRequest request, CancellationToken ct = default);
    Task<ApiResponse<CreateSaleRequest>> ResumeSaleAsync(Guid holdId, CancellationToken ct = default);
    Task<ApiResponse<List<HoldBillDto>>> GetHeldSalesAsync(CancellationToken ct = default);
    Task<ApiResponse<CashierShift>> OpenShiftAsync(Guid branchId, string counterNo, Guid cashierId, string cashierName, decimal floatAmount, CancellationToken ct = default);
    Task<ApiResponse<CashierShift>> CloseShiftAsync(Guid shiftId, decimal actualCash, string? notes, CancellationToken ct = default);
}

public interface ISyncEngine
{
    Task<ApiResponse<SyncPushResponse>> PushOutboxEventsAsync(CancellationToken ct = default);
    Task<ApiResponse<int>> PullMasterDataDeltasAsync(CancellationToken ct = default);
    Task<ApiResponse<(int Pending, int Syncing, int Done, int Failed)>> GetSyncQueueStatisticsAsync(CancellationToken ct = default);
    Task<ApiResponse<List<SyncOutbox>>> GetRecentOutboxEntriesAsync(int limit = 50, CancellationToken ct = default);
}

public interface ILicenseValidator
{
    Task<LicenseValidationResponse> ValidateAsync(string licenseKey, CancellationToken ct = default);
    string GetMachineFingerprint();
    bool IsCurrentTerminalLicensed();
}

public interface IErrorTelemetryReporter
{
    Task LogExceptionAsync(Exception ex, string? contextInfo = null, CancellationToken ct = default);
    Task<int> FlushPendingTelemetryAsync(CancellationToken ct = default);
}

using GarmentERP.Application.DTOs;
using GarmentERP.Domain.Entities;
using GarmentERP.Shared.Models;

namespace GarmentERP.Application.Interfaces;

public interface IDashboardService
{
    Task<ApiResponse<StoreKpiSummary>> GetStoreKpisAsync(Guid branchId, CancellationToken ct = default);
    Task<ApiResponse<List<LowStockAlertDto>>> GetLowStockAlertsAsync(Guid branchId, CancellationToken ct = default);
    Task<ApiResponse<List<TopSellingGarmentDto>>> GetTopSellingStylesAsync(Guid branchId, int limit = 5, CancellationToken ct = default);
}

public interface IGstReportService
{
    Task<ApiResponse<Gstr1ReportDto>> GenerateGstr1ReportAsync(DateTime fromDate, DateTime toDate, Guid? branchId = null, CancellationToken ct = default);
}

public interface IAccountingService
{
    Task PostSaleJournalAsync(Sale sale, CancellationToken ct = default);
    Task<ApiResponse<TrialBalanceReport>> GetTrialBalanceAsync(CancellationToken ct = default);
}

public interface IStaffAuthService
{
    Task<StaffLoginResponse> LoginAsync(StaffLoginRequest request, CancellationToken ct = default);
    Task<ApiResponse<StaffUserDto>> CreateStaffAsync(CreateStaffUserRequest request, CancellationToken ct = default);
    Task<ApiResponse<List<StaffUserDto>>> GetStaffMembersAsync(Guid branchId, CancellationToken ct = default);
}

public interface IBackupService
{
    Task<ApiResponse<BackupResultDto>> CreateDatabaseBackupAsync(string targetDirectory, CancellationToken ct = default);
    Task<ApiResponse<bool>> VerifyDatabaseIntegrityAsync(CancellationToken ct = default);
}

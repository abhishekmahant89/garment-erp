using GarmentERP.Domain.Common;

namespace GarmentERP.Domain.Entities;

public enum SyncStatus
{
    Pending,
    Syncing,
    Done,
    Failed
}

public class SyncOutbox : BaseEntity
{
    public string AggregateType { get; set; } = string.Empty; // Sale, Customer, StockMovement, Shift
    public Guid AggregateId { get; set; }
    public string EventType { get; set; } = string.Empty; // Created, Updated, Deleted
    public string PayloadJson { get; set; } = string.Empty;
    public SyncStatus Status { get; set; } = SyncStatus.Pending;
    public int RetryCount { get; set; } = 0;
    public string? LastError { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public string? Checksum { get; set; }
}

public class SyncInbox : BaseEntity
{
    public Guid EventId { get; set; }
    public string AggregateType { get; set; } = string.Empty;
    public Guid AggregateId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = string.Empty;
    public SyncStatus Status { get; set; } = SyncStatus.Pending;
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ProcessedAt { get; set; }
}

public class LicenseInfo : BaseEntity
{
    public string LicenseKey { get; set; } = string.Empty;
    public string RegisteredBusinessName { get; set; } = string.Empty;
    public string MachineFingerprint { get; set; } = string.Empty;
    public int AllowedTerminals { get; set; } = 1;
    public DateTime ExpiryDate { get; set; }
    public DateTime LastValidatedAt { get; set; } = DateTime.UtcNow;
    public int GracePeriodDays { get; set; } = 15;
    public bool IsValid => IsActive && DateTime.UtcNow <= ExpiryDate.AddDays(GracePeriodDays);
}

public class ErrorTelemetry : BaseEntity
{
    public string MachineName { get; set; } = Environment.MachineName;
    public string AppVersion { get; set; } = "1.0.0";
    public string ExceptionType { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string StackTrace { get; set; } = string.Empty;
    public string? SyncStatusSummary { get; set; }
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
    public bool ReportedToLicenseServer { get; set; } = false;
    public DateTime? ReportedAt { get; set; }
}

using GarmentERP.Domain.Entities;

namespace GarmentERP.Application.DTOs;

public record SyncItemDto(
    Guid Id,
    string AggregateType,
    Guid AggregateId,
    string EventType,
    string PayloadJson,
    DateTime CreatedAt,
    string? Checksum
);

public record SyncPushRequest(
    string MachineFingerprint,
    string LicenseKey,
    Guid BranchId,
    List<SyncItemDto> Events
);

public record SyncPushResponse(
    bool Success,
    List<Guid> ConfirmedEventIds,
    List<string> Errors
);

public record SyncPullResponse(
    DateTime Timestamp,
    IReadOnlyList<SyncItemDto> Changes
);

public record LicenseValidationRequest(
    string LicenseKey,
    string MachineFingerprint,
    string MachineName,
    string AppVersion
);

public record LicenseValidationResponse(
    bool IsValid,
    string RegisteredBusinessName,
    int AllowedTerminals,
    DateTime ExpiryDate,
    int RemainingGraceDays,
    string Token,
    string Message
);

public record ErrorTelemetryRequest(
    string MachineFingerprint,
    string LicenseKey,
    string AppVersion,
    string ExceptionType,
    string Message,
    string StackTrace,
    string? SyncStatusSummary,
    DateTime OccurredAt
);

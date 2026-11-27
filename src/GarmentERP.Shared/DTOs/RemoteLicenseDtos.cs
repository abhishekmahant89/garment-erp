namespace GarmentERP.Shared.DTOs;

public class RemoteVerifyRequest
{
    public string MachineId { get; set; } = string.Empty;
    public string RestaurantName { get; set; } = "Grand Fashion Store";
    public string ClientName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = "+91 96918 83083";
    public string ClientVersion { get; set; } = "2.1.0";
    public string SessionId { get; set; } = Guid.NewGuid().ToString();
    public int ProcessId { get; set; } = Environment.ProcessId;
    public string AppName { get; set; } = "GarmentERP";
    public string AppPath { get; set; } = string.Empty;
    public string Action { get; set; } = "heartbeat";
    public string RequestedModules { get; set; } = "CORE,GARMENT_ERP";
}

public class RemoteVerifyResponse
{
    public bool Success { get; set; }
    public string MachineId { get; set; } = string.Empty;
    public string Status { get; set; } = "Trial";
    public bool IsAllowed { get; set; } = true;
    public int TrialDaysRemaining { get; set; } = 15;
    public DateTime? TrialExpiresAt { get; set; }
    public string DeveloperMessage { get; set; } = string.Empty;
    public int RunningInstances { get; set; } = 1;
    public string InstallState { get; set; } = "Installed";
    public DateTime ServerUtcTime { get; set; }
    public List<string>? EnabledModules { get; set; }
}

using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GarmentERP.Application.DTOs;
using GarmentERP.Application.Interfaces;
using GarmentERP.Domain.Entities;
using GarmentERP.Infrastructure.Data;
using GarmentERP.Shared.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;

namespace GarmentERP.Infrastructure.SyncAndLicense;

public class LicenseValidator : ILicenseValidator
{
    private readonly GarmentDbContextBase _context;
    private readonly HttpClient _httpClient;

    public const string PrimaryServerUrl = "https://license.abhishekmahant.in";
    public const string FallbackServerUrl = "https://pos-license-server-4xnq.onrender.com";

    public LicenseValidator(GarmentDbContextBase context)
    {
        _context = context;
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(6) };
    }

    public string GetMachineFingerprint()
    {
        try
        {
            string machineGuid = string.Empty;
            if (OperatingSystem.IsWindows())
            {
                using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");
                if (key != null)
                {
                    machineGuid = key.GetValue("MachineGuid")?.ToString() ?? string.Empty;
                }
            }

            string raw = $"{Environment.MachineName}|{Environment.ProcessorCount}|{machineGuid}";
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(raw));
            var hex = BitConverter.ToString(hash).Replace("-", "").ToUpperInvariant();
            return $"MPOS-{hex.Substring(0, 4)}-{hex.Substring(4, 4)}-{hex.Substring(8, 4)}-{hex.Substring(12, 4)}";
        }
        catch
        {
            return $"MPOS-{Environment.MachineName.ToUpperInvariant().PadRight(16, '0')[..16]}";
        }
    }

    public async Task<LicenseValidationResponse> ValidateAsync(string licenseKey, CancellationToken ct = default)
    {
        var machineId = GetMachineFingerprint();

        // 1. Attempt Online Verification with https://license.abhishekmahant.in
        var onlineResult = await VerifyWithServerAsync(machineId, licenseKey, ct);
        if (onlineResult != null)
        {
            return onlineResult;
        }

        // 2. Offline Fallback: Validate via local encrypted license state and 15-day grace period
        var localLicense = await _context.LicenseInfos
            .FirstOrDefaultAsync(l => l.LicenseKey == licenseKey, ct);

        if (localLicense == null)
        {
            // Auto-provision trial license if fresh install
            localLicense = new LicenseInfo
            {
                LicenseKey = licenseKey,
                RegisteredBusinessName = "Grand Fashion Store (Local Trial)",
                AllowedTerminals = 5,
                ExpiryDate = DateTime.UtcNow.AddDays(15),
                GracePeriodDays = 15,
                IsActive = true
            };
            _context.LicenseInfos.Add(localLicense);
            await _context.SaveChangesAsync(ct);
        }

        var isWithinValidity = DateTime.UtcNow <= localLicense.ExpiryDate;
        var isWithinGrace = DateTime.UtcNow <= localLicense.ExpiryDate.AddDays(localLicense.GracePeriodDays);
        var remainingGrace = Math.Max(0, (localLicense.ExpiryDate.AddDays(localLicense.GracePeriodDays) - DateTime.UtcNow).Days);

        if (!localLicense.IsActive)
        {
            return new LicenseValidationResponse(
                IsValid: false,
                RegisteredBusinessName: localLicense.RegisteredBusinessName,
                AllowedTerminals: localLicense.AllowedTerminals,
                ExpiryDate: localLicense.ExpiryDate,
                RemainingGraceDays: 0,
                Token: string.Empty,
                Message: "License has been deactivated by administrator."
            );
        }

        if (!isWithinGrace)
        {
            return new LicenseValidationResponse(
                IsValid: false,
                RegisteredBusinessName: localLicense.RegisteredBusinessName,
                AllowedTerminals: localLicense.AllowedTerminals,
                ExpiryDate: localLicense.ExpiryDate,
                RemainingGraceDays: 0,
                Token: string.Empty,
                Message: "License and 15-day offline grace period have expired. Connect to https://license.abhishekmahant.in to renew."
            );
        }

        var message = isWithinValidity
            ? "License verified (Cached). Online server unreachable, offline billing allowed."
            : $"Operating in Offline Grace Period ({remainingGrace} days remaining). Please connect to internet to renew.";

        return new LicenseValidationResponse(
            IsValid: true,
            RegisteredBusinessName: localLicense.RegisteredBusinessName,
            AllowedTerminals: localLicense.AllowedTerminals,
            ExpiryDate: localLicense.ExpiryDate,
            RemainingGraceDays: remainingGrace,
            Token: Guid.NewGuid().ToString("N"),
            Message: message
        );
    }

    private async Task<LicenseValidationResponse?> VerifyWithServerAsync(string machineId, string licenseKey, CancellationToken ct)
    {
        var endpoints = new[] { PrimaryServerUrl, FallbackServerUrl };

        var req = new RemoteVerifyRequest
        {
            MachineId = machineId,
            RestaurantName = "Grand Fashion Store",
            ClientName = Environment.UserName,
            PhoneNumber = "+91 96918 83083",
            ClientVersion = "2.1.0",
            SessionId = Guid.NewGuid().ToString(),
            ProcessId = Environment.ProcessId,
            AppName = "GarmentERP",
            AppPath = AppDomain.CurrentDomain.BaseDirectory,
            Action = "heartbeat",
            RequestedModules = "CORE,GARMENT_ERP"
        };

        var json = JsonSerializer.Serialize(req);

        foreach (var baseUrl in endpoints)
        {
            try
            {
                using var content = new StringContent(json, Encoding.UTF8, "application/json");
                var url = $"{baseUrl.TrimEnd('/')}/api/license/verify";

                var resp = await _httpClient.PostAsync(url, content, ct);
                if (resp.IsSuccessStatusCode)
                {
                    var respJson = await resp.Content.ReadAsStringAsync(ct);
                    var result = JsonSerializer.Deserialize<RemoteVerifyResponse>(respJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    if (result != null && result.Success)
                    {
                        // Cache remote license response locally
                        var existing = await _context.LicenseInfos.FirstOrDefaultAsync(ct);
                        if (existing != null)
                        {
                            existing.ExpiryDate = result.TrialExpiresAt ?? DateTime.UtcNow.AddDays(result.TrialDaysRemaining);
                            existing.IsActive = result.IsAllowed;
                            await _context.SaveChangesAsync(ct);
                        }

                        var msg = !string.IsNullOrWhiteSpace(result.DeveloperMessage)
                            ? result.DeveloperMessage
                            : $"Online license verified via {baseUrl}. Status: {result.Status} ({result.TrialDaysRemaining} days remaining)";

                        return new LicenseValidationResponse(
                            IsValid: result.IsAllowed,
                            RegisteredBusinessName: "Grand Fashion Flagship",
                            AllowedTerminals: 10,
                            ExpiryDate: result.TrialExpiresAt ?? DateTime.UtcNow.AddDays(result.TrialDaysRemaining),
                            RemainingGraceDays: result.TrialDaysRemaining,
                            Token: Guid.NewGuid().ToString("N"),
                            Message: msg
                        );
                    }
                }
            }
            catch
            {
                // Move to fallback server or offline mode
            }
        }

        return null;
    }

    public async Task ExitSessionAsync(CancellationToken ct = default)
    {
        try
        {
            var body = JsonSerializer.Serialize(new
            {
                machineId = GetMachineFingerprint(),
                action = "exit",
                processId = Environment.ProcessId
            });

            using var content = new StringContent(body, Encoding.UTF8, "application/json");
            await _httpClient.PostAsync($"{PrimaryServerUrl}/api/license/exit", content, ct);
        }
        catch { }
    }

    public bool IsCurrentTerminalLicensed()
    {
        var license = _context.LicenseInfos.FirstOrDefault(l => l.IsActive);
        if (license == null) return false;
        return DateTime.UtcNow <= license.ExpiryDate.AddDays(license.GracePeriodDays);
    }
}

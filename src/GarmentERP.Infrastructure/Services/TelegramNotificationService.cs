using System.Collections.Concurrent;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using GarmentERP.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace GarmentERP.Infrastructure.Services;

public class TelegramNotificationService : ITelegramNotificationService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<TelegramNotificationService>? _logger;

    public const string DefaultBotToken = "9008744051:AAEhTj6Wx0Q-ELk0ig-lgBOg6V7XoccodjA";
    public const string DefaultChatId = "8168115314";

    private string _botToken;
    private string _chatId;

    // In-memory throttling caches to avoid notification flooding
    private readonly ConcurrentDictionary<string, DateTime> _throttles = new();

    public TelegramNotificationService(IConfiguration? config = null, ILogger<TelegramNotificationService>? logger = null)
    {
        _logger = logger;
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };

        var envToken = Environment.GetEnvironmentVariable("TELEGRAM_BOT_TOKEN");
        var envChat = Environment.GetEnvironmentVariable("TELEGRAM_CHAT_ID");

        var configToken = config?["Telegram:BotToken"];
        var configChat = config?["Telegram:ChatId"];

        _botToken = !string.IsNullOrWhiteSpace(envToken) ? envToken.Trim()
            : !string.IsNullOrWhiteSpace(configToken) ? configToken.Trim()
            : DefaultBotToken;

        _chatId = !string.IsNullOrWhiteSpace(envChat) ? envChat.Trim()
            : !string.IsNullOrWhiteSpace(configChat) ? configChat.Trim()
            : DefaultChatId;
    }

    public string GetCurrentBotToken() => _botToken;
    public string GetCurrentChatId() => _chatId;

    public void UpdateCredentials(string botToken, string chatId)
    {
        if (!string.IsNullOrWhiteSpace(botToken)) _botToken = botToken.Trim();
        if (!string.IsNullOrWhiteSpace(chatId)) _chatId = chatId.Trim();
    }

    public async Task<(bool Success, string Message)> SendRawMessageAsync(string htmlMessage, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_botToken) || string.IsNullOrWhiteSpace(_chatId))
        {
            return (false, "Bot Token and Chat ID are required.");
        }

        try
        {
            var url = $"https://api.telegram.org/bot{_botToken}/sendMessage";
            var payload = new
            {
                chat_id = _chatId,
                text = htmlMessage,
                parse_mode = "HTML",
                disable_web_page_preview = true
            };

            var json = JsonSerializer.Serialize(payload);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(url, content, ct);
            var responseBody = await response.Content.ReadAsStringAsync(ct);

            if (response.IsSuccessStatusCode)
            {
                return (true, "Telegram notification sent successfully.");
            }

            _logger?.LogWarning("Telegram API returned non-success: {StatusCode} {Body}", response.StatusCode, responseBody);
            return (false, $"Telegram API error ({response.StatusCode}): {responseBody}");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to send message to Telegram");
            return (false, $"Exception sending to Telegram: {ex.Message}");
        }
    }

    public async Task<(bool Success, string Message)> TestMessageAsync(string? botToken = null, string? chatId = null, CancellationToken ct = default)
    {
        var token = !string.IsNullOrWhiteSpace(botToken) ? botToken.Trim() : _botToken;
        var chat = !string.IsNullOrWhiteSpace(chatId) ? chatId.Trim() : _chatId;

        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(chat))
        {
            return (false, "Bot Token and Chat ID are not configured.");
        }

        var istNow = DateTime.UtcNow.AddHours(5).AddMinutes(30);
        var msg = new StringBuilder();
        msg.AppendLine("👗 <b>GARMENT STORE POS &amp; RETAIL ERP ALERT</b>");
        msg.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━");
        msg.AppendLine("✅ <b>Status:</b> Telegram Bot Connected Successfully!");
        msg.AppendLine("🔐 <b>License Server:</b> https://license.abhishekmahant.in");
        msg.AppendLine($"⏰ <b>Time:</b> {istNow:dd-MMM-yyyy hh:mm:ss tt} IST");
        msg.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━");
        msg.AppendLine("Aapko live billing, cashier shift closing, low stock aur data sync errors ke real-time alerts yahan milenge.");

        var oldToken = _botToken;
        var oldChat = _chatId;
        try
        {
            _botToken = token;
            _chatId = chat;
            return await SendRawMessageAsync(msg.ToString(), ct);
        }
        finally
        {
            _botToken = oldToken;
            _chatId = oldChat;
        }
    }

    public async Task NotifyAppStartedAsync(string storeName, string terminalName, string cashierName, string machineId, CancellationToken ct = default)
    {
        try
        {
            // Throttle app-start alert to 1 per 2 minutes
            var now = DateTime.UtcNow;
            if (_throttles.TryGetValue("app_start", out var lastTime) && (now - lastTime).TotalMinutes < 2) return;
            _throttles["app_start"] = now;

            var istNow = DateTime.UtcNow.AddHours(5).AddMinutes(30);
            var msg = new StringBuilder();
            msg.AppendLine("🟢 <b>GARMENT ERP POS LAUNCHED</b>");
            msg.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━");
            msg.AppendLine($"🏢 <b>Store:</b> {storeName}");
            msg.AppendLine($"💻 <b>Counter:</b> {terminalName}");
            msg.AppendLine($"👤 <b>Cashier:</b> {cashierName}");
            msg.AppendLine($"🆔 <b>Machine ID:</b> <code>{machineId}</code>");
            msg.AppendLine($"⏰ <b>Time:</b> {istNow:dd-MMM-yyyy hh:mm:ss tt} IST");
            msg.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━");
            msg.AppendLine("Status: Counter is ONLINE and ready for scanning.");

            await SendRawMessageAsync(msg.ToString(), ct);
        }
        catch { }
    }

    public async Task NotifySaleCompletedAsync(string invoiceNumber, decimal grandTotal, int itemsCount, string paymentMode, string customerName, CancellationToken ct = default)
    {
        try
        {
            var istNow = DateTime.UtcNow.AddHours(5).AddMinutes(30);
            var msg = new StringBuilder();
            msg.AppendLine("🧾 <b>NEW BILL COMPLETED!</b>");
            msg.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━");
            msg.AppendLine($"📄 <b>Invoice:</b> <code>{invoiceNumber}</code>");
            msg.AppendLine($"💰 <b>Grand Total:</b> <b>₹{grandTotal:N2}</b>");
            msg.AppendLine($"🛍️ <b>Items:</b> {itemsCount} pc(s)");
            msg.AppendLine($"💳 <b>Payment:</b> {paymentMode}");
            msg.AppendLine($"👤 <b>Customer:</b> {customerName}");
            msg.AppendLine($"⏰ <b>Time:</b> {istNow:dd-MMM-yyyy hh:mm:ss tt} IST");
            msg.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━");

            await SendRawMessageAsync(msg.ToString(), ct);
        }
        catch { }
    }

    public async Task NotifyShiftClosedAsync(string cashierName, decimal totalSales, decimal cashCollected, decimal upiCollected, int billCount, CancellationToken ct = default)
    {
        try
        {
            var istNow = DateTime.UtcNow.AddHours(5).AddMinutes(30);
            var msg = new StringBuilder();
            msg.AppendLine("📊 <b>CASHIER SHIFT CLOSED (Z-REPORT)</b>");
            msg.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━");
            msg.AppendLine($"👤 <b>Cashier:</b> {cashierName}");
            msg.AppendLine($"🧾 <b>Total Bills:</b> {billCount}");
            msg.AppendLine($"💰 <b>Total Sales:</b> <b>₹{totalSales:N2}</b>");
            msg.AppendLine($"💵 <b>Cash in Drawer:</b> ₹{cashCollected:N2}");
            msg.AppendLine($"📲 <b>UPI / Digital:</b> ₹{upiCollected:N2}");
            msg.AppendLine($"⏰ <b>Closed At:</b> {istNow:dd-MMM-yyyy hh:mm:ss tt} IST");
            msg.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━");
            msg.AppendLine("Cashier drawer closed and shift reconciled.");

            await SendRawMessageAsync(msg.ToString(), ct);
        }
        catch { }
    }

    public async Task NotifySyncErrorAsync(string syncType, string status, string errorDetails, CancellationToken ct = default)
    {
        try
        {
            var now = DateTime.UtcNow;
            if (_throttles.TryGetValue("sync_error", out var lastTime) && (now - lastTime).TotalMinutes < 5) return;
            _throttles["sync_error"] = now;

            var istNow = DateTime.UtcNow.AddHours(5).AddMinutes(30);
            var msg = new StringBuilder();
            msg.AppendLine("🚨 <b>POS DATA SYNC ERROR ALERT!</b>");
            msg.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━");
            msg.AppendLine($"🔄 <b>Action:</b> {syncType}");
            msg.AppendLine($"⚠️ <b>Status:</b> {status}");
            msg.AppendLine($"❌ <b>Error:</b> <i>{errorDetails}</i>");
            msg.AppendLine($"⏰ <b>Time:</b> {istNow:dd-MMM-yyyy hh:mm:ss tt} IST");
            msg.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━");
            msg.AppendLine("<i>Terminal continues offline billing. Auto-retry in progress.</i>");

            await SendRawMessageAsync(msg.ToString(), ct);
        }
        catch { }
    }

    public async Task NotifyExceptionTelemetryAsync(string exceptionType, string message, string sourceModule, CancellationToken ct = default)
    {
        try
        {
            var now = DateTime.UtcNow;
            var key = $"err_{sourceModule}";
            if (_throttles.TryGetValue(key, out var lastTime) && (now - lastTime).TotalMinutes < 3) return;
            _throttles[key] = now;

            var istNow = DateTime.UtcNow.AddHours(5).AddMinutes(30);
            var msg = new StringBuilder();
            msg.AppendLine("⚠️ <b>POS TELEMETRY CRASH ALERT</b>");
            msg.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━");
            msg.AppendLine($"📦 <b>Module:</b> {sourceModule}");
            msg.AppendLine($"🔥 <b>Type:</b> <code>{exceptionType}</code>");
            msg.AppendLine($"📝 <b>Message:</b> {message}");
            msg.AppendLine($"⏰ <b>Time:</b> {istNow:dd-MMM-yyyy hh:mm:ss tt} IST");
            msg.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━");
            msg.AppendLine("Logged to Central License Server: https://license.abhishekmahant.in");

            await SendRawMessageAsync(msg.ToString(), ct);
        }
        catch { }
    }
}

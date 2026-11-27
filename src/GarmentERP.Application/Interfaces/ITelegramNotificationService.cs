namespace GarmentERP.Application.Interfaces;

public interface ITelegramNotificationService
{
    Task<(bool Success, string Message)> SendRawMessageAsync(string htmlMessage, CancellationToken ct = default);
    Task<(bool Success, string Message)> TestMessageAsync(string? botToken = null, string? chatId = null, CancellationToken ct = default);
    Task NotifyAppStartedAsync(string storeName, string terminalName, string cashierName, string machineId, CancellationToken ct = default);
    Task NotifySaleCompletedAsync(string invoiceNumber, decimal grandTotal, int itemsCount, string paymentMode, string customerName, CancellationToken ct = default);
    Task NotifyShiftClosedAsync(string cashierName, decimal totalSales, decimal cashCollected, decimal upiCollected, int billCount, CancellationToken ct = default);
    Task NotifySyncErrorAsync(string syncType, string status, string errorDetails, CancellationToken ct = default);
    Task NotifyExceptionTelemetryAsync(string exceptionType, string message, string sourceModule, CancellationToken ct = default);
    string GetCurrentBotToken();
    string GetCurrentChatId();
    void UpdateCredentials(string botToken, string chatId);
}

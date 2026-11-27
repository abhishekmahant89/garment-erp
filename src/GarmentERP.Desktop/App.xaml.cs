using System.Windows;
using GarmentERP.Application.Interfaces;
using GarmentERP.Desktop.ViewModels;
using GarmentERP.Domain.Services;
using GarmentERP.Infrastructure.Data;
using GarmentERP.Infrastructure.Services;
using GarmentERP.Infrastructure.SyncAndLicense;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using GarmentERP.Reporting.Invoices;
using GarmentERP.Reporting.Thermal;

namespace GarmentERP.Desktop;

public partial class App : System.Windows.Application
{
    private readonly IHost _host;
    public IServiceProvider Services => _host.Services;

    public App()
    {
        _host = Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                // Local SQLite database for 100% offline billing counter
                services.AddDbContext<GarmentDbContextBase, GarmentOfflineDbContext>(options =>
                    options.UseSqlite("Data Source=garment_terminal_offline.db"));

                // HTTP Client for LAN API / Cloud Sync
                services.AddHttpClient<ISyncEngine, SyncEngine>(client =>
                {
                    client.BaseAddress = new Uri("http://localhost:5000");
                    client.Timeout = TimeSpan.FromSeconds(10);
                });

                // Domain & Services
                services.AddSingleton<IVariantMatrixGenerator, VariantMatrixGenerator>();
                services.AddSingleton<IGstCalculator, GstCalculator>();
                services.AddScoped<IProductCatalogService, ProductCatalogService>();
                services.AddScoped<IPosBillingService, PosBillingService>();
                services.AddScoped<ILicenseValidator, LicenseValidator>();
                services.AddScoped<IErrorTelemetryReporter, ErrorTelemetryReporter>();
                services.AddScoped<ISyncEngine, SyncEngine>();
                services.AddScoped<IPaymentService, PaymentService>();
                services.AddScoped<ILoyaltyService, LoyaltyService>();
                services.AddScoped<IReturnExchangeService, ReturnExchangeService>();
                services.AddScoped<IShiftReportingService, ShiftReportingService>();
                services.AddScoped<IPurchaseService, PurchaseService>();
                services.AddScoped<IStockTransferService, StockTransferService>();
                services.AddScoped<IPromotionEngine, PromotionEngine>();
                services.AddScoped<IExpenseService, ExpenseService>();
                services.AddScoped<IEnterpriseReportService, EnterpriseReportService>();
                services.AddScoped<IDashboardService, DashboardService>();
                services.AddScoped<IGstReportService, GstReportService>();
                services.AddScoped<IAccountingService, AccountingService>();
                services.AddScoped<IStaffAuthService, StaffAuthService>();
                services.AddScoped<IBackupService, BackupService>();
                services.AddSingleton<IReceiptPrinterService, EscPosReceiptBuilder>();
                services.AddSingleton<IA4InvoiceGenerator, A4InvoiceGenerator>();
                services.AddSingleton<ITelegramNotificationService, TelegramNotificationService>();

                // ViewModels & Views
                services.AddTransient<PosMainViewModel>();
                services.AddTransient<MainWindow>();
            })
            .Build();

        // Setup global error reporting to license server
        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            if (e.ExceptionObject is Exception ex)
            {
                using var scope = _host.Services.CreateScope();
                var reporter = scope.ServiceProvider.GetRequiredService<IErrorTelemetryReporter>();
                reporter.LogExceptionAsync(ex, "AppDomain Unhandled Crash").GetAwaiter().GetResult();
            }
        };

        DispatcherUnhandledException += (s, e) =>
        {
            using var scope = _host.Services.CreateScope();
            var reporter = scope.ServiceProvider.GetRequiredService<IErrorTelemetryReporter>();
            reporter.LogExceptionAsync(e.Exception, "WPF Dispatcher Crash").GetAwaiter().GetResult();
            e.Handled = true; // Prevent abrupt crash when possible
            var detail = e.Exception.InnerException != null 
                ? $"{e.Exception.Message}\n\nDetails: {e.Exception.InnerException.Message}" 
                : e.Exception.Message;
            MessageBox.Show($"Unexpected error occurred:\n{detail}\n\nError has been logged to the central license telemetry.", "POS Error Alert", MessageBoxButton.OK, MessageBoxImage.Warning);
        };
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        await _host.StartAsync();

        using (var scope = _host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GarmentDbContextBase>();
            await db.Database.EnsureCreatedAsync();
        }

        try
        {
            var telegram = _host.Services.GetRequiredService<ITelegramNotificationService>();
            var license = _host.Services.GetRequiredService<ILicenseValidator>();
            _ = telegram.NotifyAppStartedAsync("Grand Fashion Store", "Counter 01", "Rahul Verma", license.GetMachineFingerprint());
        }
        catch { }

        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        try
        {
            if (_host.Services.GetService<ILicenseValidator>() is LicenseValidator lv)
            {
                await lv.ExitSessionAsync();
            }
        }
        catch { }

        await _host.StopAsync();
        _host.Dispose();
        base.OnExit(e);
    }
}

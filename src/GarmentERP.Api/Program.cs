using GarmentERP.Api.Hubs;
using GarmentERP.Application.Interfaces;
using GarmentERP.Domain.Services;
using GarmentERP.Infrastructure.Data;
using GarmentERP.Infrastructure.Services;
using GarmentERP.Infrastructure.SyncAndLicense;
using GarmentERP.Reporting.Invoices;
using GarmentERP.Reporting.Thermal;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add Controllers & SignalR
builder.Services.AddControllers();
builder.Services.AddSignalR();
builder.Services.AddHttpClient();

// Determine Database Provider: PostgreSQL if connection string provided, else SQLite
var postgresConn = builder.Configuration.GetConnectionString("PostgresConnection");
if (!string.IsNullOrEmpty(postgresConn))
{
    var normalizedConn = NormalizePostgresConnectionString(postgresConn);
    builder.Services.AddDbContext<GarmentDbContextBase, GarmentServerDbContext>(options =>
        options.UseNpgsql(normalizedConn));
}
else
{
    var sqliteConn = builder.Configuration.GetConnectionString("SqliteConnection") ?? "Data Source=garment_store.db";
    builder.Services.AddDbContext<GarmentDbContextBase, GarmentOfflineDbContext>(options =>
        options.UseSqlite(sqliteConn));
}


// Domain & Application Services
builder.Services.AddSingleton<IVariantMatrixGenerator, VariantMatrixGenerator>();
builder.Services.AddSingleton<IGstCalculator, GstCalculator>();
builder.Services.AddScoped<IProductCatalogService, ProductCatalogService>();
builder.Services.AddScoped<IPosBillingService, PosBillingService>();
builder.Services.AddScoped<ILicenseValidator, LicenseValidator>();
builder.Services.AddScoped<IErrorTelemetryReporter, ErrorTelemetryReporter>();
builder.Services.AddScoped<ISyncEngine, SyncEngine>();
builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.AddScoped<ILoyaltyService, LoyaltyService>();
builder.Services.AddScoped<IReturnExchangeService, ReturnExchangeService>();
builder.Services.AddScoped<IShiftReportingService, ShiftReportingService>();
builder.Services.AddScoped<IPurchaseService, PurchaseService>();
builder.Services.AddScoped<IStockTransferService, StockTransferService>();
builder.Services.AddScoped<IPromotionEngine, PromotionEngine>();
builder.Services.AddScoped<IExpenseService, ExpenseService>();
builder.Services.AddScoped<IEnterpriseReportService, EnterpriseReportService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<IGstReportService, GstReportService>();
builder.Services.AddScoped<IAccountingService, AccountingService>();
builder.Services.AddScoped<IStaffAuthService, StaffAuthService>();
builder.Services.AddScoped<IBackupService, BackupService>();
builder.Services.AddSingleton<IReceiptPrinterService, EscPosReceiptBuilder>();
builder.Services.AddSingleton<IA4InvoiceGenerator, A4InvoiceGenerator>();

// CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(p => p.AllowAnyHeader().AllowAnyMethod().AllowAnyOrigin());
});

var app = builder.Build();

// Ensure DB Created and Seeded on Startup
using (var scope = app.Services.CreateScope())
{
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<GarmentDbContextBase>();
        await DatabaseSeeder.SeedInitialDataAsync(db);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Startup Warning] Database seeding deferred or error: {ex.Message}");
    }
}

app.UseCors();
app.UseRouting();

app.MapControllers();
app.MapHub<SyncHub>("/hubs/sync");

app.MapGet("/", () => Results.Ok(new
{
    Application = "Garment Retail ERP & POS Web API",
    Version = "1.0.0",
    Framework = ".NET 10",
    Status = "Healthy",
    Timestamp = DateTime.UtcNow
}));

app.Run();

static string NormalizePostgresConnectionString(string connectionString)
{
    if (string.IsNullOrWhiteSpace(connectionString)) return connectionString;
    if (!connectionString.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) &&
        !connectionString.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
    {
        return connectionString;
    }

    try
    {
        var uri = new Uri(connectionString);
        var userInfo = uri.UserInfo.Split(':');
        var username = userInfo.Length > 0 ? Uri.UnescapeDataString(userInfo[0]) : string.Empty;
        var password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : string.Empty;
        var database = uri.AbsolutePath.TrimStart('/');
        var host = uri.Host;
        var port = uri.Port > 0 ? uri.Port : 5432;

        var builder = new Npgsql.NpgsqlConnectionStringBuilder
        {
            Host = host,
            Port = port,
            Database = database,
            Username = username,
            Password = password,
            SslMode = Npgsql.SslMode.Prefer,
            TrustServerCertificate = true
        };

        return builder.ConnectionString;
    }
    catch
    {
        return connectionString;
    }
}

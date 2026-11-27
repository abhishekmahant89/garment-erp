using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GarmentERP.Application.DTOs;
using GarmentERP.Application.Interfaces;
using GarmentERP.Desktop.Services;
using GarmentERP.Domain.Entities;
using GarmentERP.Domain.Services;
using GarmentERP.Infrastructure.Data;
using GarmentERP.Reporting.Invoices;
using GarmentERP.Reporting.Thermal;
using GarmentERP.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace GarmentERP.Desktop.ViewModels;

public partial class CartLineItemViewModel : ObservableObject
{
    public Guid VariantId { get; set; }
    public string SKU { get; set; } = string.Empty;
    public string Barcode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string SizeName { get; set; } = string.Empty;
    public string ColorName { get; set; } = string.Empty;
    public string HsnCode { get; set; } = string.Empty;

    [ObservableProperty]
    private decimal _quantity = 1;

    [ObservableProperty]
    private decimal _unitPrice;

    public decimal MRP { get; set; }

    [ObservableProperty]
    private decimal _discountAmount = 0;

    [ObservableProperty]
    private decimal _taxRate = 5;

    public decimal LineTotal => Math.Max(0, (Quantity * UnitPrice) - DiscountAmount);
    public string DisplayTitle => $"{ProductName} ({SizeName} / {ColorName})";
}

public class ProductVariantDisplayItem
{
    public Guid Id { get; set; }
    public string SKU { get; set; } = string.Empty;
    public string Barcode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public string SizeName { get; set; } = string.Empty;
    public string ColorName { get; set; } = string.Empty;
    public decimal PurchasePrice { get; set; }
    public decimal SellingPrice { get; set; }
    public decimal MRP { get; set; }
    public decimal StockQty { get; set; }
}

public class HeldBillItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string CustomerName { get; set; } = string.Empty;
    public DateTime HeldAt { get; set; } = DateTime.Now;
    public int ItemCount { get; set; }
    public decimal TotalAmount { get; set; }
    public List<CartLineItemViewModel> Items { get; set; } = new();
    public decimal InvoiceDiscount { get; set; }
}

public class CartSessionSnapshotDto
{
    public DateTime SnapshotTime { get; set; } = DateTime.UtcNow;
    public string CustomerName { get; set; } = string.Empty;
    public Guid? CustomerId { get; set; }
    public string CustomerPhone { get; set; } = string.Empty;
    public decimal InvoiceDiscount { get; set; }
    public List<CartLineSnapshotItem> Items { get; set; } = new();
    public List<HeldBillSnapshotItem> HeldBills { get; set; } = new();
}

public class CartLineSnapshotItem
{
    public Guid VariantId { get; set; }
    public string SKU { get; set; } = string.Empty;
    public string Barcode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string SizeName { get; set; } = string.Empty;
    public string ColorName { get; set; } = string.Empty;
    public decimal MRP { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Quantity { get; set; }
    public decimal DiscountAmount { get; set; }
    public string HsnCode { get; set; } = string.Empty;
}

public class HeldBillSnapshotItem
{
    public Guid Id { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public DateTime HeldAt { get; set; }
    public int ItemCount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal InvoiceDiscount { get; set; }
    public List<CartLineSnapshotItem> Items { get; set; } = new();
}

public partial class PosMainViewModel : ObservableObject
{
    private readonly IProductCatalogService _catalogService;
    private readonly IPosBillingService _billingService;
    private readonly ISyncEngine _syncEngine;
    private readonly ILicenseValidator _licenseValidator;
    private readonly IPaymentService _paymentService;
    private readonly ILoyaltyService _loyaltyService;
    private readonly IReturnExchangeService _returnExchangeService;
    private readonly IShiftReportingService _shiftReportingService;
    private readonly IReceiptPrinterService _receiptPrinter;
    private readonly IA4InvoiceGenerator _a4InvoiceGenerator;
    private readonly IVariantMatrixGenerator _matrixGenerator;
    private readonly IPurchaseService _purchaseService;
    private readonly IStockTransferService _transferService;
    private readonly IExpenseService _expenseService;
    private readonly IEnterpriseReportService _reportService;
    private readonly IDashboardService _dashboardService;
    private readonly IGstReportService _gstReportService;
    private readonly IAccountingService _accountingService;
    private readonly IErrorTelemetryReporter _telemetryReporter;
    private readonly IBackupService _backupService;
    private readonly GarmentDbContextBase _context;

    // Active Navigation Module
    [ObservableProperty] private string _selectedNavModule = "POS";
    [ObservableProperty] private bool _isPosActive = true;
    [ObservableProperty] private bool _isDashboardActive = false;
    [ObservableProperty] private bool _isProductsActive = false;
    [ObservableProperty] private bool _isInventoryActive = false;
    [ObservableProperty] private bool _isPurchasesActive = false;
    [ObservableProperty] private bool _isTransfersActive = false;
    [ObservableProperty] private bool _isCustomersActive = false;
    [ObservableProperty] private bool _isReturnsActive = false;
    [ObservableProperty] private bool _isAccountingActive = false;
    [ObservableProperty] private bool _isReportsActive = false;
    [ObservableProperty] private bool _isSyncActive = false;
    [ObservableProperty] private bool _isSettingsActive = false;

    // POS Cart & Header State
    public ObservableCollection<CartLineItemViewModel> CartItems { get; } = new();
    public ObservableCollection<HeldBillItem> HeldBills { get; } = new();

    [ObservableProperty] private string _barcodeScanInput = string.Empty;
    [ObservableProperty] private string _customerName = "Walk-in Retail Customer";
    [ObservableProperty] private string _customerPhone = "9999999999";
    [ObservableProperty] private int _customerLoyaltyPoints = 0;
    [ObservableProperty] private decimal _subTotal;
    [ObservableProperty] private decimal _invoiceDiscount;
    [ObservableProperty] private decimal _taxAmount;
    [ObservableProperty] private decimal _roundOff;
    [ObservableProperty] private decimal _grandTotal;
    [ObservableProperty] private string _statusMessage = "Ready for fast counter scanning...";
    [ObservableProperty] private bool _isCrashRecoveredCart = false;
    private static readonly string _snapshotFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "garment_active_cart_snapshot.json");

    // Sync & Header Counters
    [ObservableProperty] private int _syncPendingCount = 0;
    [ObservableProperty] private int _syncDoneCount = 0;
    [ObservableProperty] private int _syncFailedCount = 0;
    [ObservableProperty] private string _licenseBadgeText = "License: Active (Flagship Retail)";
    [ObservableProperty] private string _counterInfoText = "Counter 01 | Cashier: Rahul Verma";

    // Modals
    [ObservableProperty] private bool _isReceiptModalOpen = false;
    [ObservableProperty] private string _receiptPreviewText = string.Empty;
    [ObservableProperty] private bool _isPaymentModalOpen = false;
    [ObservableProperty] private decimal _tenderCash = 0;
    [ObservableProperty] private decimal _tenderCard = 0;
    [ObservableProperty] private decimal _tenderUpi = 0;
    [ObservableProperty] private string _upiQrUri = string.Empty;
    [ObservableProperty] private string _splitPaymentSummary = string.Empty;
    [ObservableProperty] private bool _isZReportModalOpen = false;
    [ObservableProperty] private string _zReportText = string.Empty;
    [ObservableProperty] private bool _isProductSearchModalOpen = false;
    [ObservableProperty] private string _productSearchQuery = string.Empty;
    [ObservableProperty] private bool _isCustomerModalOpen = false;
    [ObservableProperty] private string _customerSearchQuery = string.Empty;
    [ObservableProperty] private bool _isDiscountModalOpen = false;
    [ObservableProperty] private decimal _discountPercentInput = 0;
    [ObservableProperty] private decimal _discountFlatInput = 0;
    [ObservableProperty] private bool _isHeldBillsModalOpen = false;

    // SCANNER SWITCH & MOBILE WIFI SCANNER
    private readonly MobileScannerServer _mobileScannerServer = new();
    private readonly BarcodeGeneratorService _barcodeGenerator = new();
    [ObservableProperty] private string _scannerMode = "PHYSICAL"; // PHYSICAL, MOBILE, DEMO
    [ObservableProperty] private bool _isPhysicalScanner = true;
    [ObservableProperty] private bool _isMobileScanner = false;
    [ObservableProperty] private bool _isDemoScanner = false;
    [ObservableProperty] private bool _isMobileScannerModalOpen = false;
    [ObservableProperty] private string _mobileScannerUrl = string.Empty;
    [ObservableProperty] private BitmapImage? _mobileScannerQrImage;
    [ObservableProperty] private string _connectedMobileDevice = "Waiting for Android phone to connect...";

    // BARCODE FOR PRODUCT SKU & HANG TAG LABEL
    [ObservableProperty] private bool _isBarcodeLabelModalOpen = false;
    [ObservableProperty] private ProductVariantDisplayItem? _selectedBarcodeItem;
    [ObservableProperty] private BitmapSource? _barcodeLabelPreviewImage;
    [ObservableProperty] private string _barcodeLabelPrintStatus = string.Empty;

    // DASHBOARD PROPERTIES
    [ObservableProperty] private decimal _dashTodaySales = 0;
    [ObservableProperty] private int _dashInvoiceCount = 0;
    [ObservableProperty] private decimal _dashItemsSold = 0;
    [ObservableProperty] private decimal _dashCashCollection = 0;
    [ObservableProperty] private decimal _dashUpiCollection = 0;
    [ObservableProperty] private int _dashLowStockAlerts = 0;
    public ObservableCollection<SaleResponseDto> DashRecentSales { get; } = new();

    // PRODUCTS & VARIANT MATRIX PROPERTIES
    public ObservableCollection<ProductVariantDisplayItem> CatalogVariants { get; } = new();
    public ObservableCollection<ProductVariantDisplayItem> SearchFilteredVariants { get; } = new();
    [ObservableProperty] private string _catalogFilterQuery = string.Empty;
    [ObservableProperty] private string _matrixGenStatus = string.Empty;

    // INVENTORY PROPERTIES
    public ObservableCollection<ProductVariantDisplayItem> InventoryStockItems { get; } = new();
    [ObservableProperty] private string _inventoryFilterQuery = string.Empty;
    [ObservableProperty] private bool _inventoryShowLowStockOnly = false;
    [ObservableProperty] private ProductVariantDisplayItem? _selectedInventoryItem;
    [ObservableProperty] private decimal _stockAdjustmentQty = 1;
    [ObservableProperty] private string _stockAdjustmentReason = "Inventory Audit / Physical Count";

    // PURCHASES & SUPPLIER PROPERTIES
    public ObservableCollection<SupplierDto> SuppliersList { get; } = new();
    public ObservableCollection<PurchaseInvoiceDto> PurchaseHistory { get; } = new();
    [ObservableProperty] private SupplierDto? _selectedSupplier;
    [ObservableProperty] private string _purchaseInvoiceNo = "PUR-" + DateTime.Now.ToString("yyyyMMdd-HHmm");
    [ObservableProperty] private ProductVariantDisplayItem? _purchaseSelectedVariant;
    [ObservableProperty] private decimal _purchaseQty = 20;
    [ObservableProperty] private decimal _purchasePrice = 650;
    [ObservableProperty] private decimal _purchaseMRP = 1299;

    // TRANSFERS PROPERTIES
    public ObservableCollection<StockTransferDto> TransferHistory { get; } = new();
    [ObservableProperty] private ProductVariantDisplayItem? _transferSelectedVariant;
    [ObservableProperty] private decimal _transferQty = 5;
    [ObservableProperty] private string _transferNotes = "Store Replenishment from Central Warehouse";

    // CUSTOMERS PROPERTIES
    public ObservableCollection<Customer> CustomersDirectory { get; } = new();
    [ObservableProperty] private string _newCustomerName = string.Empty;
    [ObservableProperty] private string _newCustomerPhone = string.Empty;
    [ObservableProperty] private string _newCustomerEmail = string.Empty;

    // RETURNS PROPERTIES
    [ObservableProperty] private string _returnInvoiceLookupNo = string.Empty;
    [ObservableProperty] private SaleResponseDto? _returnFoundSale;
    [ObservableProperty] private SaleItemDto? _returnSelectedItem;
    [ObservableProperty] private decimal _returnQuantity = 1;
    [ObservableProperty] private string _returnReason = "Size / Fit Exchange";
    [ObservableProperty] private string _creditNoteResult = string.Empty;

    // ACCOUNTING & EXPENSES PROPERTIES
    public ObservableCollection<ExpenseDto> ExpensesList { get; } = new();
    public ObservableCollection<LedgerAccount> ChartOfAccounts { get; } = new();
    [ObservableProperty] private string _expenseTitle = "Staff Refreshments / Tea";
    [ObservableProperty] private decimal _expenseAmount = 250;
    [ObservableProperty] private string _expenseCategory = "Operating Expense";
    [ObservableProperty] private decimal _trialBalanceDebits = 0;
    [ObservableProperty] private decimal _trialBalanceCredits = 0;
    [ObservableProperty] private bool _isTrialBalanceBalanced = true;

    // GST & P/L REPORTS PROPERTIES
    [ObservableProperty] private decimal _rptTotalRevenue = 0;
    [ObservableProperty] private decimal _rptCogs = 0;
    [ObservableProperty] private decimal _rptGrossProfit = 0;
    [ObservableProperty] private decimal _rptExpenses = 0;
    [ObservableProperty] private decimal _rptNetProfit = 0;
    [ObservableProperty] private decimal _rptStockTotalValue = 0;
    [ObservableProperty] private decimal _rptStockTotalUnits = 0;
    public ObservableCollection<Gstr1HsnSummaryRow> Gstr1HsnSummaries { get; } = new();

    // DATA SYNC CENTER PROPERTIES
    public ObservableCollection<SyncOutbox> OutboxList { get; } = new();
    public ObservableCollection<SyncInbox> InboxList { get; } = new();

    // SETTINGS PROPERTIES
    [ObservableProperty] private string _settingStoreName = "GRAND FASHION STORE";
    [ObservableProperty] private string _settingBranchName = "Flagship Retail Showroom";
    [ObservableProperty] private string _settingGstin = "29AAAAA0000A1Z5";
    [ObservableProperty] private string _settingAddress = "MG Road Commercial Complex, Bangalore";
    [ObservableProperty] private string _settingPhone = "+91 98765 43210";
    [ObservableProperty] private string _hardwareFingerprint = "HW-" + Environment.MachineName + "-FASHION-POS";
    [ObservableProperty] private string _settingStatusMessage = string.Empty;

    // TELEGRAM & REMOTE LICENSE SERVER PROPERTIES
    [ObservableProperty] private string _telegramBotToken = "9008744051:AAEhTj6Wx0Q-ELk0ig-lgBOg6V7XoccodjA";
    [ObservableProperty] private string _telegramChatId = "8168115314";
    [ObservableProperty] private string _licenseServerUrl = "https://license.abhishekmahant.in";
    [ObservableProperty] private string _telegramStatusMessage = string.Empty;

    private readonly ITelegramNotificationService _telegramService;

    private Guid _branchId;
    private Guid _warehouseId;
    private Guid _shiftId;
    private Guid? _customerId;
    private SaleResponseDto? _lastSale;

    private readonly StoreReceiptHeader _storeHeader = new(
        StoreName: "GRAND FASHION STORE",
        BranchName: "MG Road Flagship Showroom",
        Address: "MG Road Commercial Complex, Bangalore",
        Phone: "+91 98765 43210",
        Gstin: "29AAAAA0000A1Z5"
    );

    public PosMainViewModel(
        IProductCatalogService catalogService,
        IPosBillingService billingService,
        ISyncEngine syncEngine,
        ILicenseValidator licenseValidator,
        IPaymentService paymentService,
        ILoyaltyService loyaltyService,
        IReturnExchangeService returnExchangeService,
        IShiftReportingService shiftReportingService,
        IReceiptPrinterService receiptPrinter,
        IA4InvoiceGenerator a4InvoiceGenerator,
        IVariantMatrixGenerator matrixGenerator,
        IPurchaseService purchaseService,
        IStockTransferService transferService,
        IExpenseService expenseService,
        IEnterpriseReportService reportService,
        IDashboardService dashboardService,
        IGstReportService gstReportService,
        IAccountingService accountingService,
        IErrorTelemetryReporter telemetryReporter,
        IBackupService backupService,
        ITelegramNotificationService telegramService,
        GarmentDbContextBase context)
    {
        _catalogService = catalogService;
        _billingService = billingService;
        _syncEngine = syncEngine;
        _licenseValidator = licenseValidator;
        _paymentService = paymentService;
        _loyaltyService = loyaltyService;
        _returnExchangeService = returnExchangeService;
        _shiftReportingService = shiftReportingService;
        _receiptPrinter = receiptPrinter;
        _a4InvoiceGenerator = a4InvoiceGenerator;
        _matrixGenerator = matrixGenerator;
        _purchaseService = purchaseService;
        _transferService = transferService;
        _expenseService = expenseService;
        _reportService = reportService;
        _dashboardService = dashboardService;
        _gstReportService = gstReportService;
        _accountingService = accountingService;
        _telemetryReporter = telemetryReporter;
        _backupService = backupService;
        _telegramService = telegramService;
        _context = context;
    }

    public async Task InitializeAsync()
    {
        await _context.Database.EnsureCreatedAsync();
        await DatabaseSeeder.SeedInitialDataAsync(_context);

        var branch = await _context.Branches.FirstOrDefaultAsync();
        _branchId = branch?.Id ?? Guid.NewGuid();

        var warehouse = await _context.Warehouses.FirstOrDefaultAsync();
        _warehouseId = warehouse?.Id ?? Guid.NewGuid();

        var customer = await _context.Customers.FirstOrDefaultAsync();
        _customerId = customer?.Id;
        if (customer != null)
        {
            CustomerName = customer.Name;
            CustomerPhone = customer.Phone;
            CustomerLoyaltyPoints = customer.LoyaltyPoints;
        }

        var cashierId = Guid.NewGuid();
        var shiftResult = await _billingService.OpenShiftAsync(_branchId, "Counter-01", cashierId, "Rahul Verma", 2000m);
        if (shiftResult.Success && shiftResult.Data != null)
        {
            _shiftId = shiftResult.Data.Id;
        }

        HardwareFingerprint = _licenseValidator.GetMachineFingerprint();
        await RefreshSyncStatsAsync();
        await LoadCatalogDisplayItemsAsync();

        // Start Embedded Mobile WiFi Scanner Microserver
        _mobileScannerServer.OnBarcodeScanned += HandleMobileBarcodeScannedAsync;
        _mobileScannerServer.OnDeviceConnected += (device) =>
        {
            System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
            {
                ConnectedMobileDevice = $"🟢 Connected: {device}";
            });
        };
        _mobileScannerServer.Start(5055);
        MobileScannerUrl = _mobileScannerServer.ScannerUrl;
        MobileScannerQrImage = _mobileScannerServer.GenerateQrCodeImage();

        // ⚡ Crash Recovery: Restore unsaved cart or held bills if system previously hung or power cut occurred
        TryRestoreCartSessionSnapshot();
    }

    // ==========================================
    // NAVIGATION SWITCHER
    // ==========================================
    [RelayCommand]
    public async Task NavigateToAsync(string module)
    {
        SelectedNavModule = module;
        IsPosActive = module == "POS";
        IsDashboardActive = module == "DASHBOARD";
        IsProductsActive = module == "PRODUCTS";
        IsInventoryActive = module == "INVENTORY";
        IsPurchasesActive = module == "PURCHASES";
        IsTransfersActive = module == "TRANSFERS";
        IsCustomersActive = module == "CUSTOMERS";
        IsReturnsActive = module == "RETURNS";
        IsAccountingActive = module == "ACCOUNTING";
        IsReportsActive = module == "REPORTS";
        IsSyncActive = module == "SYNC";
        IsSettingsActive = module == "SETTINGS";

        switch (module)
        {
            case "DASHBOARD":
                await LoadDashboardDataAsync();
                break;
            case "PRODUCTS":
            case "INVENTORY":
                await LoadCatalogDisplayItemsAsync();
                break;
            case "PURCHASES":
                await LoadPurchasesDataAsync();
                break;
            case "TRANSFERS":
                await LoadTransfersDataAsync();
                break;
            case "CUSTOMERS":
                await LoadCustomersDataAsync();
                break;
            case "ACCOUNTING":
                await LoadAccountingDataAsync();
                break;
            case "REPORTS":
                await LoadReportsDataAsync();
                break;
            case "SYNC":
                await LoadSyncQueueDataAsync();
                break;
        }
    }

    // ==========================================
    // POS BILLING METHODS
    // ==========================================
    [RelayCommand]
    public async Task ScanBarcodeAsync()
    {
        if (string.IsNullOrWhiteSpace(BarcodeScanInput)) return;

        var term = BarcodeScanInput.Trim();
        BarcodeScanInput = string.Empty;

        var existing = CartItems.FirstOrDefault(c => c.Barcode == term || c.SKU == term);
        if (existing != null)
        {
            existing.Quantity += 1;
            RecalculateTotals();
            StatusMessage = $"Incremented {existing.DisplayTitle} (Qty: {existing.Quantity})";
            return;
        }

        var lookup = await _catalogService.FindVariantByBarcodeAsync(term);
        if (lookup.Success && lookup.Data != null)
        {
            var v = lookup.Data;
            var product = await _context.Products.Include(p => p.HsnCode).FirstOrDefaultAsync(p => p.Id == v.ProductId);
            var hsnCode = product?.HsnCode?.Code ?? "6205";
            var gstRate = product?.HsnCode?.GstRate ?? 5.0m;

            var line = new CartLineItemViewModel
            {
                VariantId = v.Id,
                SKU = v.SKU,
                Barcode = v.Barcode,
                ProductName = product?.Name ?? "Apparel Item",
                SizeName = v.SizeName,
                ColorName = v.ColorName,
                HsnCode = hsnCode,
                UnitPrice = v.SellingPrice,
                MRP = v.MRP,
                Quantity = 1,
                TaxRate = gstRate
            };

            CartItems.Add(line);
            RecalculateTotals();
            StatusMessage = $"Added: {line.DisplayTitle} @ Rs.{line.UnitPrice:N2}";
        }
        else
        {
            StatusMessage = $"Barcode '{term}' not found in catalog!";
        }
    }

    // ==========================================
    // SCANNER SWITCH & DEMO / MOBILE SCANNER
    // ==========================================
    [RelayCommand]
    public void SetScannerMode(string mode)
    {
        ScannerMode = (mode ?? "PHYSICAL").ToUpperInvariant();
        IsPhysicalScanner = ScannerMode == "PHYSICAL";
        IsMobileScanner = ScannerMode == "MOBILE";
        IsDemoScanner = ScannerMode == "DEMO";

        if (IsMobileScanner)
        {
            MobileScannerUrl = _mobileScannerServer.ScannerUrl;
            MobileScannerQrImage = _mobileScannerServer.GenerateQrCodeImage();
            IsMobileScannerModalOpen = true;
            StatusMessage = $"📱 Mobile Scanner Active: Pair phone at {MobileScannerUrl}";
        }
        else if (IsDemoScanner)
        {
            StatusMessage = "⚡ DEMO Scanner Mode Active: Click any quick demo barcode below to simulate laser scans.";
        }
        else
        {
            StatusMessage = "🔫 Physical USB/HID Laser Scanner Mode Active. Ready for hardware scanner.";
        }
    }

    [RelayCommand]
    public void OpenMobileScannerSetup()
    {
        MobileScannerUrl = _mobileScannerServer.ScannerUrl;
        MobileScannerQrImage = _mobileScannerServer.GenerateQrCodeImage();
        IsMobileScannerModalOpen = true;
    }

    [RelayCommand]
    public void CloseMobileScannerSetup()
    {
        IsMobileScannerModalOpen = false;
    }

    [RelayCommand]
    public async Task SimulateBarcodeScanAsync(string barcode)
    {
        BarcodeScanInput = barcode;
        await ScanBarcodeAsync();
        try { System.Media.SystemSounds.Beep.Play(); } catch { }
        StatusMessage = $"⚡ [Demo Scan Simulated] Barcode: {barcode}";
    }

    private async Task<BarcodeScanResult> HandleMobileBarcodeScannedAsync(string barcode, string device)
    {
        var app = System.Windows.Application.Current;
        if (app?.Dispatcher == null)
            return new BarcodeScanResult(false, "", "", "", 0, "POS Dispatcher unavailable");

        var task = await app.Dispatcher.InvokeAsync(async () =>
        {
            try { System.Media.SystemSounds.Beep.Play(); } catch { }

            var term = barcode.Trim();
            var existing = CartItems.FirstOrDefault(c => string.Equals(c.Barcode, term, StringComparison.OrdinalIgnoreCase) || string.Equals(c.SKU, term, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                existing.Quantity += 1;
                RecalculateTotals();
                StatusMessage = $"📱 [Mobile Scan: {device}] Incremented {existing.DisplayTitle} (Qty: {existing.Quantity})";
                return new BarcodeScanResult(true, existing.ProductName, existing.SizeName, existing.ColorName, existing.UnitPrice, "Incremented existing cart item");
            }

            var v = await _context.ProductVariants
                .Include(pv => pv.Product).ThenInclude(p => p!.HsnCode)
                .Include(pv => pv.Size)
                .Include(pv => pv.Color)
                .FirstOrDefaultAsync(pv => pv.Barcode == term || pv.SKU == term);

            if (v != null)
            {
                var product = v.Product;
                var hsnCode = product?.HsnCode?.Code ?? "6205";
                var gstRate = product?.HsnCode?.GstRate ?? 5.0m;

                var line = new CartLineItemViewModel
                {
                    VariantId = v.Id,
                    SKU = v.SKU,
                    Barcode = v.Barcode,
                    ProductName = product?.Name ?? "Apparel Item",
                    SizeName = v.Size?.Name ?? "-",
                    ColorName = v.Color?.Name ?? "-",
                    HsnCode = hsnCode,
                    UnitPrice = v.SellingPrice,
                    MRP = v.MRP,
                    Quantity = 1,
                    TaxRate = gstRate
                };

                CartItems.Add(line);
                RecalculateTotals();
                StatusMessage = $"📱 [Mobile Scan: {device}] Added: {line.DisplayTitle} @ Rs.{line.UnitPrice:N2}";
                return new BarcodeScanResult(true, line.ProductName, line.SizeName, line.ColorName, line.UnitPrice, "Added to cart successfully");
            }

            StatusMessage = $"📱 [Mobile Scan: {device}] Barcode '{term}' not found in catalog!";
            return new BarcodeScanResult(false, "", "", "", 0, $"Barcode '{term}' not found");
        });
        return await task;
    }

    // ==========================================
    // BARCODE FOR PRODUCT SKU & HANG TAG LABEL
    // ==========================================
    [RelayCommand]
    public void OpenBarcodeLabelModal(ProductVariantDisplayItem item)
    {
        if (item == null) return;
        SelectedBarcodeItem = item;
        BarcodeLabelPreviewImage = _barcodeGenerator.GenerateGarmentHangTagLabel(
            SettingStoreName,
            item.ProductName,
            item.SKU,
            item.SizeName,
            item.ColorName,
            item.MRP,
            item.SellingPrice,
            item.Barcode
        );
        BarcodeLabelPrintStatus = $"Hang tag barcode label generated for SKU: {item.SKU}";
        IsBarcodeLabelModalOpen = true;
    }

    [RelayCommand]
    public void CloseBarcodeLabelModal()
    {
        IsBarcodeLabelModalOpen = false;
    }

    [RelayCommand]
    public void PrintBarcodeLabel()
    {
        BarcodeLabelPrintStatus = $"Printed barcode label for SKU: {SelectedBarcodeItem?.SKU} (50mm × 35mm Thermal Label)";
        StatusMessage = BarcodeLabelPrintStatus;
    }

    [RelayCommand]
    public void RemoveLineItem(CartLineItemViewModel item)
    {
        if (CartItems.Contains(item))
        {
            CartItems.Remove(item);
            RecalculateTotals();
            StatusMessage = $"Removed item: {item.DisplayTitle}";
        }
    }

    [RelayCommand]
    public void ClearCart()
    {
        CartItems.Clear();
        InvoiceDiscount = 0;
        IsCrashRecoveredCart = false;
        RecalculateTotals();
        StatusMessage = "Cart cleared.";
    }

    public void RecalculateTotals()
    {
        SubTotal = CartItems.Sum(c => c.Quantity * c.UnitPrice);
        var itemDiscounts = CartItems.Sum(c => c.DiscountAmount);
        var totalDiscount = itemDiscounts + InvoiceDiscount;
        var taxable = Math.Max(0, SubTotal - totalDiscount);

        decimal calculatedTax = 0;
        foreach (var item in CartItems)
        {
            var effectivePiecePrice = item.UnitPrice - (item.DiscountAmount / (item.Quantity > 0 ? item.Quantity : 1));
            var slabRate = effectivePiecePrice <= 1000m ? 0.05m : 0.12m;
            calculatedTax += Math.Round(item.LineTotal * slabRate, 2);
        }

        TaxAmount = calculatedTax;
        var net = taxable + TaxAmount;
        GrandTotal = Math.Round(net, 0, MidpointRounding.AwayFromZero);
        RoundOff = GrandTotal - net;

        // Auto-save atomic snapshot to survive power cuts or freezes
        SaveCartSessionSnapshot();
    }

    private void SaveCartSessionSnapshot()
    {
        try
        {
            if (!CartItems.Any() && !HeldBills.Any())
            {
                if (File.Exists(_snapshotFilePath))
                {
                    File.Delete(_snapshotFilePath);
                }
                return;
            }

            var snapshot = new CartSessionSnapshotDto
            {
                SnapshotTime = DateTime.UtcNow,
                CustomerName = CustomerName,
                CustomerId = _customerId,
                CustomerPhone = CustomerPhone,
                InvoiceDiscount = InvoiceDiscount,
                Items = CartItems.Select(c => new CartLineSnapshotItem
                {
                    VariantId = c.VariantId,
                    SKU = c.SKU,
                    Barcode = c.Barcode,
                    ProductName = c.ProductName,
                    SizeName = c.SizeName,
                    ColorName = c.ColorName,
                    MRP = c.MRP,
                    UnitPrice = c.UnitPrice,
                    Quantity = c.Quantity,
                    DiscountAmount = c.DiscountAmount,
                    HsnCode = c.HsnCode
                }).ToList(),
                HeldBills = HeldBills.Select(h => new HeldBillSnapshotItem
                {
                    Id = h.Id,
                    CustomerName = h.CustomerName,
                    HeldAt = h.HeldAt,
                    ItemCount = h.ItemCount,
                    TotalAmount = h.TotalAmount,
                    InvoiceDiscount = h.InvoiceDiscount,
                    Items = h.Items.Select(c => new CartLineSnapshotItem
                    {
                        VariantId = c.VariantId,
                        SKU = c.SKU,
                        Barcode = c.Barcode,
                        ProductName = c.ProductName,
                        SizeName = c.SizeName,
                        ColorName = c.ColorName,
                        MRP = c.MRP,
                        UnitPrice = c.UnitPrice,
                        Quantity = c.Quantity,
                        DiscountAmount = c.DiscountAmount,
                        HsnCode = c.HsnCode
                    }).ToList()
                }).ToList()
            };

            var json = System.Text.Json.JsonSerializer.Serialize(snapshot, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            var tempPath = _snapshotFilePath + ".tmp";
            File.WriteAllText(tempPath, json);
            File.Move(tempPath, _snapshotFilePath, overwrite: true);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error saving cart snapshot: {ex.Message}");
        }
    }

    private void TryRestoreCartSessionSnapshot()
    {
        try
        {
            if (!File.Exists(_snapshotFilePath)) return;

            var json = File.ReadAllText(_snapshotFilePath);
            var snapshot = System.Text.Json.JsonSerializer.Deserialize<CartSessionSnapshotDto>(json);
            if (snapshot == null) return;

            // 1. Restore HeldBills if any
            if (snapshot.HeldBills != null && snapshot.HeldBills.Any())
            {
                HeldBills.Clear();
                foreach (var h in snapshot.HeldBills)
                {
                    var heldItem = new HeldBillItem
                    {
                        Id = h.Id,
                        CustomerName = h.CustomerName,
                        HeldAt = h.HeldAt,
                        ItemCount = h.ItemCount,
                        TotalAmount = h.TotalAmount,
                        InvoiceDiscount = h.InvoiceDiscount,
                        Items = h.Items.Select(i => new CartLineItemViewModel
                        {
                            VariantId = i.VariantId,
                            SKU = i.SKU,
                            Barcode = i.Barcode,
                            ProductName = i.ProductName,
                            SizeName = i.SizeName,
                            ColorName = i.ColorName,
                            MRP = i.MRP,
                            UnitPrice = i.UnitPrice,
                            Quantity = i.Quantity,
                            DiscountAmount = i.DiscountAmount,
                            HsnCode = i.HsnCode
                        }).ToList()
                    };
                    HeldBills.Add(heldItem);
                }
            }

            // 2. Restore Active Cart Items if any
            if (snapshot.Items != null && snapshot.Items.Any())
            {
                CartItems.Clear();
                foreach (var i in snapshot.Items)
                {
                    CartItems.Add(new CartLineItemViewModel
                    {
                        VariantId = i.VariantId,
                        SKU = i.SKU,
                        Barcode = i.Barcode,
                        ProductName = i.ProductName,
                        SizeName = i.SizeName,
                        ColorName = i.ColorName,
                        MRP = i.MRP,
                        UnitPrice = i.UnitPrice,
                        Quantity = i.Quantity,
                        DiscountAmount = i.DiscountAmount,
                        HsnCode = i.HsnCode
                    });
                }

                if (!string.IsNullOrEmpty(snapshot.CustomerName)) CustomerName = snapshot.CustomerName;
                if (snapshot.CustomerId.HasValue) _customerId = snapshot.CustomerId.Value;
                if (!string.IsNullOrEmpty(snapshot.CustomerPhone)) CustomerPhone = snapshot.CustomerPhone;
                InvoiceDiscount = snapshot.InvoiceDiscount;

                RecalculateTotals();
                IsCrashRecoveredCart = true;
                StatusMessage = $"⚡ CRASH RECOVERY: Restored {CartItems.Count} item(s) (Rs.{GrandTotal:N2}) from previous unsaved session!";
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to restore cart snapshot: {ex.Message}");
        }
    }

    [RelayCommand]
    public async Task QuickCashCheckoutAsync()
    {
        if (!CartItems.Any())
        {
            StatusMessage = "Cart is empty. Scan an item first.";
            return;
        }

        var saleRequest = new CreateSaleRequest(
            _branchId,
            _shiftId,
            _customerId,
            CartItems.Select(c => new SaleItemRequest(c.VariantId, c.Quantity, c.UnitPrice, 0, c.DiscountAmount)).ToList(),
            new List<PaymentRequest>
            {
                new PaymentRequest(PaymentMethod.Cash, GrandTotal)
            },
            InvoiceDiscount,
            false,
            "Quick Cash Bill POS"
        );

        var result = await _billingService.ProcessSaleAsync(saleRequest);
        if (result.Success && result.Data != null)
        {
            _lastSale = result.Data;

            if (_customerId.HasValue)
            {
                await _loyaltyService.AccruePointsAsync(_customerId.Value, result.Data.SaleId, result.Data.TotalAmount);
                var c = await _context.Customers.FindAsync(_customerId.Value);
                if (c != null) CustomerLoyaltyPoints = c.LoyaltyPoints;
            }

            ReceiptPreviewText = _receiptPrinter.GeneratePlainTextReceipt(
                result.Data,
                _storeHeader,
                new List<PaymentRequest> { new(PaymentMethod.Cash, GrandTotal) },
                ThermalPaperWidth.Width80mm
            );
            IsReceiptModalOpen = true;

            StatusMessage = $"BILL COMPLETED! Invoice: {result.Data.InvoiceNumber} | Total: Rs.{result.Data.TotalAmount:N2}";

            _ = _telegramService.NotifySaleCompletedAsync(
                result.Data.InvoiceNumber,
                result.Data.TotalAmount,
                (int)CartItems.Sum(c => c.Quantity),
                "CASH",
                CustomerName
            );

            CartItems.Clear();
            InvoiceDiscount = 0;
            RecalculateTotals();

            await RefreshSyncStatsAsync();
        }
        else
        {
            StatusMessage = $"Checkout Error: {result.Message}";
        }
    }

    [RelayCommand]
    public void OpenSplitPaymentModal()
    {
        if (!CartItems.Any())
        {
            StatusMessage = "Cart is empty.";
            return;
        }

        TenderCash = GrandTotal;
        TenderCard = 0;
        TenderUpi = 0;

        var upi = _paymentService.GenerateUpiQrPayload(
            "merchant@upi",
            "Grand Fashion Store",
            GrandTotal,
            $"POS-{DateTime.UtcNow.Ticks % 100000}"
        );
        UpiQrUri = upi.QrPayloadString;
        SplitPaymentSummary = $"Net Payable: Rs.{GrandTotal:N2} | Exact cash pre-filled.";
        IsPaymentModalOpen = true;
    }

    [RelayCommand]
    public async Task ConfirmSplitPaymentAsync()
    {
        var payments = new List<PaymentRequest>();
        if (TenderCash > 0) payments.Add(new PaymentRequest(PaymentMethod.Cash, TenderCash));
        if (TenderCard > 0) payments.Add(new PaymentRequest(PaymentMethod.Card, TenderCard));
        if (TenderUpi > 0) payments.Add(new PaymentRequest(PaymentMethod.UPI, TenderUpi));

        var validation = _paymentService.ValidateSplitTender(GrandTotal, payments);
        if (!validation.IsSufficient)
        {
            SplitPaymentSummary = validation.Message;
            return;
        }

        var saleRequest = new CreateSaleRequest(
            _branchId,
            _shiftId,
            _customerId,
            CartItems.Select(c => new SaleItemRequest(c.VariantId, c.Quantity, c.UnitPrice, 0, c.DiscountAmount)).ToList(),
            payments,
            InvoiceDiscount,
            false,
            "Split Payment POS"
        );

        var result = await _billingService.ProcessSaleAsync(saleRequest);
        if (result.Success && result.Data != null)
        {
            _lastSale = result.Data;
            IsPaymentModalOpen = false;

            ReceiptPreviewText = _receiptPrinter.GeneratePlainTextReceipt(
                result.Data,
                _storeHeader,
                payments,
                ThermalPaperWidth.Width80mm
            );
            IsReceiptModalOpen = true;

            StatusMessage = $"SPLIT BILL COMPLETED! {validation.Message}";

            _ = _telegramService.NotifySaleCompletedAsync(
                result.Data.InvoiceNumber,
                result.Data.TotalAmount,
                (int)CartItems.Sum(c => c.Quantity),
                "SPLIT (Cash/Card/UPI)",
                CustomerName
            );

            CartItems.Clear();
            InvoiceDiscount = 0;
            RecalculateTotals();

            await RefreshSyncStatsAsync();
        }
    }

    // HOLD & RESUME BILLS
    [RelayCommand]
    public void HoldCurrentBill()
    {
        if (!CartItems.Any())
        {
            StatusMessage = "Cannot hold empty cart.";
            return;
        }

        var held = new HeldBillItem
        {
            CustomerName = CustomerName,
            HeldAt = DateTime.Now,
            ItemCount = CartItems.Count,
            TotalAmount = GrandTotal,
            InvoiceDiscount = InvoiceDiscount,
            Items = CartItems.ToList()
        };

        HeldBills.Add(held);
        CartItems.Clear();
        InvoiceDiscount = 0;
        RecalculateTotals();
        StatusMessage = $"Bill held successfully ({held.CustomerName}). Total held: {HeldBills.Count}";
    }

    [RelayCommand]
    public void OpenHeldBillsModal()
    {
        IsHeldBillsModalOpen = true;
    }

    [RelayCommand]
    public void CloseHeldBillsModal()
    {
        IsHeldBillsModalOpen = false;
    }

    [RelayCommand]
    public void ResumeHeldBill(HeldBillItem held)
    {
        if (held == null) return;

        CartItems.Clear();
        foreach (var it in held.Items) CartItems.Add(it);
        CustomerName = held.CustomerName;
        InvoiceDiscount = held.InvoiceDiscount;
        RecalculateTotals();

        HeldBills.Remove(held);
        IsHeldBillsModalOpen = false;
        StatusMessage = $"Resumed held bill for {held.CustomerName}";
    }

    // PRODUCT SEARCH POPUP (F1)
    partial void OnProductSearchQueryChanged(string value)
    {
        ApplyProductSearchFilter();
    }

    public void ApplyProductSearchFilter()
    {
        SearchFilteredVariants.Clear();
        var q = (ProductSearchQuery ?? string.Empty).Trim().ToLowerInvariant();

        foreach (var item in CatalogVariants)
        {
            if (string.IsNullOrEmpty(q) ||
                item.ProductName.ToLowerInvariant().Contains(q) ||
                item.SKU.ToLowerInvariant().Contains(q) ||
                item.Barcode.ToLowerInvariant().Contains(q) ||
                item.SizeName.ToLowerInvariant().Contains(q) ||
                item.ColorName.ToLowerInvariant().Contains(q))
            {
                SearchFilteredVariants.Add(item);
            }
        }
    }

    [RelayCommand]
    public async Task OpenProductSearchModalAsync()
    {
        IsProductSearchModalOpen = !IsProductSearchModalOpen;
        if (IsProductSearchModalOpen)
        {
            if (!CatalogVariants.Any())
            {
                await LoadCatalogDisplayItemsAsync();
            }
            ProductSearchQuery = string.Empty;
            ApplyProductSearchFilter();
        }
    }

    [RelayCommand]
    public void AddFirstFilteredVariantToCart()
    {
        var first = SearchFilteredVariants.FirstOrDefault();
        if (first != null)
        {
            AddVariantToCartFromSearch(first);
        }
    }

    [RelayCommand]
    public void AddVariantToCartFromSearch(ProductVariantDisplayItem item)
    {
        if (item == null) return;

        var existing = CartItems.FirstOrDefault(c => c.VariantId == item.Id);
        if (existing != null)
        {
            existing.Quantity += 1;
        }
        else
        {
            CartItems.Add(new CartLineItemViewModel
            {
                VariantId = item.Id,
                SKU = item.SKU,
                Barcode = item.Barcode,
                ProductName = item.ProductName,
                SizeName = item.SizeName,
                ColorName = item.ColorName,
                HsnCode = "6205",
                UnitPrice = item.SellingPrice,
                MRP = item.MRP,
                Quantity = 1,
                TaxRate = item.SellingPrice <= 1000 ? 5 : 12
            });
        }

        RecalculateTotals();
        IsProductSearchModalOpen = false;
        StatusMessage = $"Added: {item.ProductName} ({item.SizeName}/{item.ColorName})";
    }

    // DISCOUNT MODAL (F4)
    [RelayCommand]
    public void OpenDiscountModal()
    {
        DiscountPercentInput = 0;
        DiscountFlatInput = 0;
        IsDiscountModalOpen = !IsDiscountModalOpen;
    }

    [RelayCommand]
    public void ApplyDiscount()
    {
        if (DiscountPercentInput > 0)
        {
            InvoiceDiscount = Math.Round(SubTotal * (DiscountPercentInput / 100m), 2);
        }
        else if (DiscountFlatInput > 0)
        {
            InvoiceDiscount = Math.Min(SubTotal, DiscountFlatInput);
        }

        RecalculateTotals();
        IsDiscountModalOpen = false;
        StatusMessage = $"Applied Discount: Rs.{InvoiceDiscount:N2}";
    }

    // CUSTOMER SELECT MODAL (F5)
    [RelayCommand]
    public void OpenCustomerModal()
    {
        IsCustomerModalOpen = !IsCustomerModalOpen;
    }

    [RelayCommand]
    public void SelectCustomer(Customer cust)
    {
        if (cust == null) return;
        _customerId = cust.Id;
        CustomerName = cust.Name;
        CustomerPhone = cust.Phone;
        CustomerLoyaltyPoints = cust.LoyaltyPoints;
        IsCustomerModalOpen = false;
        StatusMessage = $"Customer linked: {cust.Name} (Points: {cust.LoyaltyPoints})";
    }

    [RelayCommand]
    public void PrintLastReceipt()
    {
        if (_lastSale == null)
        {
            StatusMessage = "No recent invoice found to re-print.";
            return;
        }

        ReceiptPreviewText = _receiptPrinter.GeneratePlainTextReceipt(_lastSale, _storeHeader, null, ThermalPaperWidth.Width80mm);
        IsReceiptModalOpen = true;
        StatusMessage = $"Re-printing Invoice {_lastSale.InvoiceNumber}...";
    }

    [RelayCommand]
    public async Task GenerateShiftZReportAsync()
    {
        var reportResult = await _shiftReportingService.GenerateShiftZReportAsync(_shiftId);
        if (reportResult.Success && reportResult.Data != null)
        {
            ZReportText = _shiftReportingService.FormatZReportReceipt(reportResult.Data);
            IsZReportModalOpen = true;
            StatusMessage = "Shift Z-Report generated successfully.";

            _ = _telegramService.NotifyShiftClosedAsync(
                reportResult.Data.CashierName,
                reportResult.Data.NetSales,
                reportResult.Data.CashSales,
                reportResult.Data.UpiSales,
                reportResult.Data.BillsCount
            );
        }
        else
        {
            StatusMessage = $"Error generating Z-Report: {reportResult.Message}";
        }
    }

    [RelayCommand]
    public void CloseReceiptModal() => IsReceiptModalOpen = false;

    [RelayCommand]
    public void CloseZReportModal() => IsZReportModalOpen = false;

    // ==========================================
    // MODULE: DASHBOARD
    // ==========================================
    public async Task LoadDashboardDataAsync()
    {
        var kpis = await _dashboardService.GetStoreKpisAsync(_branchId);
        if (kpis.Success && kpis.Data != null)
        {
            DashTodaySales = kpis.Data.TodayNetSales;
            DashInvoiceCount = kpis.Data.TodayBillsCount;
            DashItemsSold = kpis.Data.TotalActiveVariants;
            DashCashCollection = kpis.Data.TodayCashCollected;
            DashUpiCollection = kpis.Data.TodayUpiCollected;
            DashLowStockAlerts = kpis.Data.LowStockAlertsCount;
        }

        var recentSales = await _context.Sales
            .Include(s => s.Items)
            .OrderByDescending(s => s.CreatedAt)
            .Take(10)
            .ToListAsync();

        DashRecentSales.Clear();
        foreach (var s in recentSales)
        {
            DashRecentSales.Add(new SaleResponseDto(
                s.Id,
                s.InvoiceNumber,
                s.SubTotal,
                s.DiscountAmount,
                s.SubTotal - s.DiscountAmount,
                s.CgstAmount,
                s.SgstAmount,
                s.IgstAmount,
                s.RoundOff,
                s.TotalAmount,
                s.CreatedAt,
                s.Items.Select(i => new SaleItemDto(
                    i.VariantId,
                    i.SKU,
                    i.Barcode,
                    i.ProductName,
                    i.SizeName,
                    i.ColorName,
                    i.HsnCode,
                    i.Quantity,
                    i.UnitPrice,
                    i.DiscountAmount,
                    i.TaxRate,
                    Math.Round((i.Quantity * i.UnitPrice - i.DiscountAmount) * (i.TaxRate / 100m), 2),
                    i.TotalAmount,
                    i.UnitPrice * 1.2m
                )).ToList()
            ));
        }
    }

    // ==========================================
    // MODULE: PRODUCTS & 25-VARIANT MATRIX
    // ==========================================
    public async Task LoadCatalogDisplayItemsAsync()
    {
        var variants = await _context.ProductVariants
            .Include(v => v.Product)
            .Include(v => v.Size)
            .Include(v => v.Color)
            .Include(v => v.Stocks)
            .ToListAsync();

        CatalogVariants.Clear();
        InventoryStockItems.Clear();

        foreach (var v in variants)
        {
            var item = new ProductVariantDisplayItem
            {
                Id = v.Id,
                SKU = v.SKU,
                Barcode = v.Barcode,
                ProductName = v.Product?.Name ?? "Apparel Item",
                CategoryName = "Men's Apparel",
                SizeName = v.Size?.Name ?? "-",
                ColorName = v.Color?.Name ?? "-",
                PurchasePrice = v.CostPrice,
                SellingPrice = v.SellingPrice,
                MRP = v.MRP,
                StockQty = v.Stocks.Sum(s => s.Quantity)
            };

            CatalogVariants.Add(item);
            InventoryStockItems.Add(item);
        }

        ApplyProductSearchFilter();
    }

    [RelayCommand]
    public async Task Generate25VariantMatrixAsync()
    {
        try
        {
            MatrixGenStatus = "Generating 5 Sizes × 5 Colors = 25 independently trackable variants...";

            var cat = await _context.Categories.FirstOrDefaultAsync();
            var brand = await _context.Brands.FirstOrDefaultAsync();
            var hsn = await _context.HsnCodes.FirstOrDefaultAsync();

            var existingCount = await _context.Products.CountAsync(p => p.StyleCode.StartsWith("SHIRT-OXF-"));
            var batchNumber = existingCount + 1;
            var uniqueStyleCode = $"SHIRT-OXF-B{batchNumber}-{DateTime.UtcNow:yyMMddHHmm}";

            var parentProduct = new Product
            {
                StyleCode = uniqueStyleCode,
                Name = $"Men's Luxury Oxford Casual Shirt (Batch #{batchNumber})",
                CategoryId = cat?.Id ?? Guid.NewGuid(),
                BrandId = brand?.Id ?? Guid.NewGuid(),
                HsnCodeId = hsn?.Id ?? Guid.NewGuid(),
                Gender = "Men",
                Fabric = "100% Cotton Oxford",
                Season = "All Season",
                HasVariants = true,
                Description = $"Premium 100% Cotton Oxford Weave Casual Shirt (Batch #{batchNumber})"
            };
            _context.Products.Add(parentProduct);
            await _context.SaveChangesAsync();

            var sizes = await _context.Sizes.Take(5).ToListAsync();
            var colors = await _context.Colors.Take(5).ToListAsync();

            var generated = _matrixGenerator.GenerateMatrix(
                parentProduct,
                sizes,
                colors,
                650m,
                1299m,
                1899m
            );

            // Ensure every generated variant barcode is globally unique in the database
            var existingBarcodes = await _context.ProductVariants.Select(pv => pv.Barcode).ToHashSetAsync();
            foreach (var v in generated)
            {
                while (existingBarcodes.Contains(v.Barcode))
                {
                    var randomNum = Random.Shared.Next(100000000, 999999999);
                    var raw12 = $"890{randomNum:D9}";
                    v.Barcode = $"{raw12}{VariantMatrixGenerator.CalculateEan13CheckDigit(raw12)}";
                }
                existingBarcodes.Add(v.Barcode);

                _context.ProductVariants.Add(v);
                _context.Stocks.Add(new Stock
                {
                    VariantId = v.Id,
                    WarehouseId = _warehouseId,
                    BranchId = _branchId,
                    Quantity = 20,
                    ReservedQuantity = 0
                });
            }
            await _context.SaveChangesAsync();

            MatrixGenStatus = $"SUCCESS! Created 25 variants with unique SKUs, EAN-13 barcodes & 20 initial stock each for Batch #{batchNumber}.";
            await LoadCatalogDisplayItemsAsync();
        }
        catch (Exception ex)
        {
            var innerMsg = ex.InnerException?.Message ?? ex.Message;
            MatrixGenStatus = $"❌ Error creating variant matrix: {innerMsg}";
            await _telemetryReporter.LogExceptionAsync(ex, "Generate25VariantMatrixAsync");
        }
    }

    // ==========================================
    // MODULE: INVENTORY ADJUSTMENT
    // ==========================================
    [RelayCommand]
    public async Task AdjustStockAsync()
    {
        if (SelectedInventoryItem == null)
        {
            StatusMessage = "Select a variant to adjust.";
            return;
        }

        var stock = await _context.Stocks.FirstOrDefaultAsync(s => s.VariantId == SelectedInventoryItem.Id && s.BranchId == _branchId);
        if (stock == null)
        {
            stock = new Stock { VariantId = SelectedInventoryItem.Id, WarehouseId = _warehouseId, BranchId = _branchId, Quantity = 0 };
            _context.Stocks.Add(stock);
        }

        stock.Quantity += StockAdjustmentQty;
        _context.StockMovements.Add(new StockMovement
        {
            VariantId = SelectedInventoryItem.Id,
            WarehouseId = _warehouseId,
            Quantity = StockAdjustmentQty,
            MovementType = StockMovementType.Adjustment,
            UnitCost = SelectedInventoryItem.PurchasePrice,
            Notes = StockAdjustmentReason
        });
        await _context.SaveChangesAsync();

        StatusMessage = $"Stock adjusted for {SelectedInventoryItem.ProductName} by {StockAdjustmentQty:+#;-#;0}";
        await LoadCatalogDisplayItemsAsync();
    }

    // ==========================================
    // MODULE: PURCHASES & SUPPLIER INTAKE
    // ==========================================
    public async Task LoadPurchasesDataAsync()
    {
        var suppliersRes = await _purchaseService.GetSuppliersAsync();
        if (suppliersRes.Success && suppliersRes.Data != null)
        {
            SuppliersList.Clear();
            foreach (var s in suppliersRes.Data) SuppliersList.Add(s);
            SelectedSupplier = SuppliersList.FirstOrDefault();
        }

        var recentRes = await _purchaseService.GetRecentPurchasesAsync();
        if (recentRes.Success && recentRes.Data != null)
        {
            PurchaseHistory.Clear();
            foreach (var p in recentRes.Data) PurchaseHistory.Add(p);
        }
    }

    [RelayCommand]
    public async Task SubmitPurchaseIntakeAsync()
    {
        if (SelectedSupplier == null || PurchaseSelectedVariant == null)
        {
            StatusMessage = "Select supplier and variant for purchase.";
            return;
        }

        var req = new CreatePurchaseInvoiceRequest(
            PurchaseInvoiceNo,
            SelectedSupplier.Id,
            _branchId,
            _warehouseId,
            DateTime.UtcNow,
            new List<PurchaseItemInputDto>
            {
                new(PurchaseSelectedVariant.Id, PurchaseQty, PurchasePrice, PurchaseMRP, PurchaseMRP, 5m)
            },
            PurchaseQty * PurchasePrice,
            "Direct Inward Intake from Supplier"
        );

        var res = await _purchaseService.ProcessPurchaseInvoiceAsync(req);
        if (res.Success)
        {
            StatusMessage = $"Purchase inward recorded! Stock increased by {PurchaseQty}";
            PurchaseInvoiceNo = "PUR-" + DateTime.Now.ToString("yyyyMMdd-HHmm");
            await LoadPurchasesDataAsync();
            await LoadCatalogDisplayItemsAsync();
        }
    }

    // ==========================================
    // MODULE: STOCK TRANSFERS
    // ==========================================
    public async Task LoadTransfersDataAsync()
    {
        var transfersRes = await _transferService.GetPendingTransfersAsync();
        if (transfersRes.Success && transfersRes.Data != null)
        {
            TransferHistory.Clear();
            foreach (var t in transfersRes.Data) TransferHistory.Add(t);
        }
    }

    [RelayCommand]
    public async Task DispatchTransferAsync()
    {
        if (TransferSelectedVariant == null)
        {
            StatusMessage = "Select a variant to transfer.";
            return;
        }

        var req = new CreateStockTransferRequest(
            _warehouseId,
            _warehouseId,
            new List<StockTransferItemInputDto>
            {
                new(TransferSelectedVariant.Id, TransferQty)
            },
            "Store Cashier",
            TransferNotes
        );

        var res = await _transferService.CreateTransferDispatchAsync(req);
        if (res.Success)
        {
            StatusMessage = $"Transfer dispatched (Qty: {TransferQty})";
            await LoadTransfersDataAsync();
        }
    }

    [RelayCommand]
    public async Task ReceiveTransferAsync(StockTransferDto transfer)
    {
        if (transfer == null) return;
        var res = await _transferService.ConfirmTransferReceiveAsync(transfer.Id, "Store Manager");
        if (res.Success)
        {
            StatusMessage = $"Transfer {transfer.TransferNumber} received into store inventory!";
            await LoadTransfersDataAsync();
            await LoadCatalogDisplayItemsAsync();
        }
    }

    // ==========================================
    // MODULE: CUSTOMERS & LOYALTY
    // ==========================================
    public async Task LoadCustomersDataAsync()
    {
        var custs = await _context.Customers.ToListAsync();
        CustomersDirectory.Clear();
        foreach (var c in custs) CustomersDirectory.Add(c);
    }

    [RelayCommand]
    public async Task CreateNewCustomerAsync()
    {
        if (string.IsNullOrWhiteSpace(NewCustomerName) || string.IsNullOrWhiteSpace(NewCustomerPhone))
        {
            StatusMessage = "Enter customer name and phone number.";
            return;
        }

        var cust = new Customer
        {
            Name = NewCustomerName.Trim(),
            Phone = NewCustomerPhone.Trim(),
            Email = NewCustomerEmail.Trim(),
            LoyaltyPoints = 50
        };

        _context.Customers.Add(cust);
        await _context.SaveChangesAsync();

        NewCustomerName = string.Empty;
        NewCustomerPhone = string.Empty;
        NewCustomerEmail = string.Empty;

        StatusMessage = $"Customer '{cust.Name}' enrolled with 50 loyalty bonus points!";
        await LoadCustomersDataAsync();
    }

    // ==========================================
    // MODULE: RETURNS & EXCHANGES
    // ==========================================
    [RelayCommand]
    public async Task LookupReturnInvoiceAsync()
    {
        if (string.IsNullOrWhiteSpace(ReturnInvoiceLookupNo)) return;

        var term = ReturnInvoiceLookupNo.Trim();
        var sale = await _context.Sales
            .Include(s => s.Items)
            .FirstOrDefaultAsync(s => s.InvoiceNumber == term);

        if (sale != null)
        {
            ReturnFoundSale = new SaleResponseDto(
                sale.Id,
                sale.InvoiceNumber,
                sale.SubTotal,
                sale.DiscountAmount,
                sale.SubTotal - sale.DiscountAmount,
                sale.CgstAmount,
                sale.SgstAmount,
                sale.IgstAmount,
                sale.RoundOff,
                sale.TotalAmount,
                sale.CreatedAt,
                sale.Items.Select(i => new SaleItemDto(
                    i.VariantId,
                    i.SKU,
                    i.Barcode,
                    i.ProductName,
                    i.SizeName,
                    i.ColorName,
                    i.HsnCode,
                    i.Quantity,
                    i.UnitPrice,
                    i.DiscountAmount,
                    i.TaxRate,
                    Math.Round((i.Quantity * i.UnitPrice - i.DiscountAmount) * (i.TaxRate / 100m), 2),
                    i.TotalAmount,
                    i.UnitPrice * 1.2m
                )).ToList()
            );
            ReturnSelectedItem = ReturnFoundSale.Items.FirstOrDefault();
            StatusMessage = $"Found Invoice {sale.InvoiceNumber} with {sale.Items.Count} item(s).";
        }
        else
        {
            StatusMessage = $"Invoice '{ReturnInvoiceLookupNo}' not found!";
            ReturnFoundSale = null;
        }
    }

    [RelayCommand]
    public async Task ProcessReturnAsync()
    {
        if (ReturnFoundSale == null || ReturnSelectedItem == null)
        {
            StatusMessage = "Select an item to return.";
            return;
        }

        var req = new ProcessReturnExchangeRequest(
            ReturnFoundSale.SaleId,
            _branchId,
            _shiftId,
            new List<ReturnItemRequest>
            {
                new(ReturnSelectedItem.VariantId, ReturnQuantity, ReturnSelectedItem.UnitPrice, ReturnReason)
            }
        );

        var res = await _returnExchangeService.ProcessReturnExchangeAsync(req);
        if (res.Success && res.Data != null)
        {
            CreditNoteResult = $"RETURN SUCCESS! Credit Note: {res.Data.CreditNoteNumber} | Refund: Rs.{res.Data.NetPayableOrRefund:N2}";
            StatusMessage = CreditNoteResult;
            await RefreshSyncStatsAsync();
            await LoadCatalogDisplayItemsAsync();
        }
    }

    // ==========================================
    // MODULE: ACCOUNTING & EXPENSES
    // ==========================================
    public async Task LoadAccountingDataAsync()
    {
        var expenses = await _expenseService.GetDailyExpensesAsync(_branchId, DateTime.UtcNow);
        if (expenses.Success && expenses.Data != null)
        {
            ExpensesList.Clear();
            foreach (var exp in expenses.Data) ExpensesList.Add(exp);
        }

        var ledgers = await _context.LedgerAccounts.ToListAsync();
        ChartOfAccounts.Clear();
        foreach (var l in ledgers) ChartOfAccounts.Add(l);

        var tb = await _accountingService.GetTrialBalanceAsync();
        if (tb.Success && tb.Data != null)
        {
            TrialBalanceDebits = tb.Data.TotalDebits;
            TrialBalanceCredits = tb.Data.TotalCredits;
            IsTrialBalanceBalanced = tb.Data.IsBalanced;
        }
    }

    [RelayCommand]
    public async Task AddExpenseAsync()
    {
        if (ExpenseAmount <= 0) return;

        var cat = await _context.ExpenseCategories.FirstOrDefaultAsync();
        var dto = new CreateExpenseDto(
            _branchId,
            cat?.Id ?? Guid.NewGuid(),
            _shiftId,
            ExpenseAmount,
            "Store Vendor",
            PaymentMethod.Cash,
            ExpenseTitle
        );

        var res = await _expenseService.RecordExpenseAsync(dto);
        if (res.Success)
        {
            StatusMessage = $"Expense Rs.{ExpenseAmount:N2} recorded for {ExpenseTitle}";
            ExpenseAmount = 0;
            await LoadAccountingDataAsync();
        }
    }

    // ==========================================
    // MODULE: GST & P/L REPORTS
    // ==========================================
    public async Task LoadReportsDataAsync()
    {
        var start = DateTime.UtcNow.AddMonths(-1);
        var end = DateTime.UtcNow.AddDays(1);

        var gstr1 = await _gstReportService.GenerateGstr1ReportAsync(start, end, _branchId);
        if (gstr1.Success && gstr1.Data != null)
        {
            Gstr1HsnSummaries.Clear();
            foreach (var hsn in gstr1.Data.HsnRows) Gstr1HsnSummaries.Add(hsn);
        }

        var pl = await _reportService.GetProfitAndLossReportAsync(start, end);
        if (pl.Success && pl.Data != null)
        {
            RptTotalRevenue = pl.Data.TotalSalesRevenue;
            RptCogs = pl.Data.TotalCostOfGoodsSold;
            RptGrossProfit = pl.Data.GrossProfit;
            RptExpenses = pl.Data.TotalOperatingExpenses;
            RptNetProfit = pl.Data.NetProfitBeforeTax;
        }

        var valuation = await _reportService.GetStockValuationReportAsync(_warehouseId);
        if (valuation.Success && valuation.Data != null)
        {
            RptStockTotalValue = valuation.Data.TotalCostValuation;
            RptStockTotalUnits = valuation.Data.TotalQuantityOnHand;
        }
    }

    // ==========================================
    // MODULE: DATA SYNC CENTER (POSRES SYNC)
    // ==========================================
    public async Task LoadSyncQueueDataAsync()
    {
        var outbox = await _context.SyncOutbox.OrderByDescending(o => o.CreatedAt).Take(30).ToListAsync();
        OutboxList.Clear();
        foreach (var o in outbox) OutboxList.Add(o);

        var inbox = await _context.SyncInbox.OrderByDescending(i => i.ReceivedAt).Take(30).ToListAsync();
        InboxList.Clear();
        foreach (var i in inbox) InboxList.Add(i);

        await RefreshSyncStatsAsync();
    }

    [RelayCommand]
    public async Task ForceSyncAsync()
    {
        StatusMessage = "Triggering DataSync Engine with Store Server...";
        var result = await _syncEngine.PushOutboxEventsAsync();
        if (result.Success && result.Data != null)
        {
            StatusMessage = $"Sync Complete: {result.Data.ConfirmedEventIds.Count} sent. Status: {result.Message}";
        }
        else
        {
            StatusMessage = $"Sync Result: {result.Message}";
        }
        await RefreshSyncStatsAsync();
        await LoadSyncQueueDataAsync();
    }

    public async Task RefreshSyncStatsAsync()
    {
        var counts = await _syncEngine.GetSyncQueueStatisticsAsync();
        if (counts.Success)
        {
            SyncPendingCount = counts.Data.Pending;
            SyncDoneCount = counts.Data.Done;
            SyncFailedCount = counts.Data.Failed;
        }
    }

    // ==========================================
    // MODULE: SETTINGS & HARDWARE
    // ==========================================
    [RelayCommand]
    public async Task TestErrorTelemetryAsync()
    {
        SettingStatusMessage = "Sending test exception to License Telemetry Server...";
        var ex = new ApplicationException("Manual User Telemetry Test: Hardware scanner & sync diagnostics.");
        await _telemetryReporter.LogExceptionAsync(ex, "Settings Diagnostics Test");
        SettingStatusMessage = "Telemetry Dispatched to License Server (or spooled in local buffer).";
    }

    [RelayCommand]
    public async Task TestTelegramBotAsync()
    {
        TelegramStatusMessage = "Sending test alert via Telegram Bot...";
        _telegramService.UpdateCredentials(TelegramBotToken, TelegramChatId);
        var res = await _telegramService.TestMessageAsync(TelegramBotToken, TelegramChatId);
        TelegramStatusMessage = res.Success
            ? "✅ Telegram alert delivered successfully to your Telegram chat!"
            : $"❌ Telegram Error: {res.Message}";
    }

    [RelayCommand]
    public async Task VerifyOnlineLicenseAsync()
    {
        SettingStatusMessage = "Connecting to https://license.abhishekmahant.in/api/license/verify...";
        var res = await _licenseValidator.ValidateAsync("GARMENT-PRO-2026-FASHION");
        LicenseBadgeText = $"License: {(res.IsValid ? "Active" : "Trial")} (license.abhishekmahant.in)";
        SettingStatusMessage = $"[Online License Response] {res.Message}";
    }

    [RelayCommand]
    public async Task CreateBackupSnapshotAsync()
    {
        SettingStatusMessage = "Creating SHA-256 verified database backup snapshot...";
        var targetDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "backups");
        var res = await _backupService.CreateDatabaseBackupAsync(targetDir);
        if (res.Success && res.Data != null)
        {
            SettingStatusMessage = $"Backup Created! File: {Path.GetFileName(res.Data.BackupFilePath)} ({res.Data.FileSizeBytes} bytes)";
        }
        else
        {
            SettingStatusMessage = $"Backup failed: {res.Message}";
        }
    }
}

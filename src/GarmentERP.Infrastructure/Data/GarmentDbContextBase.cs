using GarmentERP.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GarmentERP.Infrastructure.Data;

public abstract class GarmentDbContextBase : DbContext
{
    protected GarmentDbContextBase(DbContextOptions options) : base(options) { }

    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Brand> Brands => Set<Brand>();
    public DbSet<Size> Sizes => Set<Size>();
    public DbSet<Color> Colors => Set<Color>();
    public DbSet<HsnCode> HsnCodes => Set<HsnCode>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductVariant> ProductVariants => Set<ProductVariant>();
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<Warehouse> Warehouses => Set<Warehouse>();
    public DbSet<Stock> Stocks => Set<Stock>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<CashierShift> CashierShifts => Set<CashierShift>();
    public DbSet<Sale> Sales => Set<Sale>();
    public DbSet<SaleItem> SaleItems => Set<SaleItem>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<SyncOutbox> SyncOutbox => Set<SyncOutbox>();
    public DbSet<SyncInbox> SyncInbox => Set<SyncInbox>();
    public DbSet<LicenseInfo> LicenseInfos => Set<LicenseInfo>();
    public DbSet<ErrorTelemetry> ErrorTelemetries => Set<ErrorTelemetry>();
    public DbSet<LoyaltyTransaction> LoyaltyTransactions => Set<LoyaltyTransaction>();
    public DbSet<CreditNote> CreditNotes => Set<CreditNote>();
    public DbSet<ReturnInvoice> ReturnInvoices => Set<ReturnInvoice>();
    public DbSet<ReturnItem> ReturnItems => Set<ReturnItem>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<PurchaseInvoice> PurchaseInvoices => Set<PurchaseInvoice>();
    public DbSet<PurchaseItem> PurchaseItems => Set<PurchaseItem>();
    public DbSet<StockTransfer> StockTransfers => Set<StockTransfer>();
    public DbSet<StockTransferItem> StockTransferItems => Set<StockTransferItem>();
    public DbSet<Promotion> Promotions => Set<Promotion>();
    public DbSet<ExpenseCategory> ExpenseCategories => Set<ExpenseCategory>();
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<StaffUser> StaffUsers => Set<StaffUser>();
    public DbSet<LedgerAccount> LedgerAccounts => Set<LedgerAccount>();
    public DbSet<JournalVoucher> JournalVouchers => Set<JournalVoucher>();
    public DbSet<VoucherEntry> VoucherEntries => Set<VoucherEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Product Variant unique constraint on SKU & Barcode
        modelBuilder.Entity<ProductVariant>(entity =>
        {
            entity.HasIndex(e => e.SKU).IsUnique();
            entity.HasIndex(e => e.Barcode).IsUnique();
            entity.Property(e => e.CostPrice).HasPrecision(18, 2);
            entity.Property(e => e.SellingPrice).HasPrecision(18, 2);
            entity.Property(e => e.MRP).HasPrecision(18, 2);
        });

        // Stock indexing & precision
        modelBuilder.Entity<Stock>(entity =>
        {
            entity.HasIndex(e => new { e.VariantId, e.WarehouseId }).IsUnique();
            entity.Property(e => e.Quantity).HasPrecision(18, 3);
            entity.Property(e => e.ReservedQuantity).HasPrecision(18, 3);
        });

        // Sales configuration
        modelBuilder.Entity<Sale>(entity =>
        {
            entity.HasIndex(e => e.InvoiceNumber).IsUnique();
            entity.Property(e => e.SubTotal).HasPrecision(18, 2);
            entity.Property(e => e.DiscountPercentage).HasPrecision(5, 2);
            entity.Property(e => e.DiscountAmount).HasPrecision(18, 2);
            entity.Property(e => e.TaxAmount).HasPrecision(18, 2);
            entity.Property(e => e.CgstAmount).HasPrecision(18, 2);
            entity.Property(e => e.SgstAmount).HasPrecision(18, 2);
            entity.Property(e => e.IgstAmount).HasPrecision(18, 2);
            entity.Property(e => e.RoundOff).HasPrecision(18, 2);
            entity.Property(e => e.TotalAmount).HasPrecision(18, 2);
        });

        modelBuilder.Entity<SaleItem>(entity =>
        {
            entity.Property(e => e.Quantity).HasPrecision(18, 3);
            entity.Property(e => e.UnitPrice).HasPrecision(18, 2);
            entity.Property(e => e.MRP).HasPrecision(18, 2);
            entity.Property(e => e.DiscountPercentage).HasPrecision(5, 2);
            entity.Property(e => e.DiscountAmount).HasPrecision(18, 2);
            entity.Property(e => e.TaxableAmount).HasPrecision(18, 2);
            entity.Property(e => e.TaxRate).HasPrecision(5, 2);
            entity.Property(e => e.CgstAmount).HasPrecision(18, 2);
            entity.Property(e => e.SgstAmount).HasPrecision(18, 2);
            entity.Property(e => e.IgstAmount).HasPrecision(18, 2);
            entity.Property(e => e.TotalAmount).HasPrecision(18, 2);
        });

        modelBuilder.Entity<Payment>(entity =>
        {
            entity.Property(e => e.Amount).HasPrecision(18, 2);
        });

        modelBuilder.Entity<SyncOutbox>(entity =>
        {
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.CreatedAt);
        });
    }
}

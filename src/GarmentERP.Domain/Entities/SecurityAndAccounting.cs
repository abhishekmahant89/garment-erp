using GarmentERP.Domain.Common;

namespace GarmentERP.Domain.Entities;

public enum UserRole
{
    Cashier,
    StoreManager,
    InventoryClerk,
    SuperAdmin
}

public class StaffUser : BaseEntity
{
    public string Username { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string PasswordSalt { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.Cashier;
    public Guid BranchId { get; set; }
    public Branch? Branch { get; set; }
    public DateTime? LastLoginAt { get; set; }
}

public enum AccountType
{
    Asset,
    Liability,
    Equity,
    Revenue,
    Expense
}

public class LedgerAccount : BaseEntity
{
    public string AccountCode { get; set; } = string.Empty; // e.g. 1010 (Cash), 4010 (Sales)
    public string AccountName { get; set; } = string.Empty;
    public AccountType Type { get; set; }
    public decimal CurrentBalance { get; set; } = 0; // Positive for Debit normal, or Credit normal
    public bool IsSystemAccount { get; set; } = true;
}

public class JournalVoucher : BaseEntity
{
    public string VoucherNumber { get; set; } = string.Empty; // JV-YYYYMMDD-XXXX
    public DateTime VoucherDate { get; set; } = DateTime.UtcNow;
    public string ReferenceNumber { get; set; } = string.Empty; // InvoiceNumber, PurchaseNumber
    public string Description { get; set; } = string.Empty;
    public decimal TotalDebit { get; set; }
    public decimal TotalCredit { get; set; }
    public bool IsBalanced => Math.Abs(TotalDebit - TotalCredit) < 0.05m;

    public ICollection<VoucherEntry> Entries { get; set; } = new List<VoucherEntry>();
}

public class VoucherEntry : BaseEntity
{
    public Guid JournalVoucherId { get; set; }
    public JournalVoucher? JournalVoucher { get; set; }
    public Guid LedgerAccountId { get; set; }
    public LedgerAccount? LedgerAccount { get; set; }
    public decimal DebitAmount { get; set; } = 0;
    public decimal CreditAmount { get; set; } = 0;
    public string? LineNarration { get; set; }
}

using GarmentERP.Domain.Common;

namespace GarmentERP.Domain.Entities;

public enum PromotionType
{
    FlatPercentage,       // e.g., 20% off entire bill or category
    FlatAmount,           // e.g., Flat Rs. 500 off on purchase above Rs. 3000
    BuyXGetYFree,         // e.g., Buy 2 Shirts Get 1 T-Shirt Free
    CouponCode            // e.g., "FESTIVE2026"
}

public class Promotion : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty; // e.g. DIWALI20, FLAT500
    public PromotionType Type { get; set; }
    public decimal DiscountValue { get; set; } // 20 (%) or 500 (Rs)
    public decimal MinimumBillValue { get; set; } = 0;
    public int BuyQuantity { get; set; } = 0;
    public int FreeQuantity { get; set; } = 0;
    public Guid? TargetCategoryId { get; set; }
    public Category? TargetCategory { get; set; }
    public Guid? TargetBrandId { get; set; }
    public Brand? TargetBrand { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int UsageCount { get; set; } = 0;
    public int? MaxUsageCount { get; set; }
}

public class ExpenseCategory : BaseEntity
{
    public string Name { get; set; } = string.Empty; // Store Rent, Electricity, Cleaning, Staff Refreshment
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public class Expense : BaseEntity
{
    public Guid BranchId { get; set; }
    public Branch? Branch { get; set; }
    public Guid ExpenseCategoryId { get; set; }
    public ExpenseCategory? ExpenseCategory { get; set; }
    public Guid? CashierShiftId { get; set; } // If paid out of cashier petty cash
    public decimal Amount { get; set; }
    public string PaidTo { get; set; } = string.Empty;
    public PaymentMethod PaymentMode { get; set; } = PaymentMethod.Cash;
    public string? ReceiptVoucherNumber { get; set; }
    public string? Remarks { get; set; }
    public DateTime ExpenseDate { get; set; } = DateTime.UtcNow;
}

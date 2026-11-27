using GarmentERP.Domain.Common;

namespace GarmentERP.Domain.Entities;

public class Customer : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Gstin { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public int LoyaltyPoints { get; set; } = 0;
    public decimal StoreCredit { get; set; } = 0;
}

public enum ShiftStatus
{
    Open,
    Closed
}

public class CashierShift : BaseEntity
{
    public Guid BranchId { get; set; }
    public Branch? Branch { get; set; }
    public string CounterNumber { get; set; } = "Counter-01";
    public Guid CashierUserId { get; set; }
    public string CashierName { get; set; } = string.Empty;
    public DateTime OpenedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ClosedAt { get; set; }
    public decimal StartingFloat { get; set; } = 0;
    public decimal ExpectedCash { get; set; } = 0;
    public decimal ActualCash { get; set; } = 0;
    public decimal Discrepancy => ActualCash - ExpectedCash;
    public ShiftStatus Status { get; set; } = ShiftStatus.Open;
    public string? Notes { get; set; }
}

public enum SaleStatus
{
    Completed,
    Held,
    Cancelled,
    PartiallyReturned,
    FullyReturned
}

public enum PaymentMethod
{
    Cash,
    Card,
    UPI,
    Split,
    StoreCredit,
    LoyaltyPoints
}

public class Sale : BaseEntity
{
    public string InvoiceNumber { get; set; } = string.Empty;
    public Guid BranchId { get; set; }
    public Branch? Branch { get; set; }
    public Guid CashierShiftId { get; set; }
    public CashierShift? CashierShift { get; set; }
    public Guid? CustomerId { get; set; }
    public Customer? Customer { get; set; }

    public decimal SubTotal { get; set; }
    public decimal DiscountPercentage { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal CgstAmount { get; set; }
    public decimal SgstAmount { get; set; }
    public decimal IgstAmount { get; set; }
    public decimal RoundOff { get; set; }
    public decimal TotalAmount { get; set; }

    public SaleStatus Status { get; set; } = SaleStatus.Completed;
    public string? HoldTag { get; set; } // Reference name when parked/held
    public string? Notes { get; set; }

    public ICollection<SaleItem> Items { get; set; } = new List<SaleItem>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
}

public class SaleItem : BaseEntity
{
    public Guid SaleId { get; set; }
    public Sale? Sale { get; set; }
    public Guid VariantId { get; set; }
    public ProductVariant? Variant { get; set; }

    public string SKU { get; set; } = string.Empty;
    public string Barcode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string SizeName { get; set; } = string.Empty;
    public string ColorName { get; set; } = string.Empty;
    public string HsnCode { get; set; } = string.Empty;

    public decimal Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }
    public decimal MRP { get; set; }
    public decimal DiscountPercentage { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxableAmount { get; set; }
    public decimal TaxRate { get; set; } // 5%, 12%, etc.
    public decimal CgstAmount { get; set; }
    public decimal SgstAmount { get; set; }
    public decimal IgstAmount { get; set; }
    public decimal TotalAmount { get; set; }
}

public class Payment : BaseEntity
{
    public Guid SaleId { get; set; }
    public Sale? Sale { get; set; }
    public PaymentMethod Method { get; set; }
    public decimal Amount { get; set; }
    public string? ReferenceTransactionId { get; set; }
    public DateTime PaidAt { get; set; } = DateTime.UtcNow;
}

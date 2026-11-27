using GarmentERP.Domain.Common;

namespace GarmentERP.Domain.Entities;

public enum LoyaltyTransactionType
{
    Earned,
    Redeemed,
    Expired,
    Adjusted
}

public class LoyaltyTransaction : BaseEntity
{
    public Guid CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public Guid? SaleId { get; set; }
    public Sale? Sale { get; set; }
    public LoyaltyTransactionType TransactionType { get; set; }
    public int Points { get; set; }
    public decimal EquivalentAmount { get; set; }
    public string Description { get; set; } = string.Empty;
}

public class CreditNote : BaseEntity
{
    public string NoteNumber { get; set; } = string.Empty;
    public Guid CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public Guid OriginalSaleId { get; set; }
    public Sale? OriginalSale { get; set; }
    public decimal Amount { get; set; }
    public decimal BalanceAmount { get; set; }
    public DateTime ExpiryDate { get; set; }
    public bool IsRedeemed => BalanceAmount <= 0;
}

public class ReturnInvoice : BaseEntity
{
    public string ReturnNumber { get; set; } = string.Empty;
    public Guid OriginalSaleId { get; set; }
    public Sale? OriginalSale { get; set; }
    public Guid BranchId { get; set; }
    public Guid CashierShiftId { get; set; }
    public decimal TotalRefundAmount { get; set; }
    public string Reason { get; set; } = "Customer exchange/return";
    public ICollection<ReturnItem> Items { get; set; } = new List<ReturnItem>();
}

public class ReturnItem : BaseEntity
{
    public Guid ReturnInvoiceId { get; set; }
    public ReturnInvoice? ReturnInvoice { get; set; }
    public Guid VariantId { get; set; }
    public ProductVariant? Variant { get; set; }
    public decimal Quantity { get; set; }
    public decimal RefundAmount { get; set; }
    public string Condition { get; set; } = "Good / Resellable";
}

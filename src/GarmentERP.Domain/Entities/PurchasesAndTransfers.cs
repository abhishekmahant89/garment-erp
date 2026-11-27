using GarmentERP.Domain.Common;

namespace GarmentERP.Domain.Entities;

public class Supplier : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string ContactPerson { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string Gstin { get; set; } = string.Empty;
    public decimal CurrentBalance { get; set; } = 0; // Outstanding balance payable to supplier
}

public enum PurchaseStatus
{
    Draft,
    Received,
    PartiallyPaid,
    Paid,
    Cancelled
}

public class PurchaseInvoice : BaseEntity
{
    public string InvoiceNumber { get; set; } = string.Empty; // Supplier's bill no
    public string ErpPurchaseNumber { get; set; } = string.Empty; // PO-YYYYMMDD-XXXX
    public Guid SupplierId { get; set; }
    public Supplier? Supplier { get; set; }
    public Guid BranchId { get; set; }
    public Branch? Branch { get; set; }
    public Guid WarehouseId { get; set; }
    public Warehouse? Warehouse { get; set; }
    public DateTime InvoiceDate { get; set; } = DateTime.UtcNow;
    public DateTime ReceivedDate { get; set; } = DateTime.UtcNow;

    public decimal SubTotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal BalanceAmount => TotalAmount - PaidAmount;

    public PurchaseStatus Status { get; set; } = PurchaseStatus.Received;
    public string? Notes { get; set; }

    public ICollection<PurchaseItem> Items { get; set; } = new List<PurchaseItem>();
}

public class PurchaseItem : BaseEntity
{
    public Guid PurchaseInvoiceId { get; set; }
    public PurchaseInvoice? PurchaseInvoice { get; set; }
    public Guid VariantId { get; set; }
    public ProductVariant? Variant { get; set; }

    public decimal QuantityReceived { get; set; }
    public decimal UnitCostPrice { get; set; }
    public decimal MRP { get; set; }
    public decimal SellingPrice { get; set; }
    public decimal TaxRate { get; set; } // 5%, 12%
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }
}

public enum TransferStatus
{
    Draft,
    Dispatched,
    InTransit,
    Received,
    Cancelled
}

public class StockTransfer : BaseEntity
{
    public string TransferNumber { get; set; } = string.Empty; // TRF-YYYYMMDD-XXXX
    public Guid SourceWarehouseId { get; set; }
    public Warehouse? SourceWarehouse { get; set; }
    public Guid DestinationWarehouseId { get; set; }
    public Warehouse? DestinationWarehouse { get; set; }

    public DateTime DispatchedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReceivedAt { get; set; }
    public TransferStatus Status { get; set; } = TransferStatus.Draft;
    public string? DispatchedBy { get; set; }
    public string? ReceivedBy { get; set; }
    public string? Notes { get; set; }

    public ICollection<StockTransferItem> Items { get; set; } = new List<StockTransferItem>();
}

public class StockTransferItem : BaseEntity
{
    public Guid StockTransferId { get; set; }
    public StockTransfer? StockTransfer { get; set; }
    public Guid VariantId { get; set; }
    public ProductVariant? Variant { get; set; }

    public decimal QuantityDispatched { get; set; }
    public decimal QuantityReceived { get; set; }
}

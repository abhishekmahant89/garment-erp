using GarmentERP.Domain.Common;

namespace GarmentERP.Domain.Entities;

public class Branch : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string Pincode { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Gstin { get; set; } = string.Empty;
    public bool IsHeadOffice { get; set; } = false;

    public ICollection<Warehouse> Warehouses { get; set; } = new List<Warehouse>();
}

public class Warehouse : BaseEntity
{
    public Guid BranchId { get; set; }
    public Branch? Branch { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string LocationType { get; set; } = "StoreFloor"; // StoreFloor, BackStore, CentralWarehouse
    public ICollection<Stock> Stocks { get; set; } = new List<Stock>();
}

public class Stock : BaseEntity
{
    public Guid VariantId { get; set; }
    public ProductVariant? Variant { get; set; }
    public Guid WarehouseId { get; set; }
    public Warehouse? Warehouse { get; set; }
    public Guid BranchId { get; set; }
    public Branch? Branch { get; set; }
    public decimal Quantity { get; set; } = 0;
    public decimal ReservedQuantity { get; set; } = 0;
    public decimal AvailableQuantity => Quantity - ReservedQuantity;
}

public enum StockMovementType
{
    InitialStock,
    Purchase,
    Sale,
    CustomerReturn,
    SupplierReturn,
    TransferIn,
    TransferOut,
    Adjustment
}

public class StockMovement : BaseEntity
{
    public Guid VariantId { get; set; }
    public ProductVariant? Variant { get; set; }
    public Guid WarehouseId { get; set; }
    public Warehouse? Warehouse { get; set; }
    public StockMovementType MovementType { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public string? ReferenceNumber { get; set; } // InvoiceNo, PO Number, TransferNo
    public string? Notes { get; set; }
}

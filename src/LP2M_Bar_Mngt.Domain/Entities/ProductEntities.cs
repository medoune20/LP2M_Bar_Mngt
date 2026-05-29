using LP2M_Bar_Mngt.Domain.Common;

namespace LP2M_Bar_Mngt.Domain.Entities;

public sealed class ProductCategory : Entity
{
    public string Name { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
}

public sealed class Product : Entity
{
    public long CategoryId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Sku { get; private set; }
    public string? Barcode { get; private set; }
    public long SalePriceCents { get; private set; }
    public long CostPriceCents { get; private set; }
    public bool IsStockManaged { get; private set; }
    public double LowStockThreshold { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public bool IsLowStock(double currentQuantity)
    {
        return IsStockManaged && currentQuantity <= LowStockThreshold;
    }
}

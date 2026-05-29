using LP2M_Bar_Mngt.Domain.Common;
using LP2M_Bar_Mngt.Domain.Enums;

namespace LP2M_Bar_Mngt.Domain.Entities;

public sealed class StockLevel
{
    public long ProductId { get; private set; }
    public double Quantity { get; private set; }
}

public sealed class StockMovement : Entity
{
    public long ProductId { get; private set; }
    public StockMovementType MovementType { get; private set; }
    public double Quantity { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public long? SaleId { get; private set; }
    public long UserId { get; private set; }
    public DateTime CreatedAt { get; private set; }
}

using LP2M_Bar_Mngt.Domain.Common;
using LP2M_Bar_Mngt.Domain.Enums;

namespace LP2M_Bar_Mngt.Domain.Entities;

public sealed class Sale : Entity
{
    public string TicketNumber { get; private set; } = string.Empty;
    public long CashSessionId { get; private set; }
    public long CashierId { get; private set; }
    public DateTime SaleDate { get; private set; }
    public long SubtotalAmountCents { get; private set; }
    public long DiscountAmountCents { get; private set; }
    public long TotalAmountCents { get; private set; }
    public PaymentMethod PaymentMethod { get; private set; }
    public SaleStatus Status { get; private set; }
    public DateTime? CancelledAt { get; private set; }
    public long? CancelledBy { get; private set; }
    public string? CancelReason { get; private set; }
}

public sealed class SaleItem : Entity
{
    public long SaleId { get; private set; }
    public long ProductId { get; private set; }
    public string ProductName { get; private set; } = string.Empty;
    public double Quantity { get; private set; }
    public long UnitPriceCents { get; private set; }
    public long DiscountAmountCents { get; private set; }
    public long TotalAmountCents { get; private set; }
}

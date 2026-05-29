using LP2M_Bar_Mngt.Domain.Common;
using LP2M_Bar_Mngt.Domain.Enums;

namespace LP2M_Bar_Mngt.Domain.Entities;

public sealed class CashSession : Entity
{
    public long CashierId { get; private set; }
    public DateTime OpenedAt { get; private set; }
    public DateTime? ClosedAt { get; private set; }
    public long OpeningAmountCents { get; private set; }
    public long ExpectedClosingAmountCents { get; private set; }
    public long? DeclaredClosingAmountCents { get; private set; }
    public long? DifferenceAmountCents { get; private set; }
    public CashSessionStatus Status { get; private set; }
}

public sealed class CashMovement : Entity
{
    public long CashSessionId { get; private set; }
    public CashMovementType MovementType { get; private set; }
    public long AmountCents { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public long UserId { get; private set; }
    public DateTime CreatedAt { get; private set; }
}

using LP2M_Bar_Mngt.Domain.Common;

namespace LP2M_Bar_Mngt.Domain.Entities;

public sealed class ExpenseCategory : Entity
{
    public string Name { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
}

public sealed class Expense : Entity
{
    public long CategoryId { get; private set; }
    public long? CashSessionId { get; private set; }
    public long UserId { get; private set; }
    public long AmountCents { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public DateTime ExpenseDate { get; private set; }
    public bool PaidFromCashRegister { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public string Status { get; private set; } = string.Empty;
}

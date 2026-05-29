namespace LP2M_Bar_Mngt.Domain.Enums;

public enum CashMovementType
{
    SalePayment = 1,
    Expense = 2,
    ManualIn = 3,
    ManualOut = 4
}

public enum CashSessionStatus
{
    Open = 1,
    Closed = 2,
    Cancelled = 3
}

public enum PaymentMethod
{
    Cash = 1,
    Card = 2,
    MobileMoney = 3,
    Other = 4
}

public enum SaleStatus
{
    Completed = 1,
    Cancelled = 2
}

public enum StockMovementType
{
    In = 1,
    OutSale = 2,
    OutManual = 3,
    Adjustment = 4,
    Return = 5
}

namespace LP2M_Bar_Mngt.Application.DTOs;

public sealed record DashboardSummary(
    long TodayRevenueCents,
    int TodayTicketCount,
    int OpenCashSessionCount,
    int LowStockCount,
    int ActiveProductCount,
    int ActiveUserCount,
    IReadOnlyList<RecentSaleDto> RecentSales,
    IReadOnlyList<LowStockAlertDto> LowStockAlerts)
{
    public static DashboardSummary Empty { get; } = new(
        0,
        0,
        0,
        0,
        0,
        0,
        Array.Empty<RecentSaleDto>(),
        Array.Empty<LowStockAlertDto>());

    public decimal TodayRevenue => TodayRevenueCents / 100m;
}

public sealed record RecentSaleDto(
    string TicketNumber,
    DateTime SaleDate,
    string CashierName,
    decimal TotalAmount,
    string PaymentMethod);

public sealed record LowStockAlertDto(
    string ProductName,
    string CategoryName,
    double Quantity,
    double Threshold);

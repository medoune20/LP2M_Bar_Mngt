namespace LP2M_Bar_Mngt.Application.DTOs;

public sealed record WebDashboardPointDto(string Label, decimal Amount, int Count);

public sealed record WebTopProductDto(string ProductName, double QuantitySold, decimal TotalAmount);

public sealed record WebDashboardDto(
    decimal TodayRevenue,
    int TodayTicketCount,
    int OpenCashSessionCount,
    int LowStockCount,
    int ProductCount,
    int UserCount,
    decimal TodayExpenseTotal,
    decimal OpenCashBalance,
    decimal EstimatedProfit,
    IReadOnlyList<WebDashboardPointDto> SalesChart,
    IReadOnlyList<WebTopProductDto> TopProducts);

public sealed record WebDataSetDto(
    BusinessProfileDto BusinessProfile,
    WebDashboardDto Dashboard,
    IReadOnlyList<CategoryDto> Categories,
    IReadOnlyList<CategoryDto> ExpenseCategories,
    IReadOnlyList<RoleDto> Roles,
    IReadOnlyList<ProductDto> Products,
    IReadOnlyList<StockItemDto> Stock,
    IReadOnlyList<ExpenseDto> Expenses,
    IReadOnlyList<UserDto> Users,
    IReadOnlyList<SaleDto> Sales,
    IReadOnlyList<CashSessionDto> CashSessions);

public sealed record BusinessProfileDto(
    string Name,
    string Sigle,
    string Address,
    string Contact,
    string? LogoData,
    string? CoverImageData,
    string TicketFooter);

public sealed record BusinessProfileSaveRequest(
    string Name,
    string Sigle,
    string Address,
    string Contact,
    string? LogoData,
    string? CoverImageData,
    string TicketFooter);

public sealed record CategoryDto(long Id, string Name, bool IsActive, bool IsHidden, string? ImageData);

public sealed record RoleDto(long Id, string Name);

public sealed record ProductDto(
    long Id,
    long CategoryId,
    string CategoryName,
    string Name,
    string? Sku,
    string? Barcode,
    decimal SalePrice,
    decimal CostPrice,
    bool IsStockManaged,
    double LowStockThreshold,
    double Quantity,
    bool IsActive,
    bool IsHidden,
    string? ImageData);

public sealed record StockItemDto(
    long ProductId,
    string ProductName,
    string CategoryName,
    double Quantity,
    double LowStockThreshold,
    bool IsLowStock,
    bool IsHidden);

public sealed record ExpenseDto(
    long Id,
    string CategoryName,
    string Description,
    decimal Amount,
    bool PaidFromCashRegister,
    DateTime ExpenseDate,
    string Status,
    bool IsHidden);

public sealed record UserDto(
    long Id,
    string Username,
    string FullName,
    long RoleId,
    string RoleName,
    bool IsActive,
    DateTime CreatedAt,
    bool IsHidden,
    bool TwoFactorEnabled);

public sealed record SaleDto(
    long Id,
    string TicketNumber,
    string CashierName,
    string CustomerName,
    decimal TotalAmount,
    string PaymentMethod,
    string Status,
    DateTime SaleDate,
    bool IsHidden);

public sealed record CashSessionDto(
    long Id,
    string CashierName,
    DateTime OpenedAt,
    DateTime? ClosedAt,
    decimal OpeningAmount,
    decimal ExpectedClosingAmount,
    decimal? DeclaredClosingAmount,
    decimal? DifferenceAmount,
    string Status,
    bool IsHidden);

public sealed record CashOpenRequest(long CashierId, decimal OpeningAmount);

public sealed record CashCloseRequest(long CashSessionId, decimal DeclaredClosingAmount);

public sealed record SaleCreateRequest(
    long CashSessionId,
    long ProductId,
    double Quantity,
    int PaymentMethod,
    decimal DiscountAmount);

public sealed record SaleCartItemRequest(long ProductId, double Quantity);

public sealed record SaleCartCreateRequest(
    long CashSessionId,
    string? CustomerName,
    int PaymentMethod,
    decimal DiscountAmount,
    IReadOnlyList<SaleCartItemRequest> Items);

public sealed record ProductSaveRequest(
    long? Id,
    long CategoryId,
    string Name,
    string? Sku,
    string? Barcode,
    decimal SalePrice,
    decimal CostPrice,
    bool IsStockManaged,
    double LowStockThreshold,
    double InitialQuantity,
    bool IsActive,
    string? ImageData);

public sealed record CategoryCreateRequest(string Name, string? ImageData);

public sealed record StockAdjustmentRequest(long ProductId, double QuantityDelta, string Reason);

public sealed record ExpenseCreateRequest(
    long CategoryId,
    string Description,
    decimal Amount,
    bool PaidFromCashRegister);

public sealed record UserSaveRequest(
    long? Id,
    string Username,
    string FullName,
    long RoleId,
    string? Password,
    bool IsActive,
    bool TwoFactorEnabled,
    bool ResetTwoFactorSecret);

public sealed record TwoFactorSetupDto(
    long UserId,
    string Username,
    string Secret,
    string OtpAuthUri);

public sealed record ObjectVisibilityRequest(string ObjectType, long Id, bool IsHidden);

public sealed record ObjectReferenceDto(string ObjectType, long Id);

public sealed record BulkObjectRequest(IReadOnlyList<ObjectReferenceDto> Objects, bool IsHidden);

public sealed record TicketItemDto(string ProductName, double Quantity, decimal UnitPrice, decimal DiscountAmount, decimal TotalAmount);

public sealed record TicketDto(
    BusinessProfileDto BusinessProfile,
    long SaleId,
    string TicketNumber,
    string CashierName,
    string CustomerName,
    DateTime SaleDate,
    string PaymentMethod,
    decimal SubtotalAmount,
    decimal DiscountAmount,
    decimal TotalAmount,
    IReadOnlyList<TicketItemDto> Items);

namespace LP2M_Bar_Mngt.Application.DTOs;

public sealed record OperationResult(bool Success, string Message);

public sealed record ModuleRowDto(
    string Section,
    string Item,
    string Value,
    string Details);

public sealed record OperationsSnapshot(
    IReadOnlyList<ModuleRowDto> DashboardRows,
    IReadOnlyList<ModuleRowDto> CashRows,
    IReadOnlyList<ModuleRowDto> SalesRows,
    IReadOnlyList<ModuleRowDto> ProductRows,
    IReadOnlyList<ModuleRowDto> StockRows,
    IReadOnlyList<ModuleRowDto> ExpenseRows,
    IReadOnlyList<ModuleRowDto> ReportRows,
    IReadOnlyList<ModuleRowDto> UserRows)
{
    public static OperationsSnapshot Empty { get; } = new(
        Array.Empty<ModuleRowDto>(),
        Array.Empty<ModuleRowDto>(),
        Array.Empty<ModuleRowDto>(),
        Array.Empty<ModuleRowDto>(),
        Array.Empty<ModuleRowDto>(),
        Array.Empty<ModuleRowDto>(),
        Array.Empty<ModuleRowDto>(),
        Array.Empty<ModuleRowDto>());
}

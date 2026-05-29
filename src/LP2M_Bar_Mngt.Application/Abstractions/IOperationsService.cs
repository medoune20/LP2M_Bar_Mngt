using LP2M_Bar_Mngt.Application.DTOs;

namespace LP2M_Bar_Mngt.Application.Abstractions;

public interface IOperationsService
{
    Task<OperationsSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default);
    Task<OperationResult> OpenCashSessionAsync(CancellationToken cancellationToken = default);
    Task<OperationResult> CloseCashSessionAsync(CancellationToken cancellationToken = default);
    Task<OperationResult> CreateSaleAsync(CancellationToken cancellationToken = default);
    Task<OperationResult> ReprintLastTicketAsync(CancellationToken cancellationToken = default);
    Task<OperationResult> CancelLastSaleAsync(CancellationToken cancellationToken = default);
    Task<OperationResult> AddProductAsync(CancellationToken cancellationToken = default);
    Task<OperationResult> AddCategoryAsync(CancellationToken cancellationToken = default);
    Task<OperationResult> UpdateProductPriceAsync(CancellationToken cancellationToken = default);
    Task<OperationResult> RestockLowProductsAsync(CancellationToken cancellationToken = default);
    Task<OperationResult> AdjustStockAsync(CancellationToken cancellationToken = default);
    Task<OperationResult> CheckStockAlertsAsync(CancellationToken cancellationToken = default);
    Task<OperationResult> RecordExpenseAsync(CancellationToken cancellationToken = default);
    Task<OperationResult> RecordCashExpenseAsync(CancellationToken cancellationToken = default);
    Task<OperationResult> GenerateDailyReportAsync(CancellationToken cancellationToken = default);
    Task<OperationResult> ExportDailyReportAsync(CancellationToken cancellationToken = default);
    Task<OperationResult> AddCashierUserAsync(CancellationToken cancellationToken = default);
    Task<OperationResult> ResetAdminPasswordAsync(CancellationToken cancellationToken = default);
    Task<OperationResult> WriteAuditEntryAsync(CancellationToken cancellationToken = default);
}

using LP2M_Bar_Mngt.Application.DTOs;

namespace LP2M_Bar_Mngt.Application.Abstractions;

public interface IWebManagementService
{
    Task<WebDashboardDto> GetDashboardAsync(CancellationToken cancellationToken = default);
    Task<WebDataSetDto> GetDataSetAsync(CancellationToken cancellationToken = default);
    Task<BusinessProfileDto> GetBusinessProfileAsync(CancellationToken cancellationToken = default);
    Task<BusinessProfileDto> SaveBusinessProfileAsync(BusinessProfileSaveRequest request, CancellationToken cancellationToken = default);
    Task<TicketDto> GetTicketAsync(long? saleId, CancellationToken cancellationToken = default);
    Task<OperationResult> OpenCashSessionAsync(CashOpenRequest request, CancellationToken cancellationToken = default);
    Task<OperationResult> CloseCashSessionAsync(CashCloseRequest request, CancellationToken cancellationToken = default);
    Task<OperationResult> CreateSaleAsync(SaleCreateRequest request, CancellationToken cancellationToken = default);
    Task<OperationResult> CreateSaleCartAsync(SaleCartCreateRequest request, CancellationToken cancellationToken = default);
    Task<ProductDto> SaveProductAsync(ProductSaveRequest request, CancellationToken cancellationToken = default);
    Task<CategoryDto> CreateCategoryAsync(CategoryCreateRequest request, CancellationToken cancellationToken = default);
    Task<StockItemDto> AdjustStockAsync(StockAdjustmentRequest request, CancellationToken cancellationToken = default);
    Task<ExpenseDto> CreateExpenseAsync(ExpenseCreateRequest request, CancellationToken cancellationToken = default);
    Task<UserDto> SaveUserAsync(UserSaveRequest request, CancellationToken cancellationToken = default);
    Task<TwoFactorSetupDto> ResetTwoFactorSecretAsync(long userId, CancellationToken cancellationToken = default);
    Task<OperationResult> SetProductActiveAsync(long productId, bool isActive, CancellationToken cancellationToken = default);
    Task<OperationResult> SetUserActiveAsync(long userId, bool isActive, CancellationToken cancellationToken = default);
    Task<OperationResult> SetObjectHiddenAsync(ObjectVisibilityRequest request, CancellationToken cancellationToken = default);
    Task<OperationResult> SetObjectsHiddenAsync(BulkObjectRequest request, CancellationToken cancellationToken = default);
    Task<OperationResult> DeleteObjectsAsync(IReadOnlyList<ObjectReferenceDto> objects, CancellationToken cancellationToken = default);
    Task<OperationResult> DeleteObjectAsync(string objectType, long id, CancellationToken cancellationToken = default);
}

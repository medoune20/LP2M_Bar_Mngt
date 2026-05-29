using LP2M_Bar_Mngt.Application.DTOs;

namespace LP2M_Bar_Mngt.Application.Abstractions;

public interface IDashboardReadService
{
    Task<DashboardSummary> GetSummaryAsync(CancellationToken cancellationToken = default);
}

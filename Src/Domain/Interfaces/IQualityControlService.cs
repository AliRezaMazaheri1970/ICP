using Domain.Enums;
using Domain.Models;

namespace Application.Services.Interfaces;

public interface IQualityControlService
{
    Task<int> RunCheckAsync(Guid projectId, CheckType checkType, CancellationToken cancellationToken = default);
    Task<int> RunAllChecksAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task<ProjectQualitySummary> GetSummaryAsync(Guid projectId, CancellationToken cancellationToken = default);
}
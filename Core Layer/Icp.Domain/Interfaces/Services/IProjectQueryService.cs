using Core.Icp.Domain.Models.Projects;

namespace Core.Icp.Domain.Interfaces.Services
{
    /// <summary>
    /// سرویس فقط-خواندنی برای کوئری‌های پروژه (لیست صفحه‌بندی‌شده، جزئیات و ...).
    /// </summary>
    public interface IProjectQueryService
    {
        /// <summary>
        /// لیست صفحه‌بندی‌شده پروژه‌ها.
        /// </summary>
        Task<PagedProjectListResult> GetPagedAsync(
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// جزئیات یک پروژه (به‌همراه Samples).
        /// </summary>
        Task<ProjectDetailsResult?> GetDetailsAsync(
            Guid id,
            CancellationToken cancellationToken = default);
    }
}

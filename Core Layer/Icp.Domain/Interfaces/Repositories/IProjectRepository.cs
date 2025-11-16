using Core.Icp.Domain.Entities.Projects;
using Core.Icp.Domain.Enums;

namespace Core.Icp.Domain.Interfaces.Repositories
{
    public interface IProjectRepository : IRepository<Project>
    {
        Task<(IReadOnlyList<Project> Items, int TotalCount)> GetPagedAsync(
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken = default);

        Task<IEnumerable<Project>> GetRecentProjectsAsync(
            int count,
            CancellationToken cancellationToken = default);

        Task<IEnumerable<Project>> GetByStatusAsync(
            ProjectStatus status,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// دریافت پروژه همراه با نمونه‌ها (Samples).
        /// (متد قدیمی که هنوز بعضی سرویس‌ها از آن استفاده می‌کنند.)
        /// </summary>
        Task<Project?> GetWithSamplesAsync(
            Guid id,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// دریافت پروژه همراه با نمونه‌ها (هم‌معنی GetWithSamplesAsync).
        /// </summary>
        Task<Project?> GetByIdWithSamplesAsync(
            Guid id,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// دریافت پروژه با جزئیات کامل (نمونه‌ها + اندازه‌گیری‌ها).
        /// </summary>
        Task<Project?> GetWithFullDetailsAsync(
            Guid id,
            CancellationToken cancellationToken = default);
    }

}
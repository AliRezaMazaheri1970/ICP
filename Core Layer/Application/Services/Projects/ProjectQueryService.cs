using Core.Icp.Domain.Interfaces.Repositories;
using Core.Icp.Domain.Interfaces.Services;
using Core.Icp.Domain.Models.Projects;

namespace Core.Icp.Application.Services.Projects
{
    /// <summary>
    /// سرویس فقط-خواندنی برای کوئری‌های پروژه (لیست، جزئیات و ...).
    /// </summary>
    public class ProjectQueryService : IProjectQueryService
    {
        private readonly IUnitOfWork _unitOfWork;

        public ProjectQueryService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        /// <inheritdoc />
        public async Task<PagedProjectListResult> GetPagedAsync(
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            var (items, totalCount) = await _unitOfWork.Projects
                .GetPagedAsync(pageNumber, pageSize, cancellationToken);

            return new PagedProjectListResult
            {
                Items = items,
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize
            };
        }

        /// <inheritdoc />
        public async Task<ProjectDetailsResult?> GetDetailsAsync(
            Guid id,
            CancellationToken cancellationToken = default)
        {
            var project = await _unitOfWork.Projects
                .GetByIdWithSamplesAsync(id, cancellationToken);

            if (project == null)
                return null;

            return new ProjectDetailsResult
            {
                Project = project
            };
        }
    }
}

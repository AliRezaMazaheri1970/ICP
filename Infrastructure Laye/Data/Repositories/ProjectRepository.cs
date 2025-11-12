using Core.Icp.Domain.Entities.Projects;
using Core.Icp.Domain.Enums;
using Core.Icp.Domain.Interfaces.Repositories;
using Infrastructure.Icp.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Icp.Data.Repositories
{
    /// <summary>
    ///     Repository for Project entities, extending the base repository.
    /// </summary>
    public class ProjectRepository : BaseRepository<Project>, IProjectRepository
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="ProjectRepository" /> class.
        /// </summary>
        /// <param name="context">The database context.</param>
        public ProjectRepository(ICPDbContext context) : base(context)
        {
        }

        /// <inheritdoc />
        public async Task<IEnumerable<Project>> GetByStatusAsync(
            ProjectStatus status,
            CancellationToken cancellationToken = default)
        {
            return await _dbSet
                .Where(p => !p.IsDeleted && p.Status == status)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync(cancellationToken);
        }

        /// <inheritdoc />
        public async Task<Project?> GetWithSamplesAsync(
            Guid id,
            CancellationToken cancellationToken = default)
        {
            return await _dbSet
                .Include(p => p.Samples)
                .FirstOrDefaultAsync(p => !p.IsDeleted && p.Id == id, cancellationToken);
        }

        /// <inheritdoc />
        public async Task<Project?> GetWithFullDetailsAsync(
            Guid id,
            CancellationToken cancellationToken = default)
        {
            return await _dbSet
                .Include(p => p.Samples)
                    .ThenInclude(s => s.Measurements)
                        .ThenInclude(m => m.Element)
                .Include(p => p.Samples)
                    .ThenInclude(s => s.QualityChecks)
                .Include(p => p.CalibrationCurves)
                    .ThenInclude(c => c.Points) // Corrected from CalibrationPoints to Points
                .FirstOrDefaultAsync(p => !p.IsDeleted && p.Id == id, cancellationToken);
        }

        /// <inheritdoc />
        public async Task<IEnumerable<Project>> GetRecentProjectsAsync(
            int count,
            CancellationToken cancellationToken = default)
        {
            return await _dbSet
                .Where(p => !p.IsDeleted)
                .OrderByDescending(p => p.CreatedAt)
                .Take(count)
                .ToListAsync(cancellationToken);
        }
    }
}
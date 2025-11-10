using Core.Icp.Domain.Entities.Projects;
using Shared.Icp.DTOs.Projects;

namespace Shared.Icp.Helpers.Mappers
{
    /// <summary>
    /// Mapper برای Project
    /// </summary>
    public static class ProjectMapper
    {
        /// <summary>
        /// Entity به DTO
        /// </summary>
        public static ProjectDto ToDto(this Project project)
        {
            return new ProjectDto
            {
                Id = project.Id,
                Name = project.Name,
                Description = project.Description,
                SourceFileName = project.SourceFileName,
                StartDate = project.StartDate,
                EndDate = project.EndDate,
                Status = project.Status,
                SampleCount = project.Samples?.Count ?? 0,
                CalibrationCurveCount = project.CalibrationCurves?.Count ?? 0,
                CreatedBy = project.CreatedBy,
                CreatedAt = project.CreatedAt,
                UpdatedAt = project.UpdatedAt
            };
        }

        /// <summary>
        /// لیست Entity به لیست DTO
        /// </summary>
        public static IEnumerable<ProjectDto> ToDtoList(this IEnumerable<Project> projects)
        {
            return projects.Select(p => p.ToDto());
        }

        /// <summary>
        /// CreateDTO به Entity
        /// </summary>
        public static Project ToEntity(this CreateProjectDto dto)
        {
            return new Project
            {
                Name = dto.Name,
                Description = dto.Description,
                SourceFileName = dto.SourceFileName,
                StartDate = dto.StartDate
            };
        }

        /// <summary>
        /// به‌روزرسانی Entity از UpdateDTO
        /// </summary>
        public static void UpdateFromDto(this Project project, UpdateProjectDto dto)
        {
            project.Name = dto.Name;
            project.Description = dto.Description;
            project.EndDate = dto.EndDate;
            project.Status = dto.Status;
            project.UpdatedAt = DateTime.UtcNow;
        }
    }
}
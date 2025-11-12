using Core.Icp.Domain.Entities.Projects;
using Core.Icp.Domain.Enums;
using Shared.Icp.DTOs.Projects;
using Shared.Icp.Helpers.Extensions;

namespace Shared.Icp.Helpers.Mappers
{
    /// <summary>
    /// Provides mapping methods to convert between Project domain entities and their corresponding DTOs.
    /// </summary>
    public static class ProjectMapper
    {
        /// <summary>
        /// Maps a <see cref="Project"/> entity to a <see cref="ProjectDto"/>.
        /// </summary>
        /// <param name="project">The project entity to map.</param>
        /// <returns>A new <see cref="ProjectDto"/> instance.</returns>
        /// <exception cref="ArgumentNullException">Thrown if the project is null.</exception>
        public static ProjectDto ToDto(this Project project)
        {
            if (project == null) throw new ArgumentNullException(nameof(project));

            return new ProjectDto
            {
                Id = project.Id,
                Name = project.Name,
                Description = project.Description,
                SourceFileName = project.SourceFileName,
                Status = project.Status.ToString(),
                CreatedAt = project.CreatedAt,
                UpdatedAt = project.UpdatedAt,
                SampleCount = project.Samples?.Count ?? 0
            };
        }

        /// <summary>
        /// Maps a <see cref="Project"/> entity to a <see cref="ProjectSummaryDto"/>.
        /// </summary>
        /// <param name="project">The project entity to map.</param>
        /// <returns>A new <see cref="ProjectSummaryDto"/> instance.</returns>
        /// <exception cref="ArgumentNullException">Thrown if the project is null.</exception>
        public static ProjectSummaryDto ToSummaryDto(this Project project)
        {
            if (project == null) throw new ArgumentNullException(nameof(project));

            return new ProjectSummaryDto
            {
                Id = project.Id,
                Name = project.Name,
                Description = project.Description,
                Status = project.Status.ToString(),
                SampleCount = project.Samples?.Count ?? 0,
                CreatedAt = project.CreatedAt,
                UpdatedAt = project.UpdatedAt
            };
        }

        /// <summary>
        /// Maps a <see cref="CreateProjectDto"/> to a new <see cref="Project"/> entity.
        /// </summary>
        /// <param name="dto">The DTO to map from.</param>
        /// <returns>A new <see cref="Project"/> entity.</returns>
        /// <exception cref="ArgumentNullException">Thrown if the DTO is null.</exception>
        public static Project ToEntity(this CreateProjectDto dto)
        {
            if (dto == null) throw new ArgumentNullException(nameof(dto));

            return new Project
            {
                Name = dto.Name,
                Description = dto.Description,
                SourceFileName = dto.SourceFileName,
                Status = ProjectStatus.Created // Default status for new projects
            };
        }

        /// <summary>
        /// Updates an existing <see cref="Project"/> entity from an <see cref="UpdateProjectDto"/>.
        /// </summary>
        /// <param name="project">The project entity to update.</param>
        /// <param name="dto">The DTO containing the updated values.</param>
        /// <exception cref="ArgumentNullException">Thrown if the project or DTO is null.</exception>
        public static void UpdateFromDto(this Project project, UpdateProjectDto dto)
        {
            if (project == null) throw new ArgumentNullException(nameof(project));
            if (dto == null) throw new ArgumentNullException(nameof(dto));

            // Update main fields
            if (!string.IsNullOrWhiteSpace(dto.Name))
                project.Name = dto.Name;

            project.Description = dto.Description;
            project.SourceFileName = dto.SourceFileName;

            // Convert status from string to enum
            if (!string.IsNullOrWhiteSpace(dto.Status))
            {
                if (Enum.TryParse<ProjectStatus>(dto.Status, ignoreCase: true, out var status))
                {
                    project.Status = status;
                }
            }

            // Update timestamp
            project.UpdatedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// Maps a collection of <see cref="Project"/> entities to a list of <see cref="ProjectDto"/>s.
        /// </summary>
        /// <param name="projects">The collection of project entities.</param>
        /// <returns>A list of <see cref="ProjectDto"/>s.</returns>
        public static List<ProjectDto> ToDtoList(this IEnumerable<Project> projects)
        {
            return projects?.Select(p => p.ToDto()).ToList() ?? new List<ProjectDto>();
        }

        /// <summary>
        /// Maps a collection of <see cref="Project"/> entities to a list of <see cref="ProjectSummaryDto"/>s.
        /// </summary>
        /// <param name="projects">The collection of project entities.</param>
        /// <returns>A list of <see cref="ProjectSummaryDto"/>s.</returns>
        public static List<ProjectSummaryDto> ToSummaryDtoList(this IEnumerable<Project> projects)
        {
            return projects?.Select(p => p.ToSummaryDto()).ToList() ?? new List<ProjectSummaryDto>();
        }
    }
}
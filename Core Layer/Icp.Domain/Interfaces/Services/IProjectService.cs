using Core.Icp.Domain.Entities.Projects;

namespace Core.Icp.Domain.Interfaces.Services
{
    /// <summary>
    /// Defines the contract for a service that manages analysis projects.
    /// </summary>
    public interface IProjectService
    {
        /// <summary>
        /// Asynchronously retrieves all projects.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation. The task result contains a collection of all projects.</returns>
        Task<IEnumerable<Project>> GetAllProjectsAsync();

        /// <summary>
        /// Asynchronously retrieves a specific project by its unique identifier.
        /// </summary>
        /// <param name="id">The unique identifier of the project.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the found project or null if not found.</returns>
        Task<Project?> GetProjectByIdAsync(int id);

        /// <summary>
        /// Asynchronously creates a new project.
        /// </summary>
        /// <param name="project">The project entity to create.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the newly created project.</returns>
        Task<Project> CreateProjectAsync(Project project);

        /// <summary>
        /// Asynchronously updates an existing project.
        /// </summary>
        /// <param name="project">The project entity with updated information.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the updated project.</returns>
        Task<Project> UpdateProjectAsync(Project project);

        /// <summary>
        /// Asynchronously deletes a project by its unique identifier.
        /// </summary>
        /// <param name="id">The unique identifier of the project to delete.</param>
        /// <returns>A task that represents the asynchronous operation. The task result is true if the deletion was successful; otherwise, false.</returns>
        Task<bool> DeleteProjectAsync(int id);

        /// <summary>
        /// Asynchronously saves a project's data to a specified file.
        /// </summary>
        /// <param name="projectId">The unique identifier of the project to save.</param>
        /// <param name="filePath">The full path of the file where the project will be saved.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the path to the saved file.</returns>
        Task<string> SaveProjectToFileAsync(int projectId, string filePath);

        /// <summary>
        /// Asynchronously loads a project from a specified file.
        /// </summary>
        /// <param name="filePath">The full path of the file to load the project from.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the loaded project entity.</returns>
        Task<Project> LoadProjectFromFileAsync(string filePath);
    }
}
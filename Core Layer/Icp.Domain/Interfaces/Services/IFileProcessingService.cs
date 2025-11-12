using Core.Icp.Domain.Entities.Projects;
using Core.Icp.Domain.Entities.Samples;

namespace Core.Icp.Domain.Interfaces.Services
{
    /// <summary>
    /// Defines the contract for a service that processes data files like CSV and Excel.
    /// </summary>
    public interface IFileProcessingService
    {
        /// <summary>
        /// Asynchronously reads and processes a CSV file, creating a new project with its data.
        /// </summary>
        /// <param name="filePath">The path to the CSV file.</param>
        /// <param name="projectName">The name to assign to the new project.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the newly created <see cref="Project"/> entity.</returns>
        Task<Project> ProcessCsvFileAsync(string filePath, string projectName);

        /// <summary>
        /// Asynchronously reads and processes an Excel file, creating a new project with its data.
        /// </summary>
        /// <param name="filePath">The path to the Excel file.</param>
        /// <param name="projectName">The name to assign to the new project.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the newly created <see cref="Project"/> entity.</returns>
        Task<Project> ProcessExcelFileAsync(string filePath, string projectName);

        /// <summary>
        /// Asynchronously detects the format of a file (e.g., "CSV", "Excel") based on its content or extension.
        /// </summary>
        /// <param name="filePath">The path to the file to inspect.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains a string identifying the detected file format.</returns>
        Task<string> DetectFileFormatAsync(string filePath);

        /// <summary>
        /// Asynchronously validates the structure and content of a data file to ensure it can be processed.
        /// </summary>
        /// <param name="filePath">The path to the file to validate.</param>
        /// <returns>A task that represents the asynchronous operation. The task result is true if the file is valid; otherwise, false.</returns>
        Task<bool> ValidateFileAsync(string filePath);

        /// <summary>
        /// Asynchronously extracts sample data from a given file.
        /// </summary>
        /// <param name="filePath">The path to the file from which to extract samples.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains a collection of <see cref="Sample"/> entities extracted from the file.</returns>
        Task<IEnumerable<Sample>> ExtractSamplesFromFileAsync(string filePath);
    }
}
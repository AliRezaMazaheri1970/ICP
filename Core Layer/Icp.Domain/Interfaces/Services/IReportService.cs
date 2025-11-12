namespace Core.Icp.Domain.Interfaces.Services
{
    /// <summary>
    /// Defines the contract for a service that generates various types of reports.
    /// </summary>
    public interface IReportService
    {
        /// <summary>
        /// Asynchronously generates a PDF report for a specific project.
        /// </summary>
        /// <param name="projectId">The unique identifier of the project for which to generate the report.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the generated PDF report as a byte array.</returns>
        Task<byte[]> GeneratePdfReportAsync(int projectId);

        /// <summary>
        /// Asynchronously generates an Excel report for a specific project.
        /// </summary>
        /// <param name="projectId">The unique identifier of the project for which to generate the report.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the generated Excel report as a byte array.</returns>
        Task<byte[]> GenerateExcelReportAsync(int projectId);

        /// <summary>
        /// Asynchronously exports the results of a project to a CSV file.
        /// </summary>
        /// <param name="projectId">The unique identifier of the project whose results are to be exported.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the CSV file content as a byte array.</returns>
        Task<byte[]> ExportResultsToCsvAsync(int projectId);

        /// <summary>
        /// Asynchronously generates a quality control report for a specific project.
        /// </summary>
        /// <param name="projectId">The unique identifier of the project for which to generate the quality control report.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the generated quality control report as a byte array.</returns>
        Task<byte[]> GenerateQualityControlReportAsync(int projectId);
    }
}
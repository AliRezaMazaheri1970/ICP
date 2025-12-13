namespace Application.Services;

/// <summary>
/// Defines services for generating reports and exporting project data.
/// </summary>
public interface IReportService
{
    /// <summary>
    /// Generates a report based on the specified request configuration.
    /// </summary>
    /// <param name="request">The report generation parameters.</param>
    /// <returns>The generated report file details.</returns>
    Task<Result<ReportResultDto>> GenerateReportAsync(ReportRequest request);

    /// <summary>
    /// Exports specific data columns to a requested format (e.g., Excel/CSV) as a byte array.
    /// </summary>
    /// <param name="request">The export parameters.</param>
    /// <returns>The exported file content.</returns>
    Task<Result<byte[]>> ExportDataAsync(ExportRequest request);

    /// <summary>
    /// Exports the full project report to an Excel file with multiple sheets.
    /// </summary>
    /// <param name="projectId">The project identifier.</param>
    /// <param name="options">Optional export options.</param>
    /// <returns>The Excel file content.</returns>
    Task<Result<byte[]>> ExportToExcelAsync(Guid projectId, ReportOptions? options = null);

    /// <summary>
    /// Exports the project data to a CSV file.
    /// </summary>
    /// <param name="projectId">The project identifier.</param>
    /// <param name="useOxide">Whether to convert values to oxides.</param>
    /// <returns>The CSV file content.</returns>
    Task<Result<byte[]>> ExportToCsvAsync(Guid projectId, bool useOxide = false);

    /// <summary>
    /// Exports the project data to a JSON file.
    /// </summary>
    /// <param name="projectId">The project identifier.</param>
    /// <returns>The JSON file content.</returns>
    Task<Result<byte[]>> ExportToJsonAsync(Guid projectId);

    /// <summary>
    /// Generates an HTML report for the project.
    /// </summary>
    /// <param name="projectId">The project identifier.</param>
    /// <param name="options">Optional report options.</param>
    /// <returns>The HTML content string.</returns>
    Task<Result<string>> GenerateHtmlReportAsync(Guid projectId, ReportOptions? options = null);

    /// <summary>
    /// Calculates calibration ranges for each element/wavelength based on blank samples.
    /// </summary>
    /// <param name="projectId">The project identifier.</param>
    /// <returns>A dictionary of calibration ranges keyed by element.</returns>
    Task<Result<Dictionary<string, CalibrationRange>>> GetCalibrationRangesAsync(Guid projectId);

    /// <summary>
    /// Selects the best wavelength for each base element per row based on calibration ranges.
    /// </summary>
    /// <param name="request">The selection parameters.</param>
    /// <returns>The result containing best wavelength mappings.</returns>
    Task<Result<BestWavelengthResult>> SelectBestWavelengthsAsync(BestWavelengthRequest request);
}
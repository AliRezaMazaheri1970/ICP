using Application.DTOs;
using Shared.Wrapper;

namespace Application.Services;

/// <summary>
/// Defines services for importing data from various file formats into the system.
/// </summary>
public interface IImportService
{
    /// <summary>
    /// Performs a basic import of a CSV stream into a new project.
    /// </summary>
    /// <param name="csvStream">The source CSV stream.</param>
    /// <param name="projectName">The name for the new project.</param>
    /// <param name="owner">The owner of the project.</param>
    /// <param name="stateJson">Optional initialization state.</param>
    /// <param name="progress">Optional progress reporter.</param>
    /// <returns>The result of the project save operation.</returns>
    Task<Result<ProjectSaveResult>> ImportCsvAsync(
        Stream csvStream,
        string projectName,
        string? owner = null,
        string? stateJson = null,
        IProgress<(int total, int processed)>? progress = null);

    /// <summary>
    /// Performs an advanced import with explicit options and format detection.
    /// </summary>
    /// <param name="fileStream">The source file stream.</param>
    /// <param name="fileName">The original filename.</param>
    /// <param name="request">Options specifying import behavior.</param>
    /// <param name="progress">Optional progress reporter.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The comprehensive import result.</returns>
    Task<Result<AdvancedImportResult>> ImportAdvancedAsync(
        Stream fileStream,
        string fileName,
        AdvancedImportRequest request,
        IProgress<(int total, int processed, string message)>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Analyzes a file stream to detect its format and structure without importing.
    /// </summary>
    /// <param name="fileStream">The source file stream.</param>
    /// <param name="fileName">The original filename.</param>
    /// <returns>The detected format details.</returns>
    Task<Result<FileFormatDetectionResult>> DetectFormatAsync(
        Stream fileStream,
        string fileName);

    /// <summary>
    /// Reads the first few rows of a file to generate a preview.
    /// </summary>
    /// <param name="fileStream">The source file stream.</param>
    /// <param name="fileName">The original filename.</param>
    /// <param name="previewRows">Buffer size for preview rows (default 10).</param>
    /// <returns>A preview result containing headers and sample data.</returns>
    Task<Result<FilePreviewResult>> PreviewFileAsync(
        Stream fileStream,
        string fileName,
        int previewRows = 10);

    /// <summary>
    /// Imports an additional file and appends it to an existing project.
    /// </summary>
    /// <param name="projectId">The target project identifier.</param>
    /// <param name="fileStream">The source file stream.</param>
    /// <param name="fileName">The original filename.</param>
    /// <param name="request">Data import options.</param>
    /// <param name="progress">Optional progress reporter.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The result of the append operation.</returns>
    Task<Result<AdvancedImportResult>> ImportAdditionalAsync(
        Guid projectId,
        Stream fileStream,
        string fileName,
        AdvancedImportRequest? request = null,
        IProgress<(int total, int processed, string message)>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Imports an Excel file (.xlsx/.xls) stream.
    /// </summary>
    /// <param name="fileStream">The source Excel stream.</param>
    /// <param name="fileName">The original filename.</param>
    /// <param name="request">Import options.</param>
    /// <param name="progress">Optional progress reporter.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The import result.</returns>
    Task<Result<AdvancedImportResult>> ImportExcelAsync(
        Stream fileStream,
        string fileName,
        AdvancedImportRequest request,
        IProgress<(int total, int processed, string message)>? progress = null,
        CancellationToken cancellationToken = default);
}
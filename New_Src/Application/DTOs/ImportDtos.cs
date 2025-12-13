namespace Application.DTOs;

/// <summary>
/// Defines the supported file formats for import.
/// </summary>
public enum FileFormat
{
    Unknown,
    TabularCsv,
    TabularExcel,
    SampleIdBasedCsv,
    SampleIdBasedExcel,
    IcpMassHunter,
    PerkinElmer
}

/// <summary>
/// Represents the result of a file format detection operation.
/// </summary>
/// <param name="Format">The detected file format.</param>
/// <param name="DetectedDelimiter">The detected CSV delimiter, if applicable.</param>
/// <param name="HeaderRowIndex">The index of the detected header row.</param>
/// <param name="DetectedColumns">The list of detected column names.</param>
/// <param name="Message">Optional informational message or error details.</param>
public record FileFormatDetectionResult(
    FileFormat Format,
    string? DetectedDelimiter,
    int? HeaderRowIndex,
    List<string> DetectedColumns,
    string? Message
);

/// <summary>
/// Represents a request for importing data with specified options.
/// </summary>
/// <param name="ProjectName">The name for the new project.</param>
/// <param name="Owner">The owner of the project.</param>
/// <param name="ForceFormat">Explicitly force a specific file format.</param>
/// <param name="Delimiter">Explicitly specify the delimiter.</param>
/// <param name="HeaderRow">Explicitly specify the header row index.</param>
/// <param name="ColumnMappings">Map of file columns to internal schema columns.</param>
/// <param name="SkipLastRow">Whether to skip the last row (often footer/summary).</param>
/// <param name="AutoDetectType">Whether to auto-detect sample type from labels.</param>
/// <param name="DefaultType">The default sample type to apply if detection fails.</param>
public record AdvancedImportRequest(
    string ProjectName,
    string? Owner = null,
    FileFormat? ForceFormat = null,
    string? Delimiter = null,
    int? HeaderRow = null,
    Dictionary<string, string>? ColumnMappings = null,
    bool SkipLastRow = true,
    bool AutoDetectType = true,
    string? DefaultType = "Samp"
);

/// <summary>
/// Represents the comprehensive result of an import operation.
/// </summary>
/// <param name="ProjectId">The ID of the created project.</param>
/// <param name="TotalRowsRead">Total rows read from the source.</param>
/// <param name="TotalRowsImported">Rows successfully imported.</param>
/// <param name="SkippedRows">Rows skipped during import.</param>
/// <param name="DetectedFormat">The format detected and used.</param>
/// <param name="ImportedSolutionLabels">List of imported sample labels.</param>
/// <param name="ImportedElements">List of imported elements.</param>
/// <param name="Warnings">Collection of warnings encountered during import.</param>
public record AdvancedImportResult(
    Guid ProjectId,
    int TotalRowsRead,
    int TotalRowsImported,
    int SkippedRows,
    FileFormat DetectedFormat,
    List<string> ImportedSolutionLabels,
    List<string> ImportedElements,
    List<ImportWarning> Warnings
);

/// <summary>
/// Represents a warning occurring during data import.
/// </summary>
/// <param name="RowNumber">The relevant row number, if applicable.</param>
/// <param name="Column">The relevant column name.</param>
/// <param name="Message">The warning message.</param>
/// <param name="Level">The severity level of the warning.</param>
public record ImportWarning(
    int? RowNumber,
    string Column,
    string Message,
    ImportWarningLevel Level
);

/// <summary>
/// Defines warning severity levels for import operations.
/// </summary>
public enum ImportWarningLevel
{
    Info,
    Warning,
    Error
}

/// <summary>
/// Represents a parsed row of data from an input file.
/// </summary>
/// <param name="SolutionLabel">The sample label.</param>
/// <param name="Element">The element identifier.</param>
/// <param name="Intensity">The measured intensity value.</param>
/// <param name="CorrCon">The corrected concentration value.</param>
/// <param name="Type">The sample type.</param>
/// <param name="ActWgt">Actual weight.</param>
/// <param name="ActVol">Actual volume.</param>
/// <param name="DF">Dilution factor.</param>
/// <param name="AdditionalColumns">Extra columns preserved as-is.</param>
public record ParsedFileRow(
    string SolutionLabel,
    string Element,
    decimal? Intensity,
    decimal? CorrCon,
    string Type,
    decimal? ActWgt,
    decimal? ActVol,
    decimal? DF,
    Dictionary<string, object?> AdditionalColumns
);

/// <summary>
/// Represents a preview of the file content before full import.
/// </summary>
/// <param name="DetectedFormat">The confirmed file format.</param>
/// <param name="Headers">The detected headers.</param>
/// <param name="PreviewRows">Sample rows for preview.</param>
/// <param name="TotalRows">Estimated total rows.</param>
/// <param name="SuggestedColumnMappings">Suggested mappings for known columns.</param>
/// <param name="Message">Status or error message.</param>
public record FilePreviewResult(
    FileFormat DetectedFormat,
    List<string> Headers,
    List<Dictionary<string, string>> PreviewRows,
    int TotalRows,
    List<string> SuggestedColumnMappings,
    string? Message
);
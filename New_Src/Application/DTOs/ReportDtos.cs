namespace Application.DTOs;

/// <summary>
/// Request for generating reports
/// </summary>
public record ReportRequest(
    Guid ProjectId,
    ReportType ReportType,
    ReportFormat Format = ReportFormat.Excel,
    ReportOptions? Options = null
);

/// <summary>
/// Report generation options
/// </summary>
public record ReportOptions(
    bool IncludeSummary = true,
    bool IncludeRawData = true,
    bool IncludeStatistics = true,
    bool IncludeRmCheck = true,
    bool IncludeDuplicates = true,
    bool UseOxide = false,
    int DecimalPlaces = 2,
    List<string>? SelectedElements = null,
    string? Title = null,
    string? Author = null
);

/// <summary>
/// Type of report
/// </summary>
public enum ReportType
{
    Full,           // Complete report with all data
    Summary,        // Summary statistics only
    RmCheck,        // RM check results only
    Duplicates,     // Duplicate analysis only
    PivotTable,     // Pivot table export
    CrmComparison   // CRM comparison report
}

/// <summary>
/// Report format
/// </summary>
public enum ReportFormat
{
    Excel,
    Csv,
    Json,
    Html
}

/// <summary>
/// Result of report generation
/// </summary>
public record ReportResultDto(
    string FileName,
    string ContentType,
    byte[] Data,
    DateTime GeneratedAt,
    ReportMetadataDto Metadata
);

/// <summary>
/// Report metadata
/// </summary>
public record ReportMetadataDto(
    Guid ProjectId,
    string ProjectName,
    ReportType ReportType,
    ReportFormat Format,
    int TotalRows,
    int TotalColumns,
    TimeSpan GenerationTime
);

/// <summary>
/// Export request for simple exports
/// </summary>
public record ExportRequest(
    Guid ProjectId,
    ReportFormat Format = ReportFormat.Excel,
    bool UseOxide = false,
    int DecimalPlaces = 2,
    List<string>? SelectedElements = null,
    List<string>? SelectedSolutionLabels = null
);
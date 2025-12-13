namespace Application.DTOs;

/// <summary>
/// Represents a request for generating a report.
/// </summary>
/// <param name="ProjectId">The project identifier.</param>
/// <param name="ReportType">The type of report to generate.</param>
/// <param name="Format">The export format (e.g., Excel).</param>
/// <param name="Options">Configuration options for the report.</param>
public record ReportRequest(
    Guid ProjectId,
    ReportType ReportType,
    ReportFormat Format = ReportFormat.Excel,
    ReportOptions? Options = null
);

/// <summary>
/// Defines options for report generation.
/// </summary>
/// <param name="IncludeSummary">Include summary statistics.</param>
/// <param name="IncludeRawData">Include original raw data.</param>
/// <param name="IncludeStatistics">Include detailed statistics.</param>
/// <param name="IncludeRmCheck">Include RM check sections.</param>
/// <param name="IncludeDuplicates">Include duplicate analysis.</param>
/// <param name="UseOxide">Convert values to oxides.</param>
/// <param name="DecimalPlaces">Number of decimal places.</param>
/// <param name="SelectedElements">Specific elements to include.</param>
/// <param name="Title">Report title.</param>
/// <param name="Author">Report author name.</param>
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
/// Defines the available report types.
/// </summary>
public enum ReportType
{
    Full,
    Summary,
    RmCheck,
    Duplicates,
    PivotTable,
    CrmComparison
}

/// <summary>
/// Defines the supported report file formats.
/// </summary>
public enum ReportFormat
{
    Excel,
    Csv,
    Json,
    Html
}

/// <summary>
/// Represents the result of a report generation operation.
/// </summary>
/// <param name="FileName">The suggested filename.</param>
/// <param name="ContentType">The MIME content type.</param>
/// <param name="Data">The binary file data.</param>
/// <param name="GeneratedAt">The generation timestamp.</param>
/// <param name="Metadata">Metadata regarding generation performance.</param>
public record ReportResultDto(
    string FileName,
    string ContentType,
    byte[] Data,
    DateTime GeneratedAt,
    ReportMetadataDto Metadata
);

/// <summary>
/// Contains metadata about the generated report.
/// </summary>
/// <param name="ProjectId">The project identifier.</param>
/// <param name="ProjectName">The project name.</param>
/// <param name="ReportType">The report type.</param>
/// <param name="Format">The report format.</param>
/// <param name="TotalRows">Total rows in the report.</param>
/// <param name="TotalColumns">Total columns in the report.</param>
/// <param name="GenerationTime">Time taken to generate the report.</param>
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
/// Represents a request for a simple data export.
/// </summary>
/// <param name="ProjectId">The project identifier.</param>
/// <param name="Format">The export format.</param>
/// <param name="UseOxide">Convert values to oxide.</param>
/// <param name="DecimalPlaces">Decimal precision.</param>
/// <param name="SelectedElements">Specific elements.</param>
/// <param name="SelectedSolutionLabels">Specific samples.</param>
public record ExportRequest(
    Guid ProjectId,
    ReportFormat Format = ReportFormat.Excel,
    bool UseOxide = false,
    int DecimalPlaces = 2,
    List<string>? SelectedElements = null,
    List<string>? SelectedSolutionLabels = null
);

/// <summary>
/// Represents the calibration range for an element.
/// </summary>
/// <param name="Element">The element identifier.</param>
/// <param name="Min">Minimum range value.</param>
/// <param name="Max">Maximum range value.</param>
/// <param name="DisplayRange">Formatted range string.</param>
public record CalibrationRange(
    string Element,
    decimal Min,
    decimal Max,
    string DisplayRange
);

/// <summary>
/// Represents a request to select the best wavelength.
/// </summary>
/// <param name="ProjectId">The project identifier.</param>
/// <param name="SelectedSolutionLabels">Optional list of samples.</param>
/// <param name="UseConcentration">Use Soln Conc (true) or Corr Con (false).</param>
public record BestWavelengthRequest(
    Guid ProjectId,
    List<string>? SelectedSolutionLabels = null,
    bool UseConcentration = true
);

/// <summary>
/// Represents the result of best wavelength selection processes.
/// </summary>
/// <param name="CalibrationRanges">Calibration ranges per element.</param>
/// <param name="BestWavelengthsPerRow">Best wavelength selection per row and element.</param>
/// <param name="BaseElements">Mapping of base elements to available wavelengths.</param>
/// <param name="SelectedColumns">List of columns selected for the final view.</param>
public record BestWavelengthResult(
    Dictionary<string, CalibrationRange> CalibrationRanges,
    Dictionary<int, Dictionary<string, string>> BestWavelengthsPerRow,
    Dictionary<string, List<string>> BaseElements,
    List<string> SelectedColumns
);

/// <summary>
/// Details wavelength selection for a single row.
/// </summary>
/// <param name="SolutionLabel">The sample label.</param>
/// <param name="BaseElement">The base element.</param>
/// <param name="SelectedWavelength">The selected wavelength.</param>
/// <param name="Concentration">The concentration value.</param>
/// <param name="IsInCalibrationRange">Whether the value is within range.</param>
/// <param name="DistanceFromRange">Distance from the range boundary if out of range.</param>
public record WavelengthSelectionInfo(
    string SolutionLabel,
    string BaseElement,
    string SelectedWavelength,
    decimal? Concentration,
    bool IsInCalibrationRange,
    decimal? DistanceFromRange
);
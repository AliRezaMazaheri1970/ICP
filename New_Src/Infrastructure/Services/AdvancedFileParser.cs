using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Application.DTOs;
using ClosedXML.Excel;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services;

/// <summary>
/// Advanced file parser supporting multiple ICP-MS file formats. 
/// Equivalent to load_file. py logic in Python code.
/// 
/// Improvements:
/// - Fallback detection with Try-Catch (like Python)
/// - More flexible element patterns
/// - Enhanced sample type detection matching Python logic
/// </summary>
public class AdvancedFileParser
{
    private readonly ILogger _logger;

    // Patterns for format detection - more flexible
    private static readonly Regex SampleIdPattern = new(@"^Sample\s*ID\s*:? ", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex MethodFilePattern = new(@"^Method\s*File\s*:?", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex CalibrationPattern = new(@"^Calibration\s*File\s*:?", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // More flexible element pattern - allows dash, space, or no separator
    private static readonly Regex ElementPattern = new(@"^([A-Za-z]{1,2})[\s\-_]?(\d+\. ?\d*)$", RegexOptions.Compiled);

    // Alternative element patterns for fallback
    private static readonly Regex ElementPatternAlt1 = new(@"^(\d+\.?\d*)\s*([A-Za-z]{1,2})$", RegexOptions.Compiled); // "326.068 Na"
    private static readonly Regex ElementPatternAlt2 = new(@"^([A-Za-z]{1,2})(\d+)$", RegexOptions.Compiled); // "Na326"

    // Known column names
    private static readonly string[] SolutionLabelColumns = { "Solution Label", "SolutionLabel", "Sample ID", "SampleId", "Sample", "Label", "Name" };
    private static readonly string[] ElementColumns = { "Element", "Analyte", "Mass", "Isotope" };
    private static readonly string[] IntensityColumns = { "Int", "Intensity", "Net Intensity", "CPS", "Counts", "Signal" };
    private static readonly string[] ConcentrationColumns = { "Corr Con", "CorrCon", "Concentration", "Conc", "Calibrated Conc", "Result" };
    private static readonly string[] TypeColumns = { "Type", "Sample Type", "SampleType", "Category" };

    public AdvancedFileParser(ILogger logger)
    {
        _logger = logger;
    }

    #region Format Detection with Fallback

    /// <summary>
    /// Detect file format from stream with fallback mechanisms (like Python try-catch approach)
    /// </summary>
    public async Task<FileFormatDetectionResult> DetectFormatAsync(Stream stream, string fileName)
    {
        try
        {
            var isExcel = fileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase) ||
                          fileName.EndsWith(". xls", StringComparison.OrdinalIgnoreCase);

            if (isExcel)
            {
                // Try Excel detection with fallback
                try
                {
                    return DetectExcelFormat(stream);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Excel detection failed, trying as CSV");
                    // Fallback: maybe it's a CSV with wrong extension
                    stream.Position = 0;
                    return await DetectCsvFormatWithFallbackAsync(stream);
                }
            }
            else
            {
                return await DetectCsvFormatWithFallbackAsync(stream);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to detect file format for {FileName}", fileName);

            // Ultimate fallback: assume simple tabular CSV
            return new FileFormatDetectionResult(
                FileFormat.TabularCsv,
                ",",
                0,
                new List<string>(),
                $"Fallback detection used: {ex.Message}"
            );
        }
    }

    /// <summary>
    /// CSV format detection with multiple fallback strategies (like Python)
    /// </summary>
    private async Task<FileFormatDetectionResult> DetectCsvFormatWithFallbackAsync(Stream stream)
    {
        stream.Position = 0;
        using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);

        var previewLines = new List<string>();
        string? line;
        int lineCount = 0;
        while ((line = await reader.ReadLineAsync()) != null && lineCount < 30)
        {
            previewLines.Add(line);
            lineCount++;
        }

        stream.Position = 0;

        if (!previewLines.Any())
        {
            return new FileFormatDetectionResult(FileFormat.Unknown, null, null, new List<string>(), "File is empty");
        }

        // Strategy 1: Check for Sample ID-based format markers
        bool hasSampleIdMarker = previewLines.Any(l => SampleIdPattern.IsMatch(l.Trim()));
        bool hasNetIntensity = previewLines.Any(l => l.Contains("Net Intensity", StringComparison.OrdinalIgnoreCase));
        bool hasMethodFile = previewLines.Any(l => MethodFilePattern.IsMatch(l.Trim()));

        if (hasSampleIdMarker || (hasNetIntensity && hasMethodFile))
        {
            _logger.LogDebug("Detected Sample ID-based format");
            return new FileFormatDetectionResult(
                FileFormat.SampleIdBasedCsv,
                ",",
                null,
                new List<string> { "Solution Label", "Element", "Int", "Corr Con", "Type" },
                "Detected Sample ID-based format (ICP-MS export)"
            );
        }

        // Strategy 2: Detect delimiter
        var delimiter = DetectDelimiterAdvanced(previewLines);

        // Strategy 3: Try to find header row
        for (int headerRow = 0; headerRow < Math.Min(5, previewLines.Count); headerRow++)
        {
            var headers = previewLines[headerRow].Split(new[] { delimiter }, StringSplitOptions.None)
                .Select(h => h.Trim().Trim('"'))
                .ToList();

            bool hasLabelColumn = headers.Any(h => SolutionLabelColumns.Contains(h, StringComparer.OrdinalIgnoreCase));
            bool hasElementColumn = headers.Any(h => ElementColumns.Contains(h, StringComparer.OrdinalIgnoreCase));
            bool hasDataColumn = headers.Any(h => IntensityColumns.Concat(ConcentrationColumns).Contains(h, StringComparer.OrdinalIgnoreCase));

            if (hasLabelColumn && (hasElementColumn || hasDataColumn))
            {
                _logger.LogDebug("Detected tabular format with header at row {Row}", headerRow);
                return new FileFormatDetectionResult(
                    FileFormat.TabularCsv,
                    delimiter.ToString(),
                    headerRow,
                    headers,
                    $"Detected tabular CSV format (header on row {headerRow + 1})"
                );
            }
        }

        // Strategy 4: Heuristic - check if data looks numeric
        var firstDataLine = previewLines.Skip(1).FirstOrDefault() ?? "";
        var parts = firstDataLine.Split(new[] { delimiter }, StringSplitOptions.None);
        int numericCount = parts.Count(p => decimal.TryParse(p.Trim().Trim('"'), out _));

        if (numericCount > parts.Length / 2)
        {
            _logger.LogDebug("Detected tabular format by numeric heuristic");
            var headers = previewLines.First().Split(new[] { delimiter }, StringSplitOptions.None)
                .Select(h => h.Trim().Trim('"'))
                .ToList();

            return new FileFormatDetectionResult(
                FileFormat.TabularCsv,
                delimiter.ToString(),
                0,
                headers,
                "Detected tabular CSV format (numeric heuristic)"
            );
        }

        // Fallback: assume tabular with first row as header
        _logger.LogWarning("Could not definitively determine format, assuming tabular CSV");
        return new FileFormatDetectionResult(
            FileFormat.TabularCsv,
            delimiter.ToString(),
            0,
            previewLines.First().Split(new[] { delimiter }, StringSplitOptions.None)
                .Select(h => h.Trim().Trim('"')).ToList(),
            "Assumed tabular CSV format (fallback)"
        );
    }

    /// <summary>
    /// Advanced delimiter detection considering multiple lines
    /// </summary>
    private char DetectDelimiterAdvanced(List<string> lines)
    {
        var delimiters = new[] { ',', ';', '\t', '|' };
        var scores = new Dictionary<char, int>();

        foreach (var delim in delimiters)
        {
            // Check consistency across lines
            var counts = lines.Take(10).Select(l => l.Count(c => c == delim)).ToList();
            if (counts.All(c => c == counts.First()) && counts.First() > 0)
            {
                scores[delim] = counts.First() * 10; // Bonus for consistency
            }
            else
            {
                scores[delim] = counts.Sum();
            }
        }

        return scores.OrderByDescending(kv => kv.Value).First().Key;
    }

    private FileFormatDetectionResult DetectExcelFormat(Stream stream)
    {
        try
        {
            stream.Position = 0;
            using var workbook = new XLWorkbook(stream);
            var worksheet = workbook.Worksheets.First();

            var previewRows = new List<List<string>>();
            var usedRange = worksheet.RangeUsed();
            if (usedRange == null)
            {
                return new FileFormatDetectionResult(FileFormat.Unknown, null, null, new List<string>(), "Excel file is empty");
            }

            int maxRows = Math.Min(20, usedRange.RowCount());
            int maxCols = usedRange.ColumnCount();

            for (int row = 1; row <= maxRows; row++)
            {
                var rowData = new List<string>();
                for (int col = 1; col <= maxCols; col++)
                {
                    var cell = worksheet.Cell(row, col);
                    rowData.Add(cell.GetString());
                }
                previewRows.Add(rowData);
            }

            // Check for Sample ID-based format
            bool hasSampleIdMarker = previewRows.Any(r => r.Any(c => SampleIdPattern.IsMatch(c)));

            if (hasSampleIdMarker)
            {
                return new FileFormatDetectionResult(
                    FileFormat.SampleIdBasedExcel,
                    null,
                    null,
                    new List<string> { "Solution Label", "Element", "Int", "Corr Con", "Type" },
                    "Detected Sample ID-based Excel format"
                );
            }

            // Try multiple header rows
            for (int headerRow = 0; headerRow < Math.Min(5, previewRows.Count); headerRow++)
            {
                var headers = previewRows[headerRow];
                bool hasLabelColumn = headers.Any(h => SolutionLabelColumns.Contains(h, StringComparer.OrdinalIgnoreCase));
                bool hasElementColumn = headers.Any(h => ElementColumns.Contains(h, StringComparer.OrdinalIgnoreCase));

                if (hasLabelColumn || hasElementColumn)
                {
                    return new FileFormatDetectionResult(
                        FileFormat.TabularExcel,
                        null,
                        headerRow,
                        headers,
                        $"Detected tabular Excel format (header on row {headerRow + 1})"
                    );
                }
            }

            return new FileFormatDetectionResult(
                FileFormat.TabularExcel,
                null,
                0,
                previewRows.FirstOrDefault() ?? new List<string>(),
                "Assumed tabular Excel format"
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to detect Excel format");
            return new FileFormatDetectionResult(FileFormat.Unknown, null, null, new List<string>(), $"Excel detection failed: {ex.Message}");
        }
    }

    #endregion

    #region Parsing

    /// <summary>
    /// Parse file and return rows
    /// </summary>
    public async Task<(List<ParsedFileRow> Rows, List<ImportWarning> Warnings)> ParseFileAsync(
        Stream stream,
        string fileName,
        FileFormat format,
        AdvancedImportRequest? options = null,
        IProgress<(int total, int processed, string message)>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var rows = new List<ParsedFileRow>();
        var warnings = new List<ImportWarning>();

        try
        {
            switch (format)
            {
                case FileFormat.SampleIdBasedCsv:
                    (rows, warnings) = await ParseSampleIdBasedCsvAsync(stream, options, progress, cancellationToken);
                    break;

                case FileFormat.TabularCsv:
                    (rows, warnings) = await ParseTabularCsvAsync(stream, options, progress, cancellationToken);
                    break;

                case FileFormat.TabularExcel:
                    (rows, warnings) = ParseTabularExcel(stream, options, progress, cancellationToken);
                    break;

                case FileFormat.SampleIdBasedExcel:
                    (rows, warnings) = ParseSampleIdBasedExcel(stream, options, progress, cancellationToken);
                    break;

                default:
                    var detection = await DetectFormatAsync(stream, fileName);
                    if (detection.Format != FileFormat.Unknown)
                    {
                        stream.Position = 0;
                        return await ParseFileAsync(stream, fileName, detection.Format, options, progress, cancellationToken);
                    }
                    warnings.Add(new ImportWarning(null, "", "Unknown file format", ImportWarningLevel.Error));
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse file {FileName}", fileName);
            warnings.Add(new ImportWarning(null, "", $"Parse error: {ex.Message}", ImportWarningLevel.Error));
        }

        return (rows, warnings);
    }

    /// <summary>
    /// Parse Tabular Excel format using ClosedXML
    /// </summary>
    private (List<ParsedFileRow>, List<ImportWarning>) ParseTabularExcel(
        Stream stream,
        AdvancedImportRequest? options,
        IProgress<(int total, int processed, string message)>? progress,
        CancellationToken cancellationToken)
    {
        var rows = new List<ParsedFileRow>();
        var warnings = new List<ImportWarning>();

        try
        {
            stream.Position = 0;
            using var workbook = new XLWorkbook(stream);
            var worksheet = workbook.Worksheets.First();

            var usedRange = worksheet.RangeUsed();
            if (usedRange == null)
            {
                warnings.Add(new ImportWarning(null, "", "Excel file is empty", ImportWarningLevel.Error));
                return (rows, warnings);
            }

            int totalRows = usedRange.RowCount();
            int totalCols = usedRange.ColumnCount();
            int headerRow = (options?.HeaderRow ?? 0) + 1; // ClosedXML is 1-based

            // Read headers
            var headers = new List<string>();
            for (int col = 1; col <= totalCols; col++)
            {
                headers.Add(worksheet.Cell(headerRow, col).GetString().Trim());
            }

            // Map columns
            var columnMap = MapColumns(headers.ToArray(), options?.ColumnMappings);

            int processedRows = 0;

            // Read data rows (skip header and optionally last row)
            int endRow = options?.SkipLastRow == true ? totalRows - 1 : totalRows;

            for (int row = headerRow + 1; row <= endRow; row++)
            {
                if (cancellationToken.IsCancellationRequested) break;

                try
                {
                    var solutionLabel = GetExcelColumnValue(worksheet, row, columnMap, "SolutionLabel", headers) ?? "Unknown";
                    var element = GetExcelColumnValue(worksheet, row, columnMap, "Element", headers) ?? "";
                    var intensityStr = GetExcelColumnValue(worksheet, row, columnMap, "Intensity", headers);
                    var corrConStr = GetExcelColumnValue(worksheet, row, columnMap, "CorrCon", headers);
                    var typeStr = GetExcelColumnValue(worksheet, row, columnMap, "Type", headers);

                    if (string.IsNullOrWhiteSpace(element)) continue;

                    element = NormalizeElementName(element);
                    var intensity = ParseDecimal(intensityStr);
                    var corrCon = ParseDecimal(corrConStr);
                    var type = !string.IsNullOrEmpty(typeStr) ? typeStr : DetectSampleType(solutionLabel, options);

                    var actWgtStr = GetExcelColumnValue(worksheet, row, columnMap, "ActWgt", headers);
                    var actVolStr = GetExcelColumnValue(worksheet, row, columnMap, "ActVol", headers);
                    var dfStr = GetExcelColumnValue(worksheet, row, columnMap, "DF", headers);

                    rows.Add(new ParsedFileRow(
                        solutionLabel,
                        element,
                        intensity,
                        corrCon,
                        type,
                        ParseDecimal(actWgtStr),
                        ParseDecimal(actVolStr),
                        ParseDecimal(dfStr),
                        new Dictionary<string, object?>()
                    ));
                }
                catch (Exception ex)
                {
                    warnings.Add(new ImportWarning(row, "", $"Failed to parse row: {ex.Message}", ImportWarningLevel.Warning));
                }

                processedRows++;
                if (processedRows % 100 == 0)
                {
                    progress?.Report((totalRows, processedRows, $"Parsing Excel row {processedRows}/{totalRows}"));
                }
            }

            _logger.LogInformation("Parsed {RowCount} rows from Excel file", rows.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse tabular Excel");
            warnings.Add(new ImportWarning(null, "", $"Excel parse error: {ex.Message}", ImportWarningLevel.Error));
        }

        return (rows, warnings);
    }

    /// <summary>
    /// Parse Sample ID-based Excel format
    /// </summary>
    private (List<ParsedFileRow>, List<ImportWarning>) ParseSampleIdBasedExcel(
        Stream stream,
        AdvancedImportRequest? options,
        IProgress<(int total, int processed, string message)>? progress,
        CancellationToken cancellationToken)
    {
        var rows = new List<ParsedFileRow>();
        var warnings = new List<ImportWarning>();

        try
        {
            stream.Position = 0;
            using var workbook = new XLWorkbook(stream);
            var worksheet = workbook.Worksheets.First();

            var usedRange = worksheet.RangeUsed();
            if (usedRange == null)
            {
                warnings.Add(new ImportWarning(null, "", "Excel file is empty", ImportWarningLevel.Error));
                return (rows, warnings);
            }

            int totalRows = usedRange.RowCount();
            int totalCols = usedRange.ColumnCount();
            string? currentSample = null;
            int processedRows = 0;

            for (int row = 1; row <= totalRows; row++)
            {
                if (cancellationToken.IsCancellationRequested) break;

                // Skip last row if option set
                if (options?.SkipLastRow == true && row == totalRows) continue;

                var firstCellValue = worksheet.Cell(row, 1).GetString();

                // Check for Sample ID marker
                if (SampleIdPattern.IsMatch(firstCellValue))
                {
                    // Try to get sample ID from second column or parse from first
                    var secondCell = worksheet.Cell(row, 2).GetString();
                    if (!string.IsNullOrWhiteSpace(secondCell))
                    {
                        currentSample = secondCell.Trim();
                    }
                    else
                    {
                        // Parse from first cell: "Sample ID: XXX"
                        var match = Regex.Match(firstCellValue, @"Sample\s*ID\s*:\s*(.+)", RegexOptions.IgnoreCase);
                        currentSample = match.Success ? match.Groups[1].Value.Trim() : "Unknown";
                    }
                    continue;
                }

                // Skip metadata lines
                if (MethodFilePattern.IsMatch(firstCellValue) || CalibrationPattern.IsMatch(firstCellValue))
                {
                    continue;
                }

                // Parse data row
                if (currentSample != null && !string.IsNullOrWhiteSpace(firstCellValue))
                {
                    try
                    {
                        var element = NormalizeElementName(firstCellValue);
                        var intensityStr = worksheet.Cell(row, 2).GetString();
                        var corrConStr = totalCols >= 6 ? worksheet.Cell(row, 6).GetString() : null;

                        var intensity = ParseDecimal(intensityStr);
                        var corrCon = ParseDecimal(corrConStr);

                        if (intensity.HasValue || corrCon.HasValue)
                        {
                            var type = DetectSampleType(currentSample, options);

                            rows.Add(new ParsedFileRow(
                                currentSample,
                                element,
                                intensity,
                                corrCon,
                                type,
                                null, null, null,
                                new Dictionary<string, object?>()
                            ));
                        }
                    }
                    catch (Exception ex)
                    {
                        warnings.Add(new ImportWarning(row, firstCellValue, $"Failed to parse: {ex.Message}", ImportWarningLevel.Warning));
                    }
                }

                processedRows++;
                if (processedRows % 100 == 0)
                {
                    progress?.Report((totalRows, processedRows, $"Parsing Excel row {processedRows}/{totalRows}"));
                }
            }

            _logger.LogInformation("Parsed {RowCount} rows from Sample ID-based Excel", rows.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse Sample ID-based Excel");
            warnings.Add(new ImportWarning(null, "", $"Excel parse error: {ex.Message}", ImportWarningLevel.Error));
        }

        return (rows, warnings);
    }

    /// <summary>
    /// Parse Sample ID-based format (ICP-MS export)
    /// </summary>
    private async Task<(List<ParsedFileRow>, List<ImportWarning>)> ParseSampleIdBasedCsvAsync(
        Stream stream,
        AdvancedImportRequest? options,
        IProgress<(int total, int processed, string message)>? progress,
        CancellationToken cancellationToken)
    {
        var rows = new List<ParsedFileRow>();
        var warnings = new List<ImportWarning>();

        stream.Position = 0;
        using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);

        var allLines = new List<string>();
        string? line;
        while ((line = await reader.ReadLineAsync()) != null)
        {
            allLines.Add(line);
        }

        int totalRows = allLines.Count;
        string? currentSample = null;
        int processedRows = 0;

        for (int i = 0; i < allLines.Count; i++)
        {
            if (cancellationToken.IsCancellationRequested) break;

            line = allLines[i];

            if (string.IsNullOrWhiteSpace(line)) continue;
            if (options?.SkipLastRow == true && i == allLines.Count - 1) continue;

            var parts = ParseCsvLine(line);

            if (parts.Length > 0 && SampleIdPattern.IsMatch(parts[0]))
            {
                currentSample = parts.Length > 1 ? parts[1].Trim() : "Unknown";
                continue;
            }

            if (parts.Length > 0 && (MethodFilePattern.IsMatch(parts[0]) || CalibrationPattern.IsMatch(parts[0])))
            {
                continue;
            }

            if (currentSample != null && parts.Length > 0 && !string.IsNullOrWhiteSpace(parts[0]))
            {
                try
                {
                    var element = NormalizeElementName(parts[0]);
                    decimal? intensity = parts.Length > 1 ? ParseDecimal(parts[1]) : null;
                    decimal? corrCon = parts.Length > 5 ? ParseDecimal(parts[5]) : null;

                    if (intensity.HasValue || corrCon.HasValue)
                    {
                        var type = DetectSampleType(currentSample, options);

                        rows.Add(new ParsedFileRow(
                            currentSample,
                            element,
                            intensity,
                            corrCon,
                            type,
                            null, null, null,
                            new Dictionary<string, object?>()
                        ));
                    }
                }
                catch (Exception ex)
                {
                    warnings.Add(new ImportWarning(i + 1, parts[0], $"Failed to parse: {ex.Message}", ImportWarningLevel.Warning));
                }
            }

            processedRows++;
            if (processedRows % 100 == 0)
            {
                progress?.Report((totalRows, processedRows, $"Parsing row {processedRows}/{totalRows}"));
            }
        }

        return (rows, warnings);
    }

    /// <summary>
    /// Parse tabular CSV format
    /// </summary>
    private async Task<(List<ParsedFileRow>, List<ImportWarning>)> ParseTabularCsvAsync(
        Stream stream,
        AdvancedImportRequest? options,
        IProgress<(int total, int processed, string message)>? progress,
        CancellationToken cancellationToken)
    {
        var rows = new List<ParsedFileRow>();
        var warnings = new List<ImportWarning>();

        stream.Position = 0;

        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            DetectDelimiter = true,
            BadDataFound = null,
            MissingFieldFound = null,
            TrimOptions = TrimOptions.Trim,
            HeaderValidated = null
        };

        using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
        using var csv = new CsvReader(reader, config);

        int headerRow = options?.HeaderRow ?? 0;
        for (int i = 0; i < headerRow && csv.Read(); i++) { }

        if (!csv.Read())
        {
            warnings.Add(new ImportWarning(null, "", "File is empty or has no header", ImportWarningLevel.Error));
            return (rows, warnings);
        }

        csv.ReadHeader();
        var headers = csv.HeaderRecord ?? Array.Empty<string>();
        var columnMap = MapColumns(headers, options?.ColumnMappings);

        int rowNumber = headerRow + 1;
        int totalEstimate = 1000;
        int processedRows = 0;

        while (csv.Read())
        {
            if (cancellationToken.IsCancellationRequested) break;

            rowNumber++;

            if (options?.SkipLastRow == true && csv.Parser.RawRow == csv.Parser.Count - 1)
                continue;

            try
            {
                var solutionLabel = GetColumnValue(csv, columnMap, "SolutionLabel") ?? "Unknown";
                var element = GetColumnValue(csv, columnMap, "Element") ?? "";
                var intensityStr = GetColumnValue(csv, columnMap, "Intensity");
                var corrConStr = GetColumnValue(csv, columnMap, "CorrCon");
                var typeStr = GetColumnValue(csv, columnMap, "Type");

                if (string.IsNullOrWhiteSpace(element)) continue;

                element = NormalizeElementName(element);
                var intensity = ParseDecimal(intensityStr);
                var corrCon = ParseDecimal(corrConStr);
                var type = !string.IsNullOrEmpty(typeStr) ? typeStr : DetectSampleType(solutionLabel, options);

                var actWgtStr = GetColumnValue(csv, columnMap, "ActWgt");
                var actVolStr = GetColumnValue(csv, columnMap, "ActVol");
                var dfStr = GetColumnValue(csv, columnMap, "DF");

                rows.Add(new ParsedFileRow(
                    solutionLabel,
                    element,
                    intensity,
                    corrCon,
                    type,
                    ParseDecimal(actWgtStr),
                    ParseDecimal(actVolStr),
                    ParseDecimal(dfStr),
                    new Dictionary<string, object?>()
                ));
            }
            catch (Exception ex)
            {
                warnings.Add(new ImportWarning(rowNumber, "", $"Failed to parse row: {ex.Message}", ImportWarningLevel.Warning));
            }

            processedRows++;
            if (processedRows % 100 == 0)
            {
                progress?.Report((totalEstimate, processedRows, $"Parsing row {processedRows}"));
            }
        }

        return (rows, warnings);
    }

    #endregion

    #region Enhanced Sample Type Detection (matching Python logic)

    /// <summary>
    /// Enhanced sample type detection matching Python code patterns
    /// Patterns from Python: load_file. py, pivot_tab.py, empty_check.py
    /// </summary>
    private string DetectSampleType(string solutionLabel, AdvancedImportRequest? options)
    {
        if (!(options?.AutoDetectType ?? true))
            return options?.DefaultType ?? "Samp";

        var upper = solutionLabel.ToUpperInvariant().Trim();

        // === BLANK Detection (expanded) ===
        // Python patterns: "BLANK", "BLK", rinse solutions
        if (upper.Contains("BLANK") ||
            upper.Contains("BLK") ||
            upper.StartsWith("BL1") || upper.StartsWith("BL2") || upper.StartsWith("BL3") ||
            upper.Contains("RINSE") ||
            upper.Contains("WASH") ||
            upper == "B" ||
            Regex.IsMatch(upper, @"^BL\d+$") ||
            Regex.IsMatch(upper, @"^BLANK\s*\d*$"))
        {
            return "Blk";
        }

        // === STANDARD Detection (expanded) ===
        // Python patterns: "STD", "STANDARD", calibration standards
        if (upper.Contains("STD") ||
            upper.Contains("STANDARD") ||
            upper.StartsWith("CAL") ||
            upper.StartsWith("S1") || upper.StartsWith("S2") || upper.StartsWith("S3") ||
            upper.StartsWith("S4") || upper.StartsWith("S5") || upper.StartsWith("S6") ||
            Regex.IsMatch(upper, @"^S\d+$") ||
            Regex.IsMatch(upper, @"^\d+\s*(PPM|PPB|MG/L|UG/L)$", RegexOptions.IgnoreCase) ||
            Regex.IsMatch(upper, @"^STD\s*\d+$") ||
            Regex.IsMatch(upper, @"^\d+\s*STD$"))
        {
            return "Std";
        }

        // === QC Detection (expanded) ===
        // Python patterns: QC samples, ICV, CCV, continuing calibration
        if (upper.Contains("QC") ||
            upper.Contains("CHECK") ||
            upper.Contains("QUALITY") ||
            upper.Contains("CONTROL") ||
            upper.StartsWith("ICV") ||  // Initial Calibration Verification
            upper.StartsWith("CCV") ||  // Continuing Calibration Verification
            upper.StartsWith("ICB") ||  // Initial Calibration Blank
            upper.StartsWith("CCB") ||  // Continuing Calibration Blank
            upper.Contains("DRIFT") ||
            upper.Contains("VERIFY") ||
            Regex.IsMatch(upper, @"^QC\s*\d*$") ||
            Regex.IsMatch(upper, @"^(ICV|CCV|ICB|CCB)\d*$"))
        {
            return "QC";
        }

        // === Reference Material Detection (expanded) ===
        // Python patterns: OREAS, SRM, CRM, NIST, BCR, geological standards
        if (Regex.IsMatch(upper, @"\b(OREAS|SRM|CRM|NIST|BCR|TILL|GBW|AGV|BCR|BHVO|BIR|DNC|DTS|GSP|PCC|RGM|SCO|SDC|STM|W-2|JA|JB|JG|JR)\b") ||
            Regex.IsMatch(upper, @"^(OREAS|SRM|CRM)\s*\d+", RegexOptions.IgnoreCase) ||
            upper.Contains("REFERENCE") ||
            upper.Contains("CERTIFIED") ||
            Regex.IsMatch(upper, @"^RM[\s\-_]?\d+$"))
        {
            return "RM";
        }

        // === Duplicate/Repeat Detection ===
        // Python patterns: TEK, RET, DUP for duplicates
        if (upper.EndsWith("-TEK") || upper.EndsWith(" TEK") || upper.EndsWith("_TEK") ||
            upper.EndsWith("-RET") || upper.EndsWith(" RET") || upper.EndsWith("_RET") ||
            upper.EndsWith("-DUP") || upper.EndsWith(" DUP") || upper.EndsWith("_DUP") ||
            upper.EndsWith("-REP") || upper.EndsWith(" REP") || upper.EndsWith("_REP") ||
            Regex.IsMatch(upper, @"[-_\s](TEK|RET|DUP|REP|DUPLICATE|REPEAT)\d*$"))
        {
            // It's a sample duplicate, still return Samp
            return "Samp";
        }

        // === Default: Sample ===
        return "Samp";
    }

    #endregion

    #region Helper Methods

    private string[] ParseCsvLine(string line)
    {
        var result = new List<string>();
        bool inQuotes = false;
        var current = new StringBuilder();

        foreach (char c in line)
        {
            if (c == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (c == ',' && !inQuotes)
            {
                result.Add(current.ToString().Trim());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        result.Add(current.ToString().Trim());
        return result.ToArray();
    }

    /// <summary>
    /// Normalize element name with multiple pattern support
    /// </summary>
    private string NormalizeElementName(string element)
    {
        if (string.IsNullOrWhiteSpace(element)) return element;

        element = element.Trim();

        // Try standard pattern: "Na 326. 068" or "Na326.068"
        var match = ElementPattern.Match(element);
        if (match.Success)
        {
            return $"{match.Groups[1].Value} {match.Groups[2].Value}";
        }

        // Try alternative: "326.068 Na"
        match = ElementPatternAlt1.Match(element);
        if (match.Success)
        {
            return $"{match.Groups[2].Value} {match.Groups[1].Value}";
        }

        // Try alternative: "Na326"
        match = ElementPatternAlt2.Match(element);
        if (match.Success)
        {
            return $"{match.Groups[1].Value} {match.Groups[2].Value}";
        }

        return element;
    }

    private decimal? ParseDecimal(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        value = value.Trim();

        // Handle scientific notation
        if (value.Contains('E') || value.Contains('e'))
        {
            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var dResult))
                return (decimal)dResult;
        }

        if (decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var result))
            return result;
        return null;
    }

    private Dictionary<string, int> MapColumns(string[] headers, Dictionary<string, string>? customMappings)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < headers.Length; i++)
        {
            var header = headers[i].Trim();

            if (customMappings != null)
            {
                foreach (var mapping in customMappings)
                {
                    if (header.Equals(mapping.Value, StringComparison.OrdinalIgnoreCase))
                    {
                        map[mapping.Key] = i;
                    }
                }
            }

            if (SolutionLabelColumns.Contains(header, StringComparer.OrdinalIgnoreCase))
                map["SolutionLabel"] = i;
            else if (ElementColumns.Contains(header, StringComparer.OrdinalIgnoreCase))
                map["Element"] = i;
            else if (IntensityColumns.Contains(header, StringComparer.OrdinalIgnoreCase))
                map["Intensity"] = i;
            else if (ConcentrationColumns.Contains(header, StringComparer.OrdinalIgnoreCase))
                map["CorrCon"] = i;
            else if (TypeColumns.Contains(header, StringComparer.OrdinalIgnoreCase))
                map["Type"] = i;
            else if (header.Equals("Act Wgt", StringComparison.OrdinalIgnoreCase) ||
                     header.Equals("ActWgt", StringComparison.OrdinalIgnoreCase) ||
                     header.Equals("Weight", StringComparison.OrdinalIgnoreCase))
                map["ActWgt"] = i;
            else if (header.Equals("Act Vol", StringComparison.OrdinalIgnoreCase) ||
                     header.Equals("ActVol", StringComparison.OrdinalIgnoreCase) ||
                     header.Equals("Volume", StringComparison.OrdinalIgnoreCase))
                map["ActVol"] = i;
            else if (header.Equals("DF", StringComparison.OrdinalIgnoreCase) ||
                     header.Equals("Dilution", StringComparison.OrdinalIgnoreCase) ||
                     header.Equals("Dilution Factor", StringComparison.OrdinalIgnoreCase))
                map["DF"] = i;
        }

        return map;
    }

    private string? GetColumnValue(CsvReader csv, Dictionary<string, int> columnMap, string columnName)
    {
        if (columnMap.TryGetValue(columnName, out var index))
        {
            try
            {
                return csv.GetField(index);
            }
            catch
            {
                return null;
            }
        }
        return null;
    }

    private string? GetExcelColumnValue(IXLWorksheet worksheet, int row, Dictionary<string, int> columnMap, string columnName, List<string> headers)
    {
        if (columnMap.TryGetValue(columnName, out var index))
        {
            try
            {
                return worksheet.Cell(row, index + 1).GetString(); // ClosedXML is 1-based
            }
            catch
            {
                return null;
            }
        }
        return null;
    }

    #endregion
}
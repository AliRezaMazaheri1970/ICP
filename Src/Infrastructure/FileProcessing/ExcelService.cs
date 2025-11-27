using Application.Services.Interfaces;
using ClosedXML.Excel;
using Domain.Entities;
using Domain.Enums;
using System.Text.RegularExpressions;

namespace Infrastructure.FileProcessing;

public class ExcelService : IFileImportService
{
    public bool CanSupport(string fileName)
    {
        return fileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase) ||
               fileName.EndsWith(".xls", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<List<Sample>> ProcessFileAsync(Stream fileStream, CancellationToken cancellationToken)
    {
        var samples = new List<Sample>();

        try
        {
            using var workbook = new XLWorkbook(fileStream);
            var worksheet = workbook.Worksheet(1);
            var range = worksheet.RangeUsed();

            if (range == null) return samples;

            var headerRow = range.Row(1);
            var dataRows = range.RowsUsed().Skip(1);

            var colMap = new Dictionary<string, int>();
            foreach (var cell in headerRow.CellsUsed())
            {
                colMap[cell.GetValue<string>().Trim().ToLower()] = cell.Address.ColumnNumber;
            }

            foreach (var row in dataRows)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var label = GetCellValue(row, colMap, "sampleid", "sample id", "label", "id")
                            ?? row.Cell(1).GetValue<string>();

                if (string.IsNullOrWhiteSpace(label)) continue;

                var typeStr = GetCellValue(row, colMap, "type") ?? "Sample";
                if (!Enum.TryParse(typeStr, true, out SampleType type))
                    type = DetectSampleType(label); // استفاده از منطق تشخیص نام در صورت نبود نوع

                var weight = ParseDouble(GetCellValue(row, colMap, "weight", "wt"));
                var volume = ParseDouble(GetCellValue(row, colMap, "volume", "vol"));
                var df = ParseDouble(GetCellValue(row, colMap, "dilution", "df"));
                if (df == 0) df = 1;

                var sample = new Sample
                {
                    Id = Guid.NewGuid(),
                    SolutionLabel = label,
                    Type = type,
                    Weight = weight,
                    Volume = volume,
                    DilutionFactor = df,
                    Measurements = new List<Measurement>()
                };

                foreach (var cell in headerRow.CellsUsed())
                {
                    var header = cell.GetValue<string>();
                    if (IsElementColumn(header, out string cleanName))
                    {
                        var valueCell = row.Cell(cell.Address.ColumnNumber);
                        if (valueCell.TryGetValue(out double measuredValue))
                        {
                            sample.Measurements.Add(new Measurement
                            {
                                Id = Guid.NewGuid(),
                                ElementName = cleanName,
                                Value = measuredValue,
                                Unit = "ppm"
                            });
                        }
                    }
                }
                samples.Add(sample);
            }
        }
        catch (Exception ex)
        {
            throw new Exception($"Error processing Excel file: {ex.Message}", ex);
        }

        return await Task.FromResult(samples);
    }

    // --- Helpers ---
    private string? GetCellValue(IXLRangeRow row, Dictionary<string, int> map, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (map.TryGetValue(key, out int colIndex))
                return row.Cell(colIndex).GetValue<string>();
        }
        return null;
    }

    private double ParseDouble(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return 0;
        value = value.Replace(",", "");
        if (double.TryParse(value, out var result)) return result;
        return 0;
    }

    private bool IsElementColumn(string header, out string cleanName)
    {
        cleanName = string.Empty;
        if (string.IsNullOrWhiteSpace(header)) return false;

        var fixedCols = new[] { "sampleid", "sample id", "type", "weight", "wt", "volume", "vol", "dilution", "df" };
        if (fixedCols.Contains(header.ToLower())) return false;

        var match = Regex.Match(header.Trim(), @"^([A-Z][a-z]?)[\s-]?(\d+)?$");
        if (match.Success)
        {
            cleanName = match.Groups[2].Success ? $"{match.Groups[1].Value} {match.Groups[2].Value}" : match.Groups[1].Value;
            return true;
        }
        return false;
    }

    private SampleType DetectSampleType(string label)
    {
        if (string.IsNullOrWhiteSpace(label)) return SampleType.Unknown;
        var upper = label.ToUpper();
        if (upper.Contains("STD")) return SampleType.Standard;
        if (upper.Contains("BLK") || upper.Contains("BLANK")) return SampleType.Blank;
        return SampleType.Sample;
    }
}
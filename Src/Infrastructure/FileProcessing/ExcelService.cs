// مسیر فایل: Infrastructure/FileProcessing/ExcelService.cs

using Application.Services.Interfaces;
using ClosedXML.Excel;
using Domain.Entities;
using Domain.Enums;
using System.Text.RegularExpressions;

namespace Infrastructure.FileProcessing;

public class ExcelService : IExcelService
{
    public async Task<List<Sample>> ReadSamplesFromExcelAsync(Stream fileStream, CancellationToken cancellationToken)
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

            // نگاشت ستون‌ها برای انعطاف‌پذیری در برابر تغییر جای ستون‌ها
            var colMap = new Dictionary<string, int>();
            foreach (var cell in headerRow.CellsUsed())
            {
                colMap[cell.GetValue<string>().Trim().ToLower()] = cell.Address.ColumnNumber;
            }

            foreach (var row in dataRows)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // خواندن ستون‌های ثابت (با پشتیبانی از نام‌های مختلف)
                var label = GetCellValue(row, colMap, "sampleid", "sample id", "label", "id")
                            ?? row.Cell(1).GetValue<string>(); // Fallback

                if (string.IsNullOrWhiteSpace(label)) continue;

                // تشخیص نوع نمونه
                var typeStr = GetCellValue(row, colMap, "type") ?? row.Cell(2).GetValue<string>();
                if (!Enum.TryParse(typeStr, true, out SampleType type)) type = SampleType.Sample;

                // خواندن مقادیر عددی با مدیریت خطا
                var weight = ParseDouble(GetCellValue(row, colMap, "weight", "wt", "sample_weight"));
                var volume = ParseDouble(GetCellValue(row, colMap, "volume", "vol", "sample_volume"));
                var df = ParseDouble(GetCellValue(row, colMap, "dilution", "df", "dilutionfactor"));
                if (df == 0) df = 1; // پیش‌فرض ضریب رقت

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

                // خواندن ستون‌های عنصری (Dynamic Columns)
                foreach (var cell in headerRow.CellsUsed())
                {
                    var header = cell.GetValue<string>();

                    // استفاده از منطق هوشمند برای تشخیص نام عنصر
                    if (IsElementColumn(header, out string cleanName))
                    {
                        var valueCell = row.Cell(cell.Address.ColumnNumber);
                        if (valueCell.TryGetValue(out double measuredValue))
                        {
                            sample.Measurements.Add(new Measurement
                            {
                                Id = Guid.NewGuid(),
                                ElementName = cleanName, // نام تمیز شده (مثلاً "Li 7")
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

    // --- Helper Methods ---

    private string? GetCellValue(IXLRangeRow row, Dictionary<string, int> map, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (map.TryGetValue(key, out int colIndex))
            {
                return row.Cell(colIndex).GetValue<string>();
            }
        }
        return null;
    }

    private double ParseDouble(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return 0;
        // حذف کاما برای اعداد فرمت شده مثل "1,234.56"
        value = value.Replace(",", "");
        if (double.TryParse(value, out var result)) return result;
        return 0;
    }

    private bool IsElementColumn(string header, out string cleanName)
    {
        cleanName = string.Empty;
        if (string.IsNullOrWhiteSpace(header)) return false;

        // ستون‌های ثابت را نادیده بگیر
        var fixedCols = new[] { "sampleid", "sample id", "type", "weight", "wt", "volume", "vol", "dilution", "df", "date", "time" };
        if (fixedCols.Contains(header.ToLower())) return false;

        // Regex برای تشخیص عنصر و ایزوتوپ (مثال: Li7, U-238, Cu)
        var match = Regex.Match(header.Trim(), @"^([A-Z][a-z]?)[\s-]?(\d+)?$");

        if (match.Success)
        {
            var symbol = match.Groups[1].Value;
            if (match.Groups[2].Success)
            {
                // جدا کردن عدد ایزوتوپ با فاصله (استاندارد پروژه)
                cleanName = $"{symbol} {match.Groups[2].Value}";
            }
            else
            {
                cleanName = symbol;
            }
            return true;
        }
        return false;
    }
}
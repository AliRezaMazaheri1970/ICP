using ClosedXML.Excel;
using Core.Icp.Domain.Entities.Samples;
using Infrastructure.Icp.Files.Interfaces;
using Infrastructure.Icp.Files.Models;
using Infrastructure.Icp.Files.Parsers;
using Shared.Icp.Exceptions;

namespace Infrastructure.Icp.Files.Services
{
    /// <summary>
    /// پردازش کننده فایل‌های Excel
    /// </summary>
    public class ExcelFileProcessor : IExcelFileProcessor
    {
        private readonly FileValidationService _validationService;
        private readonly SampleDataParser _sampleParser;

        public ExcelFileProcessor()
        {
            _validationService = new FileValidationService();
            _sampleParser = new SampleDataParser();
        }

        /// <summary>
        /// Import نمونه‌ها از فایل Excel
        /// </summary>
        public async Task<FileImportResult> ImportSamplesAsync(string filePath)
        {
            var startTime = DateTime.UtcNow;

            try
            {
                // اعتبارسنجی فایل
                var fileValidation = _validationService.ValidateFile(filePath);
                if (!fileValidation.IsValid)
                {
                    return FileImportResult.CreateFailure(
                        "فایل نامعتبر است",
                        fileValidation);
                }

                // خواندن فایل
                var rows = await Task.Run(() => ReadExcelFile(filePath));

                // اعتبارسنجی ساختار
                var structureValidation = _validationService.ValidateDataStructure(rows);
                if (!structureValidation.IsValid)
                {
                    return FileImportResult.CreateFailure(
                        "ساختار فایل نامعتبر است",
                        structureValidation);
                }

                // Parse کردن به Sample
                var samples = _sampleParser.ParseSamples(rows);

                // اعتبارسنجی Sample ها
                ValidateSamples(samples, structureValidation);

                var result = FileImportResult.CreateSuccess(samples, structureValidation);
                result.ProcessingTime = DateTime.UtcNow - startTime;
                result.Metadata["FileName"] = Path.GetFileName(filePath);
                result.Metadata["FileSize"] = new FileInfo(filePath).Length;

                return result;
            }
            catch (Exception ex)
            {
                throw new FileProcessingException(
                    $"خطا در پردازش فایل Excel: {ex.Message}",
                    Path.GetFileName(filePath),
                    ex);
            }
        }

        /// <summary>
        /// Import از Stream
        /// </summary>
        public async Task<FileImportResult> ImportSamplesAsync(Stream stream, string fileName)
        {
            var startTime = DateTime.UtcNow;

            try
            {
                // اعتبارسنجی Stream
                var streamValidation = _validationService.ValidateStream(stream, fileName);
                if (!streamValidation.IsValid)
                {
                    return FileImportResult.CreateFailure(
                        "Stream نامعتبر است",
                        streamValidation);
                }

                // خواندن از Stream
                var rows = await Task.Run(() => ReadExcelStream(stream));

                // اعتبارسنجی ساختار
                var structureValidation = _validationService.ValidateDataStructure(rows);
                if (!structureValidation.IsValid)
                {
                    return FileImportResult.CreateFailure(
                        "ساختار فایل نامعتبر است",
                        structureValidation);
                }

                // Parse کردن به Sample
                var samples = _sampleParser.ParseSamples(rows);

                // اعتبارسنجی Sample ها
                ValidateSamples(samples, structureValidation);

                var result = FileImportResult.CreateSuccess(samples, structureValidation);
                result.ProcessingTime = DateTime.UtcNow - startTime;
                result.Metadata["FileName"] = fileName;
                result.Metadata["FileSize"] = stream.Length;

                return result;
            }
            catch (Exception ex)
            {
                throw new FileProcessingException(
                    $"خطا در پردازش Stream: {ex.Message}",
                    fileName,
                    ex);
            }
        }

        /// <summary>
        /// Export نمونه‌ها به Excel
        /// </summary>
        public async Task<byte[]> ExportSamplesAsync<T>(IEnumerable<T> data, string sheetName = "Data") where T : class
        {
            try
            {
                return await Task.Run(() =>
                {
                    using var workbook = new XLWorkbook();
                    var worksheet = workbook.Worksheets.Add(sheetName);

                    // نوشتن داده‌ها
                    var table = worksheet.Cell(1, 1).InsertTable(data);
                    table.Theme = XLTableTheme.TableStyleMedium2;

                    // Auto-fit columns
                    worksheet.Columns().AdjustToContents();

                    using var memoryStream = new MemoryStream();
                    workbook.SaveAs(memoryStream);
                    return memoryStream.ToArray();
                });
            }
            catch (Exception ex)
            {
                throw new FileProcessingException(
                    $"خطا در تولید فایل Excel: {ex.Message}",
                    ex);
            }
        }

        /// <summary>
        /// Export به فایل
        /// </summary>
        public async Task ExportSamplesToFileAsync<T>(string filePath, IEnumerable<T> data, string sheetName = "Data") where T : class
        {
            try
            {
                await Task.Run(() =>
                {
                    using var workbook = new XLWorkbook();
                    var worksheet = workbook.Worksheets.Add(sheetName);

                    // نوشتن داده‌ها
                    var table = worksheet.Cell(1, 1).InsertTable(data);
                    table.Theme = XLTableTheme.TableStyleMedium2;

                    // Auto-fit columns
                    worksheet.Columns().AdjustToContents();

                    workbook.SaveAs(filePath);
                });
            }
            catch (Exception ex)
            {
                throw new FileProcessingException(
                    $"خطا در ذخیره فایل Excel: {ex.Message}",
                    Path.GetFileName(filePath),
                    ex);
            }
        }

        /// <summary>
        /// دریافت لیست Sheet ها
        /// </summary>
        public async Task<List<string>> GetSheetNamesAsync(string filePath)
        {
            try
            {
                return await Task.Run(() =>
                {
                    using var workbook = new XLWorkbook(filePath);
                    return workbook.Worksheets.Select(ws => ws.Name).ToList();
                });
            }
            catch (Exception ex)
            {
                throw new FileProcessingException(
                    $"خطا در خواندن Sheet ها: {ex.Message}",
                    Path.GetFileName(filePath),
                    ex);
            }
        }

        #region Private Methods

        /// <summary>
        /// خواندن فایل Excel
        /// </summary>
        private List<Dictionary<string, string>> ReadExcelFile(string filePath)
        {
            using var workbook = new XLWorkbook(filePath);
            var worksheet = workbook.Worksheet(1); // اولین Sheet
            return ReadWorksheet(worksheet);
        }

        /// <summary>
        /// خواندن از Stream
        /// </summary>
        private List<Dictionary<string, string>> ReadExcelStream(Stream stream)
        {
            stream.Position = 0;
            using var workbook = new XLWorkbook(stream);
            var worksheet = workbook.Worksheet(1);
            return ReadWorksheet(worksheet);
        }

        /// <summary>
        /// خواندن یک Worksheet
        /// </summary>
        private List<Dictionary<string, string>> ReadWorksheet(IXLWorksheet worksheet)
        {
            var rows = new List<Dictionary<string, string>>();

            // پیدا کردن اولین ردیف غیر خالی
            var firstRow = worksheet.FirstRowUsed();
            if (firstRow == null)
            {
                throw new FileProcessingException("Worksheet خالی است");
            }

            // خواندن Header
            var headers = firstRow.CellsUsed()
                .Select(c => c.GetValue<string>())
                .ToList();

            if (!headers.Any())
            {
                throw new FileProcessingException("Header یافت نشد");
            }

            // خواندن ردیف‌های داده
            var dataRows = worksheet.RowsUsed().Skip(1); // Skip header

            foreach (var row in dataRows)
            {
                var rowData = new Dictionary<string, string>();
                var cells = row.CellsUsed().ToList();

                for (int i = 0; i < headers.Count && i < cells.Count; i++)
                {
                    var header = headers[i];
                    var value = cells[i].GetValue<string>();
                    rowData[header] = value ?? string.Empty;
                }

                // اگر ردیف خالی نباشه
                if (rowData.Values.Any(v => !string.IsNullOrWhiteSpace(v)))
                {
                    rows.Add(rowData);
                }
            }

            return rows;
        }

        /// <summary>
        /// اعتبارسنجی Sample ها
        /// </summary>
        private void ValidateSamples(List<Sample> samples, FileValidationResult validationResult)
        {
            for (int i = 0; i < samples.Count; i++)
            {
                var sample = samples[i];
                if (_sampleParser.ValidateSample(sample, out var errors))
                {
                    validationResult.ValidRows++;
                }
                else
                {
                    validationResult.InvalidRows++;
                    foreach (var error in errors)
                    {
                        validationResult.AddRowError(i + 2, error);
                    }
                }
            }
        }

        #endregion
    }
}
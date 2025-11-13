using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using Core.Icp.Domain.Entities.Samples;
using Infrastructure.Icp.Files.Interfaces;
using Infrastructure.Icp.Files.Models;
using Infrastructure.Icp.Files.Parsers;
using Shared.Icp.Exceptions;

namespace Infrastructure.Icp.Files.Services
{
    /// <summary>
    /// پردازش کننده فایل‌های CSV
    /// </summary>
    public class CsvFileProcessor : ICsvFileProcessor
    {
        private readonly FileValidationService _validationService;
        private readonly SampleDataParser _sampleParser;

        public CsvFileProcessor()
        {
            _validationService = new FileValidationService();
            _sampleParser = new SampleDataParser();
        }

        /// <summary>
        /// Import نمونه‌ها از فایل CSV
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
                var rows = await ReadCsvFileAsync(filePath);

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
                    $"خطا در پردازش فایل CSV: {ex.Message}",
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
                var rows = await ReadCsvStreamAsync(stream);

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
        /// Export نمونه‌ها به CSV
        /// </summary>
        public async Task<byte[]> ExportSamplesAsync<T>(IEnumerable<T> data) where T : class
        {
            try
            {
                using var memoryStream = new MemoryStream();
                using var streamWriter = new StreamWriter(memoryStream, Encoding.UTF8);
                using var csv = new CsvWriter(streamWriter, GetCsvConfiguration());

                await csv.WriteRecordsAsync(data);
                await streamWriter.FlushAsync();

                return memoryStream.ToArray();
            }
            catch (Exception ex)
            {
                throw new FileProcessingException(
                    $"خطا در تولید فایل CSV: {ex.Message}",
                    ex);
            }
        }

        /// <summary>
        /// Export به فایل
        /// </summary>
        public async Task ExportSamplesToFileAsync<T>(string filePath, IEnumerable<T> data) where T : class
        {
            try
            {
                using var streamWriter = new StreamWriter(filePath, false, Encoding.UTF8);
                using var csv = new CsvWriter(streamWriter, GetCsvConfiguration());

                await csv.WriteRecordsAsync(data);
            }
            catch (Exception ex)
            {
                throw new FileProcessingException(
                    $"خطا در ذخیره فایل CSV: {ex.Message}",
                    Path.GetFileName(filePath),
                    ex);
            }
        }

        #region Private Methods

        /// <summary>
        /// خواندن فایل CSV
        /// </summary>
        private async Task<List<Dictionary<string, string>>> ReadCsvFileAsync(string filePath)
        {
            using var reader = new StreamReader(filePath, Encoding.UTF8);
            return await ReadCsvAsync(reader);
        }

        /// <summary>
        /// خواندن از Stream
        /// </summary>
        private async Task<List<Dictionary<string, string>>> ReadCsvStreamAsync(Stream stream)
        {
            stream.Position = 0;
            using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
            return await ReadCsvAsync(reader);
        }

        /// <summary>
        /// خواندن CSV با CsvHelper
        /// </summary>
        private async Task<List<Dictionary<string, string>>> ReadCsvAsync(StreamReader reader)
        {
            var rows = new List<Dictionary<string, string>>();

            using var csv = new CsvReader(reader, GetCsvConfiguration());

            await csv.ReadAsync();
            csv.ReadHeader();
            var headers = csv.HeaderRecord;

            if (headers == null || !headers.Any())
            {
                throw new FileProcessingException("فایل CSV حاوی هیچ Header ای نیست");
            }

            while (await csv.ReadAsync())
            {
                var row = new Dictionary<string, string>();

                foreach (var header in headers)
                {
                    row[header] = csv.GetField(header) ?? string.Empty;
                }

                rows.Add(row);
            }

            return rows;
        }

        /// <summary>
        /// تنظیمات CsvHelper
        /// </summary>
        private CsvConfiguration GetCsvConfiguration()
        {
            return new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
                MissingFieldFound = null,
                BadDataFound = null,
                TrimOptions = TrimOptions.Trim,
                IgnoreBlankLines = true,
                Encoding = Encoding.UTF8
            };
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
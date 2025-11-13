using DocumentFormat.OpenXml.Office2016.Drawing.ChartDrawing;
using Infrastructure.Icp.Files.Models;

namespace Infrastructure.Icp.Files.Services
{
    /// <summary>
    /// سرویس اعتبارسنجی فایل
    /// </summary>
    public class FileValidationService
    {
        private const long MaxFileSizeBytes = 100 * 1024 * 1024; // 100 MB
        private static readonly string[] AllowedExtensions = { ".csv", ".xlsx", ".xls" };

        /// <summary>
        /// بررسی اعتبار فایل
        /// </summary>
        public FileValidationResult ValidateFile(string filePath)
        {
            var result = new FileValidationResult { IsValid = true };

            if (!File.Exists(filePath))
            {
                result.AddError("فایل وجود ندارد");
                return result;
            }

            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            if (!AllowedExtensions.Contains(extension))
            {
                result.AddError($"پسوند فایل مجاز نیست. پسوندهای مجاز: {string.Join(", ", AllowedExtensions)}");
                return result;
            }

            var fileInfo = new FileInfo(filePath);
            if (fileInfo.Length > MaxFileSizeBytes)
            {
                result.AddError($"حجم فایل بیشتر از حد مجاز است. حداکثر: {MaxFileSizeBytes / (1024 * 1024)} MB");
                return result;
            }

            if (fileInfo.Length == 0)
            {
                result.AddError("فایل خالی است");
                return result;
            }

            return result;
        }

        /// <summary>
        /// بررسی اعتبار Stream
        /// </summary>
        public FileValidationResult ValidateStream(Stream stream, string fileName)
        {
            var result = new FileValidationResult { IsValid = true };

            if (stream == null || !stream.CanRead)
            {
                result.AddError("Stream نامعتبر است");
                return result;
            }

            if (stream.Length == 0)
            {
                result.AddError("فایل خالی است");
                return result;
            }

            if (stream.Length > MaxFileSizeBytes)
            {
                result.AddError("حجم فایل بیشتر از حد مجاز است");
                return result;
            }

            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            if (!AllowedExtensions.Contains(extension))
            {
                result.AddError("پسوند فایل مجاز نیست");
                return result;
            }

            return result;
        }

        /// <summary>
        /// بررسی ساختار داده‌ها
        /// </summary>
        public FileValidationResult ValidateDataStructure(List<Dictionary<string, string>> rows)
        {
            var result = new FileValidationResult
            {
                IsValid = true,
                TotalRows = rows.Count
            };

            if (!rows.Any())
            {
                result.AddError("فایل حاوی هیچ داده‌ای نیست");
                return result;
            }

            var requiredColumns = new[] { "SampleId", "SampleName", "Weight", "Volume" };
            var firstRow = rows.First();
            var missingColumns = requiredColumns.Where(col => !firstRow.ContainsKey(col)).ToList();

            if (missingColumns.Any())
            {
                result.AddError($"ستون‌های الزامی وجود ندارند: {string.Join(", ", missingColumns)}");
            }

            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                foreach (var requiredCol in requiredColumns)
                {
                    if (row.ContainsKey(requiredCol) && string.IsNullOrWhiteSpace(row[requiredCol]))
                    {
                        result.AddRowError(i + 2, $"ستون {requiredCol} خالی است");
                    }
                }
            }

            result.ValidRows = rows.Count - result.RowErrors.Count;
            result.InvalidRows = result.RowErrors.Count;

            return result;
        }
    }
}

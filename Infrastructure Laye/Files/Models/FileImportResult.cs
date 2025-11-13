using Core.Icp.Domain.Entities.Samples;

namespace Infrastructure.Icp.Files.Models
{
    /// <summary>
    /// نتیجه Import فایل
    /// </summary>
    public class FileImportResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public List<Sample> Samples { get; set; } = new();
        public FileValidationResult ValidationResult { get; set; } = new();
        public int TotalRecords { get; set; }
        public int SuccessfulRecords { get; set; }
        public int FailedRecords { get; set; }
        public TimeSpan ProcessingTime { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();

        public static FileImportResult CreateSuccess(List<Sample> samples, FileValidationResult validationResult)
        {
            return new FileImportResult
            {
                Success = true,
                Message = "فایل با موفقیت پردازش شد",
                Samples = samples,
                ValidationResult = validationResult,
                TotalRecords = samples.Count,
                SuccessfulRecords = samples.Count,
                FailedRecords = 0
            };
        }

        public static FileImportResult CreateFailure(string message, FileValidationResult validationResult)
        {
            return new FileImportResult
            {
                Success = false,
                Message = message,
                ValidationResult = validationResult,
                TotalRecords = validationResult.TotalRows,
                SuccessfulRecords = 0,
                FailedRecords = validationResult.InvalidRows
            };
        }
    }
}
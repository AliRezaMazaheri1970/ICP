namespace Shared.Icp.DTOs.Reports
{
    /// <summary>
    /// نتیجه تولید گزارش
    /// </summary>
    public class ReportResultDto
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? FilePath { get; set; }
        public byte[]? FileContent { get; set; }
        public string FileName { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public string Format { get; set; } = string.Empty;
        public TimeSpan GenerationTime { get; set; }
        public DateTime GeneratedAt { get; set; }
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
        public Dictionary<string, object> Metadata { get; set; } = new();

        public static ReportResultDto CreateSuccess(string filePath, byte[] content, string fileName, string format)
        {
            return new ReportResultDto
            {
                Success = true,
                Message = "گزارش با موفقیت تولید شد",
                FilePath = filePath,
                FileContent = content,
                FileName = fileName,
                Format = format,
                FileSize = content.Length,
                GeneratedAt = DateTime.Now
            };
        }

        public static ReportResultDto CreateSuccess(byte[] content, string fileName, string format)
        {
            return new ReportResultDto
            {
                Success = true,
                Message = "گزارش با موفقیت تولید شد",
                FileContent = content,
                FileName = fileName,
                Format = format,
                FileSize = content.Length,
                GeneratedAt = DateTime.Now
            };
        }

        public static ReportResultDto CreateFailure(string message, List<string>? errors = null)
        {
            return new ReportResultDto
            {
                Success = false,
                Message = message,
                Errors = errors ?? new List<string>(),
                GeneratedAt = DateTime.Now
            };
        }
    }
}
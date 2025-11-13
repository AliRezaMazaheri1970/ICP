using Core.Icp.Domain.Enums;
using Shared.Icp.DTOs.Common;

namespace Shared.Icp.DTOs.Reports
{
    public class ReportHistoryDto : BaseDto
    {
        public string ReportName { get; set; } = string.Empty;
        public string ReportType { get; set; } = string.Empty;
        public ReportFormat Format { get; set; }
        public Guid? ProjectId { get; set; }
        public string? ProjectName { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string? FilePath { get; set; }
        public long FileSize { get; set; }
        public TimeSpan GenerationTime { get; set; }
        public DateTime GeneratedAt { get; set; }
        public string GeneratedBy { get; set; } = string.Empty;
        public ReportStatus Status { get; set; }
        public int DownloadCount { get; set; }
        public DateTime? LastDownloadedAt { get; set; }
        public string? OptionsJson { get; set; }
        public string? Notes { get; set; }
    }
}
using Core.Icp.Domain.Enums;

namespace Shared.Icp.DTOs.Reports
{
    public class TemplateInfoDto
    {
        public string Name { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public List<ReportFormat> SupportedFormats { get; set; } = new();
        public TemplateCategory Category { get; set; }
        public string Version { get; set; } = "1.0";
        public string? Author { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public bool IsActive { get; set; } = true;
        public bool IsDefault { get; set; } = false;
        public List<string> Tags { get; set; } = new();
        public List<TemplateParameter> RequiredParameters { get; set; } = new();
        public string? PreviewImage { get; set; }
    }
}
using Shared.Icp.DTOs.Common;

namespace Shared.Icp.DTOs.CRM
{
    /// <summary>
    /// DTO برای نمایش CRM
    /// </summary>
    public class CRMDto : BaseDto
    {
        public string CRMId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Manufacturer { get; set; }
        public string? LotNumber { get; set; }
        public DateTime? ExpirationDate { get; set; }
        public bool IsActive { get; set; }
        public string? Notes { get; set; }
        public int CertifiedValueCount { get; set; }
        public List<CRMValueDto> CertifiedValues { get; set; } = new();
    }
}
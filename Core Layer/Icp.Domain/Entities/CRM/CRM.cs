using Core.Icp.Domain.Base;

namespace Core.Icp.Domain.Entities.CRM
{
    /// <summary>
    /// Certified Reference Material
    /// </summary>
    public class CRM : BaseEntity
    {
        /// <summary>
        /// شناسه CRM (مثلاً NIST-123)
        /// </summary>
        public string CRMId { get; set; } = string.Empty;

        /// <summary>
        /// نام CRM
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// تولیدکننده/منبع
        /// </summary>
        public string? Manufacturer { get; set; }

        /// <summary>
        /// شماره سری
        /// </summary>
        public string? LotNumber { get; set; }

        /// <summary>
        /// تاریخ انقضا
        /// </summary>
        public DateTime? ExpirationDate { get; set; }

        /// <summary>
        /// مقادیر تایید شده عناصر
        /// </summary>
        public virtual ICollection<CRMValue> CertifiedValues { get; set; } = new List<CRMValue>();

        /// <summary>
        /// آیا این CRM فعال است؟
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// توضیحات
        /// </summary>
        public string? Notes { get; set; }
    }
}
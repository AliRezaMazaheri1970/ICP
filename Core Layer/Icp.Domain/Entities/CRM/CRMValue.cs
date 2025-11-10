using Core.Icp.Domain.Base;
using Core.Icp.Domain.Entities.Elements;

namespace Core.Icp.Domain.Entities.CRM
{
    /// <summary>
    /// مقدار تایید شده یک عنصر در CRM
    /// </summary>
    public class CRMValue : BaseEntity
    {
        /// <summary>
        /// شناسه CRM
        /// </summary>
        public int CRMId { get; set; }

        /// <summary>
        /// CRM مرتبط
        /// </summary>
        public virtual CRM CRM { get; set; } = null!;

        /// <summary>
        /// شناسه عنصر
        /// </summary>
        public int ElementId { get; set; }

        /// <summary>
        /// عنصر مرتبط
        /// </summary>
        public virtual Element Element { get; set; } = null!;

        /// <summary>
        /// مقدار تایید شده (ppm)
        /// </summary>
        public decimal CertifiedValue { get; set; }

        /// <summary>
        /// عدم قطعیت
        /// </summary>
        public decimal? Uncertainty { get; set; }

        /// <summary>
        /// حد پایین قابل قبول
        /// </summary>
        public decimal? LowerLimit { get; set; }

        /// <summary>
        /// حد بالای قابل قبول
        /// </summary>
        public decimal? UpperLimit { get; set; }

        /// <summary>
        /// واحد
        /// </summary>
        public string Unit { get; set; } = "ppm";
    }
}
using Core.Icp.Domain.Base;

namespace Core.Icp.Domain.Entities.CRM
{
    /// <summary>
    /// Represents a Certified Reference Material (CRM), a standard used for calibration and quality control.
    /// </summary>
    public class CRM : BaseEntity
    {
        /// <summary>
        /// Gets or sets the custom identifier for the CRM.
        /// </summary>
        public string CRMId { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the official name of the Certified Reference Material.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the name of the manufacturer that produced the CRM.
        /// </summary>
        public string? Manufacturer { get; set; }

        /// <summary>
        /// Gets or sets the lot number, which identifies a specific production batch of the CRM.
        /// </summary>
        public string? LotNumber { get; set; }

        /// <summary>
        /// Gets or sets the date on which the CRM's certification expires.
        /// </summary>
        public DateTime? ExpirationDate { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the CRM is currently active and in use.
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Gets or sets any relevant notes or additional information about the CRM.
        /// </summary>
        public string? Notes { get; set; }

        // Navigation Properties
        /// <summary>
        /// Gets or sets the collection of certified values for various elements contained within this CRM.
        /// </summary>
        public ICollection<CRMValue> CertifiedValues { get; set; } = new List<CRMValue>();
    }
}
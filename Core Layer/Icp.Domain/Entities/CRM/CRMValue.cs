using Core.Icp.Domain.Base;
using Core.Icp.Domain.Entities.Elements;

namespace Core.Icp.Domain.Entities.CRM
{
    /// <summary>
    ///     Represents the certified value of a specific element within a Certified Reference Material (CRM).
    /// </summary>
    public class CRMValue : BaseEntity
    {
        /// <summary>
        ///     Gets or sets the foreign key for the Certified Reference Material (CRM) this value belongs to.
        /// </summary>
        public Guid CRMId { get; set; }

        /// <summary>
        ///     Gets or sets the foreign key for the chemical element this certified value represents.
        /// </summary>
        public Guid ElementId { get; set; }

        /// <summary>
        ///     Gets or sets the officially certified concentration or value of the element.
        /// </summary>
        public decimal CertifiedValue { get; set; }

        /// <summary>
        ///     Gets or sets the measurement uncertainty associated with the certified value.
        /// </summary>
        public decimal? Uncertainty { get; set; }

        /// <summary>
        ///     Gets or sets the unit of measurement for the certified value, defaulting to "ppm" (parts per million).
        /// </summary>
        public string Unit { get; set; } = "ppm";

        #region Navigation Properties

        /// <summary>
        ///     Gets or sets the navigation property to the parent CRM entity.
        /// </summary>
        public virtual CRM CRM { get; set; } = null!;

        /// <summary>
        ///     Gets or sets the navigation property to the associated Element entity.
        /// </summary>
        public virtual Element Element { get; set; } = null!;

        #endregion
    }
}
using Shared.Icp.DTOs.Common;

namespace Shared.Icp.DTOs.Elements
{
    /// <summary>
    /// DTO برای نمایش عنصر
    /// </summary>
    public class ElementDto : BaseDto
    {
        public string Symbol { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int AtomicNumber { get; set; }
        public decimal AtomicMass { get; set; }
        public bool IsActive { get; set; }
        public int DisplayOrder { get; set; }
        public int IsotopeCount { get; set; }
    }
}
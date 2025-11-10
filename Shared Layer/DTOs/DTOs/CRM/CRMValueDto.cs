namespace Shared.Icp.DTOs.CRM
{
    /// <summary>
    /// DTO برای مقدار تایید شده CRM
    /// </summary>
    public class CRMValueDto
    {
        public int Id { get; set; }
        public int CRMId { get; set; }
        public int ElementId { get; set; }
        public string ElementSymbol { get; set; } = string.Empty;
        public string ElementName { get; set; } = string.Empty;
        public decimal CertifiedValue { get; set; }
        public decimal? Uncertainty { get; set; }
        public decimal? LowerLimit { get; set; }
        public decimal? UpperLimit { get; set; }
        public string Unit { get; set; } = "ppm";
    }
}
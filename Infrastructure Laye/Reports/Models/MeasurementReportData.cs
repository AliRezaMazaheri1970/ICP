namespace Infrastructure.Icp.Reports.Models
{
    /// <summary>
    /// داده‌های اندازه‌گیری برای گزارش
    /// </summary>
    public class MeasurementReportData
    {
        public string ElementSymbol { get; set; } = string.Empty;
        public int? Isotope { get; set; }
        public decimal NetIntensity { get; set; }
        public decimal? Concentration { get; set; }
        public decimal? FinalConcentration { get; set; }
        public string? Unit { get; set; }
        public bool IsValid { get; set; }

        public static MeasurementReportData FromMeasurement(Measurement measurement)
        {
            return new MeasurementReportData
            {
                ElementSymbol = measurement.ElementSymbol,
                Isotope = measurement.Isotope,
                NetIntensity = measurement.NetIntensity,
                Concentration = measurement.Concentration,
                FinalConcentration = measurement.FinalConcentration,
                Unit = "ppm",
                IsValid = measurement.IsValid
            };
        }
    }
}
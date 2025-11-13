using Core.Icp.Domain.Entities.Samples;

namespace Infrastructure.Icp.Reports.Models
{
    /// <summary>
    /// داده‌های نمونه برای گزارش
    /// </summary>
    public class SampleReportData
    {
        public string SampleId { get; set; } = string.Empty;
        public string SampleName { get; set; } = string.Empty;
        public DateTime RunDate { get; set; }
        public decimal Weight { get; set; }
        public decimal Volume { get; set; }
        public decimal DilutionFactor { get; set; }
        public string Status { get; set; } = string.Empty;
        public List<MeasurementReportData> Measurements { get; set; } = new();
        public Dictionary<string, string> Metadata { get; set; } = new();

        public static SampleReportData FromSample(Sample sample)
        {
            return new SampleReportData
            {
                SampleId = sample.SampleId,
                SampleName = sample.SampleName,
                RunDate = sample.RunDate,
                Weight = sample.Weight,
                Volume = sample.Volume,
                DilutionFactor = sample.DilutionFactor,
                Status = sample.Status.ToString(),
                Measurements = sample.Measurements
                    .Select(m => MeasurementReportData.FromMeasurement(m))
                    .ToList()
            };
        }
    }
}
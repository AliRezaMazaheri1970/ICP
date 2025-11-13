namespace Infrastructure.Icp.Reports.Models
{
    /// <summary>
    /// گزارش کنترل کیفیت
    /// </summary>
    public class QualityControlReport
    {
        public int TotalSamples { get; set; }
        public int PassedSamples { get; set; }
        public int FailedSamples { get; set; }
        public decimal PassRate { get; set; }

        public WeightCheckReport WeightCheck { get; set; } = new();
        public VolumeCheckReport VolumeCheck { get; set; } = new();
        public DFCheckReport DFCheck { get; set; } = new();
        public EmptyCheckReport EmptyCheck { get; set; } = new();

        public List<string> FailedSampleIds { get; set; } = new();
        public Dictionary<string, List<string>> FailureReasons { get; set; } = new();
    }
}
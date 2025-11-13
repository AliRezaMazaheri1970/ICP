namespace Infrastructure.Icp.Reports.Models
{
    public class VolumeCheckReport
    {
        public int TotalChecked { get; set; }
        public int Passed { get; set; }
        public int Failed { get; set; }
        public decimal MinVolume { get; set; }
        public decimal MaxVolume { get; set; }
        public decimal AverageVolume { get; set; }
        public List<string> FailedSamples { get; set; } = new();
    }
}

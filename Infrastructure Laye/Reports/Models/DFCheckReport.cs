namespace Infrastructure.Icp.Reports.Models
{
    public class DFCheckReport
    {
        public int TotalChecked { get; set; }
        public int Passed { get; set; }
        public int Failed { get; set; }
        public int MinDF { get; set; }
        public int MaxDF { get; set; }
        public double AverageDF { get; set; }
        public List<string> FailedSamples { get; set; } = new();
    }
}

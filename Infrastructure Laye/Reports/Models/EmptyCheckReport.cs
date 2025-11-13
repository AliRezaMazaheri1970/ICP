namespace Infrastructure.Icp.Reports.Models
{
    public class EmptyCheckReport
    {
        public int TotalChecked { get; set; }
        public int EmptyRows { get; set; }
        public int ValidRows { get; set; }
    }
}

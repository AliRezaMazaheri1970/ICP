namespace Shared.Icp.DTOs.QualityControl
{
    /// <summary>
    /// DTO برای کنترل کیفیت دسته‌ای
    /// </summary>
    public class BatchQualityCheckDto
    {
        public int ProjectId { get; set; }
        public List<int> SampleIds { get; set; } = new();
        public List<string> CheckTypes { get; set; } = new();
        public bool StopOnFirstFailure { get; set; } = false;
    }
}
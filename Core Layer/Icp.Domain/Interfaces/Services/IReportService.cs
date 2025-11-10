namespace Core.Icp.Domain.Interfaces.Services
{
    /// <summary>
    /// سرویس تولید گزارش
    /// </summary>
    public interface IReportService
    {
        /// <summary>
        /// تولید گزارش PDF
        /// </summary>
        Task<byte[]> GeneratePdfReportAsync(int projectId);

        /// <summary>
        /// تولید گزارش Excel
        /// </summary>
        Task<byte[]> GenerateExcelReportAsync(int projectId);

        /// <summary>
        /// صادرات نتایج به CSV
        /// </summary>
        Task<byte[]> ExportResultsToCsvAsync(int projectId);

        /// <summary>
        /// تولید گزارش کنترل کیفیت
        /// </summary>
        Task<byte[]> GenerateQualityControlReportAsync(int projectId);
    }
}
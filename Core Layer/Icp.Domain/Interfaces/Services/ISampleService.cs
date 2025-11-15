using Core.Icp.Domain.Entities.Samples;

namespace Core.Icp.Domain.Interfaces.Services
{
    /// <summary>
    /// سرویس دامنه برای مدیریت Sample ها.
    /// </summary>
    public interface ISampleService
    {
        /// همه نمونه‌ها (برای دیباگ یا گزارش کلی).
        Task<IEnumerable<Sample>> GetAllSamplesAsync();

        /// همه نمونه‌های یک پروژه.
        Task<IEnumerable<Sample>> GetSamplesByProjectIdAsync(Guid projectId);

        /// یک نمونه بر اساس Id.
        Task<Sample?> GetSampleByIdAsync(Guid id);

        /// ایجاد Sample جدید.
        Task<Sample> CreateSampleAsync(Sample sample);

        /// ویرایش Sample.
        Task<Sample> UpdateSampleAsync(Sample sample);

        /// حذف نرم Sample.
        Task<bool> DeleteSampleAsync(Guid id);
    }
}

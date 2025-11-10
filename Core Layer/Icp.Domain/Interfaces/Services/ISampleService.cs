using Core.Icp.Domain.Entities.Samples;
using Core.Icp.Domain.Enums;

namespace Core.Icp.Domain.Interfaces.Services
{
    /// <summary>
    /// سرویس مدیریت نمونه‌ها
    /// </summary>
    public interface ISampleService
    {
        /// <summary>
        /// دریافت تمام نمونه‌ها
        /// </summary>
        Task<IEnumerable<Sample>> GetAllSamplesAsync();

        /// <summary>
        /// دریافت نمونه با شناسه
        /// </summary>
        Task<Sample?> GetSampleByIdAsync(int id);

        /// <summary>
        /// دریافت نمونه‌های یک پروژه
        /// </summary>
        Task<IEnumerable<Sample>> GetSamplesByProjectIdAsync(int projectId);

        /// <summary>
        /// جستجوی نمونه
        /// </summary>
        Task<IEnumerable<Sample>> SearchSamplesAsync(string searchTerm);

        /// <summary>
        /// ایجاد نمونه جدید
        /// </summary>
        Task<Sample> CreateSampleAsync(Sample sample);

        /// <summary>
        /// ویرایش نمونه
        /// </summary>
        Task<Sample> UpdateSampleAsync(Sample sample);

        /// <summary>
        /// حذف نمونه
        /// </summary>
        Task<bool> DeleteSampleAsync(int id);

        /// <summary>
        /// تغییر وضعیت نمونه
        /// </summary>
        Task<Sample> ChangeSampleStatusAsync(int id, SampleStatus status);

        /// <summary>
        /// دریافت نمونه با اندازه‌گیری‌ها
        /// </summary>
        Task<Sample?> GetSampleWithMeasurementsAsync(int id);

        /// <summary>
        /// اضافه کردن اندازه‌گیری به نمونه
        /// </summary>
        Task AddMeasurementToSampleAsync(int sampleId, Measurement measurement);
    }
}
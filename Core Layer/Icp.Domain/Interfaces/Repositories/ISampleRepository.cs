using Core.Icp.Domain.Entities.Samples;

namespace Core.Icp.Domain.Interfaces.Repositories
{
    /// <summary>
    /// رابط Repository نمونه‌ها
    /// </summary>
    public interface ISampleRepository : IRepository<Sample>
    {
        /// <summary>
        /// دریافت نمونه‌های یک پروژه
        /// </summary>
        Task<IEnumerable<Sample>> GetByProjectIdAsync(int projectId);

        /// <summary>
        /// جستجوی نمونه با SampleId
        /// </summary>
        Task<Sample?> GetBySampleIdAsync(string sampleId);

        /// <summary>
        /// دریافت نمونه با تمام اندازه‌گیری‌ها
        /// </summary>
        Task<Sample?> GetWithMeasurementsAsync(int id);

        /// <summary>
        /// دریافت نمونه با کنترل‌های کیفیت
        /// </summary>
        Task<Sample?> GetWithQualityChecksAsync(int id);

        /// <summary>
        /// جستجوی نمونه‌ها
        /// </summary>
        Task<IEnumerable<Sample>> SearchAsync(string searchTerm);
    }
}
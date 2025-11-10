using Core.Icp.Domain.Entities.Projects;

namespace Core.Icp.Domain.Interfaces.Repositories
{
    /// <summary>
    /// رابط Repository پروژه‌ها
    /// </summary>
    public interface IProjectRepository : IRepository<Project>
    {
        /// <summary>
        /// دریافت پروژه با تمام نمونه‌ها
        /// </summary>
        Task<Project?> GetWithSamplesAsync(int id);

        /// <summary>
        /// دریافت پروژه‌های فعال
        /// </summary>
        Task<IEnumerable<Project>> GetActiveProjectsAsync();

        /// <summary>
        /// دریافت پروژه‌های کاربر
        /// </summary>
        Task<IEnumerable<Project>> GetUserProjectsAsync(string userId);
    }
}
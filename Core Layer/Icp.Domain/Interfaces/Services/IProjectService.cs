using Core.Icp.Domain.Entities.Projects;

namespace Core.Icp.Domain.Interfaces.Services
{
    /// <summary>
    /// سرویس مدیریت پروژه‌ها
    /// </summary>
    public interface IProjectService
    {
        /// <summary>
        /// دریافت تمام پروژه‌ها
        /// </summary>
        Task<IEnumerable<Project>> GetAllProjectsAsync();

        /// <summary>
        /// دریافت پروژه با شناسه
        /// </summary>
        Task<Project?> GetProjectByIdAsync(int id);

        /// <summary>
        /// ایجاد پروژه جدید
        /// </summary>
        Task<Project> CreateProjectAsync(Project project);

        /// <summary>
        /// ویرایش پروژه
        /// </summary>
        Task<Project> UpdateProjectAsync(Project project);

        /// <summary>
        /// حذف پروژه
        /// </summary>
        Task<bool> DeleteProjectAsync(int id);

        /// <summary>
        /// ذخیره پروژه در فایل
        /// </summary>
        Task<string> SaveProjectToFileAsync(int projectId, string filePath);

        /// <summary>
        /// بارگذاری پروژه از فایل
        /// </summary>
        Task<Project> LoadProjectFromFileAsync(string filePath);
    }
}
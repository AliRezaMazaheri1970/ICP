using Core.Icp.Domain.Entities.CRM;

namespace Core.Icp.Domain.Interfaces.Repositories
{
    /// <summary>
    /// رابط Repository مواد مرجع
    /// </summary>
    public interface ICRMRepository : IRepository<CRM>
    {
        /// <summary>
        /// دریافت CRM با شناسه CRM
        /// </summary>
        Task<CRM?> GetByCRMIdAsync(string crmId);

        /// <summary>
        /// دریافت CRM های فعال
        /// </summary>
        Task<IEnumerable<CRM>> GetActiveCRMsAsync();

        /// <summary>
        /// دریافت CRM با مقادیر تایید شده
        /// </summary>
        Task<CRM?> GetWithCertifiedValuesAsync(int id);
    }
}
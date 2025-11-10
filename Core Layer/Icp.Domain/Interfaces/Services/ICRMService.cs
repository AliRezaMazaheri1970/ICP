using Core.Icp.Domain.Entities.CRM;

namespace Core.Icp.Domain.Interfaces.Services
{
    /// <summary>
    /// سرویس مدیریت مواد مرجع
    /// </summary>
    public interface ICRMService
    {
        /// <summary>
        /// دریافت تمام CRM ها
        /// </summary>
        Task<IEnumerable<CRM>> GetAllCRMsAsync();

        /// <summary>
        /// دریافت CRM با شناسه
        /// </summary>
        Task<CRM?> GetCRMByIdAsync(int id);

        /// <summary>
        /// دریافت CRM های فعال
        /// </summary>
        Task<IEnumerable<CRM>> GetActiveCRMsAsync();

        /// <summary>
        /// ایجاد CRM جدید
        /// </summary>
        Task<CRM> CreateCRMAsync(CRM crm);

        /// <summary>
        /// ویرایش CRM
        /// </summary>
        Task<CRM> UpdateCRMAsync(CRM crm);

        /// <summary>
        /// حذف CRM
        /// </summary>
        Task<bool> DeleteCRMAsync(int id);

        /// <summary>
        /// دریافت مقدار تایید شده یک عنصر در CRM
        /// </summary>
        Task<CRMValue?> GetCertifiedValueAsync(int crmId, int elementId);

        /// <summary>
        /// مقایسه مقدار اندازه‌گیری شده با CRM
        /// </summary>
        Task<bool> CompareMeasurementWithCRMAsync(
            int crmId,
            int elementId,
            decimal measuredValue);
    }
}
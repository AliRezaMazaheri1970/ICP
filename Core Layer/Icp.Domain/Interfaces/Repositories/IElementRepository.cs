using Core.Icp.Domain.Entities.Elements;

namespace Core.Icp.Domain.Interfaces.Repositories
{
    /// <summary>
    /// رابط Repository عناصر
    /// </summary>
    public interface IElementRepository : IRepository<Element>
    {
        /// <summary>
        /// دریافت عنصر با نماد
        /// </summary>
        Task<Element?> GetBySymbolAsync(string symbol);

        /// <summary>
        /// دریافت عناصر فعال
        /// </summary>
        Task<IEnumerable<Element>> GetActiveElementsAsync();

        /// <summary>
        /// دریافت عنصر با ایزوتوپ‌ها
        /// </summary>
        Task<Element?> GetWithIsotopesAsync(int id);

        /// <summary>
        /// دریافت عنصر با منحنی‌های کالیبراسیون
        /// </summary>
        Task<Element?> GetWithCalibrationCurvesAsync(int id);
    }
}
using Application.Features.Reports.DTOs;
// اگر DTO در لایه Application است، اینترفیس را هم بهتر است در لایه Application/Interfaces بگذارید
// اما برای حفظ ساختار فعلی شما:

namespace Domain.Interfaces.Services;

public interface IReportService
{
    // دریافت داده‌ها به صورت Pivot (JSON) برای نمایش در گرید
    Task<PivotReportDto> GetProjectPivotReportAsync(Guid projectId, CancellationToken cancellationToken = default);

    // خروجی اکسل (بایت آرایه)
    Task<byte[]> ExportProjectToExcelAsync(Guid projectId, CancellationToken cancellationToken = default);
}
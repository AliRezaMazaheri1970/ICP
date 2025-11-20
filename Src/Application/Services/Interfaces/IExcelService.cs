namespace Application.Services.Interfaces;

using Domain.Entities; // برای دسترسی به کلاس Sample

public interface IExcelService
{
    // ورودی استریم فایل است و خروجی لیست نمونه‌ها
    Task<List<Sample>> ReadSamplesFromExcelAsync(Stream fileStream, CancellationToken cancellationToken);
}
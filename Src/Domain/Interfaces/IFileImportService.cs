using Domain.Entities;

namespace Application.Services.Interfaces;

public interface IFileImportService
{
    // تشخیص می‌دهد آیا این سرویس می‌تواند این فایل را پردازش کند؟
    bool CanSupport(string fileName);

    // پردازش فایل و خروجی لیست نمونه‌ها
    Task<List<Sample>> ProcessFileAsync(Stream fileStream, CancellationToken cancellationToken);
}
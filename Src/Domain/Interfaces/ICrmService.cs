using Domain.Entities;

namespace Domain.Interfaces.Services;

public interface ICrmService
{
    // محاسبه فاکتورهای اصلاح (Blank, Scale) برای یک عنصر خاص در یک پروژه
    Task<(double Blank, double Scale)> CalculateCorrectionFactorsAsync(
        Guid projectId,
        string elementName,
        CancellationToken cancellationToken = default);
}
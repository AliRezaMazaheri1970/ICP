// مسیر پیشنهادی: Src/Domain/Interfaces/Services/ICalibrationService.cs

using Domain.Entities;

namespace Domain.Interfaces.Services; // Namespace اصلاح شد

public interface ICalibrationService
{
    Task<CalibrationCurve> CalculateAndSaveCurveAsync(Guid projectId, string elementName, CancellationToken cancellationToken = default);

    double CalculateConcentration(double intensity, CalibrationCurve curve);
}
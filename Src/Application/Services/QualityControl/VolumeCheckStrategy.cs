using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces.Services;
using Domain.Models;

namespace Application.Services.QualityControl.Strategies;

public class VolumeCheckStrategy : IQualityCheckStrategy
{
    public CheckType CheckType => CheckType.VolumeCheck;

    public Task<(List<Guid>, string)> ExecuteAsync(List<Sample> samples, ProjectSettings settings, CancellationToken cancellationToken)
    {
        double min = settings.MinAcceptableVolume ?? 48.0;
        double max = settings.MaxAcceptableVolume ?? 52.0;

        var failedIds = samples
            .Where(s => s.Volume < min || s.Volume > max)
            .Select(s => s.Id)
            .ToList();

        return Task.FromResult((failedIds, $"Volume out of range ({min}-{max} mL)."));
    }
}
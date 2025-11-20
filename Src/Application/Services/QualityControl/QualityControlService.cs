using Core.Icp.Domain.Interfaces.Services;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces; // برای IUnitOfWork
using Domain.Models; // برای ProjectSettings و Summary

namespace Application.Services.QualityControl;

public class QualityControlService(IUnitOfWork unitOfWork) : IQualityControlService
{
    public async Task<int> RunAllChecksAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        int totalFailures = 0;
        totalFailures += await RunCheckAsync(projectId, CheckType.WeightCheck, cancellationToken);
        totalFailures += await RunCheckAsync(projectId, CheckType.VolumeCheck, cancellationToken);
        totalFailures += await RunCheckAsync(projectId, CheckType.DilutionFactorCheck, cancellationToken);
        totalFailures += await RunCheckAsync(projectId, CheckType.EmptyCheck, cancellationToken);
        return totalFailures;
    }

    public async Task<int> RunCheckAsync(Guid projectId, CheckType checkType, CancellationToken cancellationToken = default)
    {
        var projectRepo = unitOfWork.Repository<Project>();
        var project = await projectRepo.GetByIdAsync(projectId);

        if (project == null) throw new Exception("Project not found");

        // استفاده از متد GetSettings که به Project اضافه کردیم
        var settings = project.GetSettings<ProjectSettings>() ?? new ProjectSettings();

        if (!settings.AutoQualityControl) return 0;

        var sampleRepo = unitOfWork.Repository<Sample>();
        // نکته: نام اینکلودها باید دقیق باشد
        var samples = await sampleRepo.GetAsync(s => s.ProjectId == projectId, includeProperties: "QualityChecks,Measurements");
        var qualityCheckRepo = unitOfWork.Repository<QualityCheck>();

        int failedCount = 0;

        foreach (var sample in samples)
        {
            var (status, message) = EvaluateSample(sample, checkType, settings);

            var qcEntry = sample.QualityChecks.FirstOrDefault(q => q.CheckType == checkType);
            if (qcEntry == null)
            {
                qcEntry = new QualityCheck
                {
                    SampleId = sample.Id,
                    CheckType = checkType,
                    ProjectId = projectId
                };
                await qualityCheckRepo.AddAsync(qcEntry);
            }

            qcEntry.Status = status;
            qcEntry.Message = message;
            // qcEntry.LastModified = DateTime.UtcNow; // اگر در BaseEntity دارید

            if (status == CheckStatus.Fail) failedCount++;
        }

        await unitOfWork.CommitAsync(cancellationToken);
        return failedCount;
    }

    public async Task<ProjectQualitySummary> GetSummaryAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var checks = await unitOfWork.Repository<QualityCheck>()
            .GetAsync(q => q.Sample.ProjectId == projectId);

        return new ProjectQualitySummary
        {
            ProjectId = projectId,
            TotalChecks = checks.Count,
            PassedCount = checks.Count(c => c.Status == CheckStatus.Pass),
            FailedCount = checks.Count(c => c.Status == CheckStatus.Fail),
            WarningCount = checks.Count(c => c.Status == CheckStatus.Warning)
        };
    }

    private static (CheckStatus Status, string Message) EvaluateSample(Sample sample, CheckType type, ProjectSettings settings)
    {
        return type switch
        {
            CheckType.WeightCheck => ValidateRange(sample.Weight, settings.MinAcceptableWeight, settings.MaxAcceptableWeight, "Weight", "g"),
            CheckType.VolumeCheck => ValidateRange(sample.Volume, settings.MinAcceptableVolume, settings.MaxAcceptableVolume, "Volume", "mL"),
            CheckType.DilutionFactorCheck => ValidateRange(sample.DilutionFactor, settings.MinDilutionFactor, settings.MaxDilutionFactor, "DF", ""),
            CheckType.EmptyCheck => ValidateEmpty(sample),
            _ => (CheckStatus.Pending, "Check logic not implemented")
        };
    }

    private static (CheckStatus, string) ValidateRange(double value, double? min, double? max, string name, string unit)
    {
        if (value <= 0) return (CheckStatus.Fail, $"{name} is invalid (<= 0).");
        if (min.HasValue && value < min.Value) return (CheckStatus.Fail, $"{name} < Min ({min}).");
        if (max.HasValue && value > max.Value) return (CheckStatus.Fail, $"{name} > Max ({max}).");
        return (CheckStatus.Pass, "OK");
    }

    private static (CheckStatus, string) ValidateEmpty(Sample sample)
    {
        if (sample.Measurements == null || !sample.Measurements.Any())
            return (CheckStatus.Fail, "No measurements.");

        // مثال: اگر همه مقادیر ۰ باشند
        bool hasData = sample.Measurements.Any(m => m.Value != 0);
        return hasData ? (CheckStatus.Pass, "OK") : (CheckStatus.Fail, "Empty measurements.");
    }
}
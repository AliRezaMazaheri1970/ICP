using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using Domain.Interfaces.Services;
using Domain.Models;

namespace Application.Services.QualityControl;

public class QualityControlService(
    IUnitOfWork unitOfWork,
    IEnumerable<IQualityCheckStrategy> strategies // تزریق خودکار لیست تمام استراتژی‌ها
    ) : IQualityControlService
{
    public async Task<int> RunAllChecksAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        int totalFailures = 0;
        // ترتیب اجرا مهم است
        var checkOrder = new[]
        {
            CheckType.WeightCheck,
            CheckType.VolumeCheck,
            CheckType.DilutionFactorCheck,
            CheckType.EmptyCheck
        };

        foreach (var check in checkOrder)
        {
            totalFailures += await RunCheckAsync(projectId, check, cancellationToken);
        }

        return totalFailures;
    }

    public async Task<int> RunCheckAsync(Guid projectId, CheckType checkType, CancellationToken cancellationToken = default)
    {
        // 1. پیدا کردن استراتژی مناسب از لیست تزریق شده
        var strategy = strategies.FirstOrDefault(s => s.CheckType == checkType);
        if (strategy == null)
            throw new NotImplementedException($"No strategy implementation found for check type: {checkType}");

        // 2. دریافت پروژه و تنظیمات
        var projectRepo = unitOfWork.Repository<Project>();
        var project = await projectRepo.GetByIdAsync(projectId);
        if (project == null) throw new Exception($"Project {projectId} not found.");

        var settings = project.GetSettings<ProjectSettings>() ?? new ProjectSettings();
        if (!settings.AutoQualityControl) return 0;

        // 3. دریافت نمونه‌ها (فقط Sample، استانداردها معمولاً چک نمی‌شوند مگر نیاز باشد)
        var sampleRepo = unitOfWork.Repository<Sample>();
        var samples = (await sampleRepo.GetAsync(
            s => s.ProjectId == projectId && s.Type == SampleType.Sample,
            includeProperties: "QualityChecks,Measurements"
        )).ToList();

        if (!samples.Any()) return 0;

        // 4. اجرای استراتژی (منطق اصلی اینجاست)
        var (failedIds, failMessage) = await strategy.ExecuteAsync(samples, settings, cancellationToken);

        // 5. ثبت نتایج در دیتابیس
        var qcRepo = unitOfWork.Repository<QualityCheck>();
        int newFailures = 0;

        foreach (var sample in samples)
        {
            var status = failedIds.Contains(sample.Id) ? CheckStatus.Fail : CheckStatus.Pass;
            string message = status == CheckStatus.Fail ? failMessage : "OK";

            var existingCheck = sample.QualityChecks.FirstOrDefault(q => q.CheckType == checkType);

            if (existingCheck != null)
            {
                // آپدیت رکورد موجود
                if (existingCheck.Status != status) // فقط اگر وضعیت تغییر کرده آپدیت کن
                {
                    existingCheck.Status = status;
                    existingCheck.Message = message;
                    existingCheck.LastModified = DateTime.UtcNow;
                }
            }
            else
            {
                // ایجاد رکورد جدید
                await qcRepo.AddAsync(new QualityCheck
                {
                    ProjectId = projectId,
                    SampleId = sample.Id,
                    CheckType = checkType,
                    Status = status,
                    Message = message,
                    CreatedAt = DateTime.UtcNow
                });
            }

            if (status == CheckStatus.Fail) newFailures++;
        }

        await unitOfWork.CommitAsync(cancellationToken);
        return newFailures;
    }

    public async Task<ProjectQualitySummary> GetSummaryAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var checkRepo = unitOfWork.Repository<QualityCheck>();
        var checks = await checkRepo.GetAsync(q => q.ProjectId == projectId);

        return new ProjectQualitySummary
        {
            ProjectId = projectId,
            TotalChecks = checks.Count,
            PassedCount = checks.Count(c => c.Status == CheckStatus.Pass),
            FailedCount = checks.Count(c => c.Status == CheckStatus.Fail),
            WarningCount = checks.Count(c => c.Status == CheckStatus.Warning)
        };
    }
}
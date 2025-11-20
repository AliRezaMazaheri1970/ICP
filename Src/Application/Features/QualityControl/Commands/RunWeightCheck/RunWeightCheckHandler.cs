using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using MediatR;
using Shared.Wrapper;

namespace Application.Features.QualityControl.Commands.RunWeightCheck;

public class RunWeightCheckHandler(IUnitOfWork unitOfWork) : IRequestHandler<RunWeightCheckCommand, Result<int>>
{
    public async Task<Result<int>> Handle(RunWeightCheckCommand request, CancellationToken cancellationToken)
    {
        // 1. دریافت نمونه‌های پروژه
        var project = await unitOfWork.Repository<Project>()
            .GetByIdAsync(request.ProjectId);

        if (project == null)
            return await Result<int>.FailAsync("Project not found.");

        // فرض: تنظیمات وزن در پروژه ذخیره شده است (SettingsJson)
        // اینجا ساده‌سازی می‌کنیم و مقادیر ثابت می‌گیریم (یا از DTO می‌خوانیم)
        double minWeight = 0.1;
        double maxWeight = 10.0;

        var samples = await unitOfWork.Repository<Sample>()
            .GetAsync(s => s.ProjectId == request.ProjectId);

        int failedCount = 0;

        foreach (var sample in samples)
        {
            bool isPassed = sample.Weight >= minWeight && sample.Weight <= maxWeight;

            // ذخیره نتیجه QC (اگر Entity مربوط به QC دارید)
            // یا فقط علامت‌گذاری نمونه
            if (!isPassed)
            {
                failedCount++;
                // مثلا: sample.Status = SampleStatus.Rejected;
                // یا افزودن به لیست QualityChecks
            }
        }

        await unitOfWork.CommitAsync(cancellationToken);

        return await Result<int>.SuccessAsync(failedCount, $"Check completed. {failedCount} samples failed weight check.");
    }
}
// مسیر فایل: Application/Features/Samples/Commands/ImportSamples/ImportSamplesCommandHandler.cs

using Application.Services.Interfaces;
using Domain.Interfaces;
using Domain.Entities;
using MediatR;
using Shared.Wrapper;

namespace Application.Features.Samples.Commands.ImportSamples;

public class ImportSamplesCommandHandler(IExcelService excelService, IUnitOfWork unitOfWork)
    : IRequestHandler<ImportSamplesCommand, Result<int>>
{
    public async Task<Result<int>> Handle(ImportSamplesCommand request, CancellationToken cancellationToken)
    {
        // 1. بررسی وجود پروژه
        var projectRepo = unitOfWork.Repository<Project>(); // فرض بر اینکه Generic Repository از این نوع پشتیبانی می‌کند
        // اگر Repository<Project> ندارید، باید آن را به IUnitOfWork اضافه کنید یا از متد GetByIdAsync جنریک استفاده کنید
        // کد زیر فرض می‌کند Repository<Project> در دسترس است یا از متد جنریک استفاده می‌شود.
        // اگر IUnitOfWork شما متد جنریک Repository<T>() دارد:
        var project = await unitOfWork.Repository<Project>().GetByIdAsync(request.ProjectId);

        if (project == null)
            return await Result<int>.FailAsync("Project not found.");

        // 2. خواندن فایل
        var samples = await excelService.ReadSamplesFromExcelAsync(request.FileStream, cancellationToken);

        if (samples == null || !samples.Any())
            return await Result<int>.FailAsync("No valid samples found in the file.");

        // 3. ذخیره سازی
        var sampleRepo = unitOfWork.Repository<Sample>();
        int count = 0;

        foreach (var sample in samples)
        {
            sample.ProjectId = request.ProjectId; // اتصال به پروژه
            await sampleRepo.AddAsync(sample);
            count++;
        }

        await unitOfWork.CommitAsync(cancellationToken);

        return await Result<int>.SuccessAsync(count, $"{count} samples imported successfully into project '{project.Name}'.");
    }
}
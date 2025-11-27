using Application.Services.Interfaces;
using Domain.Entities;
using Domain.Interfaces;
using MediatR;
using Shared.Wrapper;

namespace Application.Features.Samples.Commands.ImportSamples;

public class ImportSamplesCommandHandler(
    IUnitOfWork unitOfWork,
    IEnumerable<IFileImportService> fileImporters // 👈 تزریق همه ایمپورترها
    ) : IRequestHandler<ImportSamplesCommand, Result<int>>
{
    public async Task<Result<int>> Handle(ImportSamplesCommand request, CancellationToken cancellationToken)
    {
        // 1. انتخاب پردازشگر مناسب بر اساس نام فایل
        var importer = fileImporters.FirstOrDefault(x => x.CanSupport(request.FileName));

        if (importer == null)
        {
            return await Result<int>.FailAsync($"فرمت فایل '{Path.GetExtension(request.FileName)}' پشتیبانی نمی‌شود. لطفاً فایل CSV یا Excel آپلود کنید.");
        }

        // 2. پردازش فایل
        // نکته: فرض بر این است که استریم فایل در request.FileStream قرار دارد
        var samples = await importer.ProcessFileAsync(request.FileStream, cancellationToken);

        if (samples.Count == 0)
        {
            return await Result<int>.FailAsync("هیچ داده معتبری در فایل یافت نشد.");
        }

        // 3. اتصال نمونه‌ها به پروژه
        var project = await unitOfWork.Repository<Project>().GetByIdAsync(request.ProjectId);
        if (project == null)
        {
            return await Result<int>.FailAsync("پروژه مورد نظر یافت نشد.");
        }

        foreach (var sample in samples)
        {
            sample.ProjectId = project.Id;
            // اینجا می‌توانید منطق اضافی مثل بررسی تکراری بودن را اضافه کنید
        }

        // 4. ذخیره در دیتابیس
        await unitOfWork.Repository<Sample>().AddRangeAsync(samples);
        await unitOfWork.CommitAsync(cancellationToken);

        return await Result<int>.SuccessAsync(samples.Count, $"{samples.Count} نمونه با موفقیت وارد شد.");
    }
}
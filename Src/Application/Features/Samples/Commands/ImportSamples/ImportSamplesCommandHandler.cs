using Application.Services.Interfaces;
using Domain.Interfaces;
using Domain.Entities;
using MediatR;
using Shared.Wrapper;

namespace Application.Features.Samples.Commands.ImportSamples;

public class ImportSamplesCommandHandler : IRequestHandler<ImportSamplesCommand, Result<int>>
{
    private readonly IExcelService _excelService;
    private readonly IUnitOfWork _unitOfWork;

    // استفاده از Constructor استاندارد (Explicit)
    public ImportSamplesCommandHandler(IExcelService excelService, IUnitOfWork unitOfWork)
    {
        _excelService = excelService;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<int>> Handle(ImportSamplesCommand request, CancellationToken cancellationToken)
    {
        // 1. خواندن داده‌ها از اکسل
        var samples = await _excelService.ReadSamplesFromExcelAsync(request.FileStream, cancellationToken);

        if (samples == null || !samples.Any())
            return await Result<int>.FailAsync("No samples found in the excel file.");

        // 2. اضافه کردن به دیتابیس
        var sampleRepository = _unitOfWork.Repository<Sample>();

        foreach (var sample in samples)
        {
            await sampleRepository.AddAsync(sample);
        }

        // 3. ذخیره نهایی
        await _unitOfWork.CommitAsync(cancellationToken);

        return await Result<int>.SuccessAsync(samples.Count, $"{samples.Count} samples imported successfully.");
    }
}
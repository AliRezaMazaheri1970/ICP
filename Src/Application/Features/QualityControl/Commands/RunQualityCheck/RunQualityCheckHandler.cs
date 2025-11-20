// مسیر فایل: Application/Features/QualityControl/Commands/RunQualityCheck/RunQualityCheckHandler.cs

using Core.Icp.Domain.Interfaces.Services;
using MediatR;
using Shared.Wrapper;

namespace Application.Features.QualityControl.Commands.RunQualityCheck;

public class RunQualityCheckHandler(IQualityControlService qcService)
    : IRequestHandler<RunQualityCheckCommand, Result<int>>
{
    public async Task<Result<int>> Handle(RunQualityCheckCommand request, CancellationToken cancellationToken)
    {
        int failedCount;

        if (request.SpecificCheckType.HasValue)
        {
            // اجرای یک چک خاص
            failedCount = await qcService.RunCheckAsync(
                request.ProjectId,
                request.SpecificCheckType.Value,
                cancellationToken);
        }
        else
        {
            // اجرای همه چک‌ها به ترتیب
            failedCount = await qcService.RunAllChecksAsync(
                request.ProjectId,
                cancellationToken);
        }

        return await Result<int>.SuccessAsync(failedCount, $"Quality Control completed. {failedCount} checks failed/warning.");
    }
}
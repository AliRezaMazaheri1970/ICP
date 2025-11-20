using MediatR;
using Shared.Wrapper;
using Domain.Interfaces;
using Domain.Entities;

namespace Application.Features.QualityControl.Commands.CorrectWeights;

// ورودی: لیست آیدی نمونه‌های انتخاب شده و وزن جدید
public record CorrectWeightsCommand(List<Guid> SampleIds, double NewWeight) : IRequest<Result<int>>;

public class CorrectWeightsHandler(IUnitOfWork unitOfWork) : IRequestHandler<CorrectWeightsCommand, Result<int>>
{
    public async Task<Result<int>> Handle(CorrectWeightsCommand request, CancellationToken cancellationToken)
    {
        if (request.NewWeight <= 0)
            return await Result<int>.FailAsync("New weight must be positive.");

        // دریافت نمونه‌ها به همراه اندازه‌گیری‌هایشان (Measurements) چون باید آن‌ها را اصلاح کنیم
        var sampleRepo = unitOfWork.Repository<Sample>();
        var samples = await sampleRepo.GetAsync(s => request.SampleIds.Contains(s.Id), includeProperties: "Measurements");

        int correctedCount = 0;

        foreach (var sample in samples)
        {
            double oldWeight = sample.Weight;

            if (oldWeight == 0) continue; // جلوگیری از تقسیم بر صفر

            // فرمول اصلاح غلظت: (وزن جدید / وزن قدیم) * غلظت قدیم
            double ratio = request.NewWeight / oldWeight;

            // اعمال روی تمام عناصر این نمونه
            foreach (var measurement in sample.Measurements)
            {
                measurement.Value *= ratio;
            }

            // آپدیت وزن نمونه
            sample.Weight = request.NewWeight;

            // نکته: چون از EF Core استفاده می‌کنیم، تغییرات روی آبجکت‌های loaded شده
            // به صورت خودکار Track می‌شوند و نیازی به صدا زدن UpdateAsync نیست.
            correctedCount++;
        }

        // ذخیره تغییرات در دیتابیس
        await unitOfWork.CommitAsync(cancellationToken);

        return await Result<int>.SuccessAsync(correctedCount, $"{correctedCount} samples corrected and recalculated.");
    }
}
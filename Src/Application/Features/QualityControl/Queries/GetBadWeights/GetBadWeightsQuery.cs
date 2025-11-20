using MediatR;
using Shared.Wrapper;
using Application.Features.QualityControl.DTOs;
using Domain.Entities;
using Domain.Interfaces;

namespace Application.Features.QualityControl.Queries.GetBadWeights;

// ورودی: حداقل و حداکثر وزن مجاز
public record GetBadWeightsQuery(double MinWeight, double MaxWeight) : IRequest<Result<List<WeightCheckDto>>>;

public class GetBadWeightsHandler(IUnitOfWork unitOfWork) : IRequestHandler<GetBadWeightsQuery, Result<List<WeightCheckDto>>>
{
    public async Task<Result<List<WeightCheckDto>>> Handle(GetBadWeightsQuery request, CancellationToken cancellationToken)
    {
        // پیدا کردن نمونه‌هایی که وزنشان کمتر از حداقل یا بیشتر از حداکثر است
        var badSamples = await unitOfWork.Repository<Sample>()
            .GetAsync(s => s.Weight < request.MinWeight || s.Weight > request.MaxWeight);

        var dtos = badSamples.Select(s => new WeightCheckDto
        {
            Id = s.Id,
            SolutionLabel = s.SolutionLabel,
            Weight = s.Weight
        }).ToList();

        return await Result<List<WeightCheckDto>>.SuccessAsync(dtos, $"Found {dtos.Count} samples out of range.");
    }
}
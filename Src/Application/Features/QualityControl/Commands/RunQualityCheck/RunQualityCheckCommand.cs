using Domain.Enums;
using MediatR;
using Shared.Wrapper;

namespace Application.Features.QualityControl.Commands.RunQualityCheck;

public record RunQualityCheckCommand(Guid ProjectId, CheckType? SpecificCheckType = null)
    : IRequest<Result<int>>;
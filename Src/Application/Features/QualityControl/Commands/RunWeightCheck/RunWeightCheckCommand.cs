using MediatR;
using Shared.Wrapper;

namespace Application.Features.QualityControl.Commands.RunWeightCheck;

// ورودی: شناسه پروژه
public record RunWeightCheckCommand(Guid ProjectId) : IRequest<Result<int>>;
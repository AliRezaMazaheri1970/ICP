using MediatR;
using Shared.Wrapper;

namespace Application.Features.Calibration.Commands.CalculateConcentrations;

public record CalculateConcentrationsCommand(Guid ProjectId) : IRequest<Result<int>>;
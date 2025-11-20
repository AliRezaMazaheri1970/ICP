using MediatR;
using Shared.Wrapper;
using Application.Features.QualityControl.DTOs;

namespace Application.Features.QualityControl.Commands.CorrectDrift;

public record CorrectDriftCommand(
    Guid ProjectId,
    string RmKeyword, // مثلا "RM" یا "Check"
    bool IsStepwise   // آیا اصلاح تدریجی باشد؟
) : IRequest<Result<List<DriftCorrectionResultDto>>>;
using MediatR;
using Shared.Wrapper;

namespace Application.Features.Calibration.Commands.ApplyCrmCorrection;

public record ApplyCrmCorrectionCommand(
    Guid ProjectId,
    string ElementName,
    double Blank,
    double Scale,
    bool ApplyToStandards = false // آیا روی استانداردها هم اعمال شود؟ (معمولاً خیر)
) : IRequest<Result<int>>;
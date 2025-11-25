using MediatR;
using Shared.Wrapper;
using Application.Features.Calibration.DTOs;

namespace Application.Features.Calibration.Commands.CalculateCurve;

// ورودی: شناسه پروژه و نام عنصر
public record CalculateCurveCommand(Guid ProjectId, string ElementName) : IRequest<Result<CalibrationCurveDto>>;
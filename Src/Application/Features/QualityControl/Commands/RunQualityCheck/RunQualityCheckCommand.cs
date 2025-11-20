// مسیر فایل: Application/Features/QualityControl/Commands/RunQualityCheck/RunQualityCheckCommand.cs

using Domain.Enums;
using MediatR;
using Shared.Wrapper;

namespace Application.Features.QualityControl.Commands.RunQualityCheck;

/// <summary>
/// دستور اجرای کنترل کیفیت.
/// اگر SpecificCheckType مقداردهی شود، فقط آن چک اجرا می‌شود.
/// در غیر این صورت، تمام چک‌های استاندارد (وزن، حجم، DF، خالی) اجرا می‌شوند.
/// </summary>
public record RunQualityCheckCommand(Guid ProjectId, CheckType? SpecificCheckType = null)
    : IRequest<Result<int>>; // خروجی: تعداد نمونه‌های رد شده (Fail)
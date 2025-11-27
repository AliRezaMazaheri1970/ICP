using Domain.Entities;
using Domain.Interfaces;
using MediatR;
using Shared.Wrapper;

namespace Application.Features.Users.Commands.UpdateUser;

public class UpdateUserCommand : IRequest<Result<Guid>>
{
    public Guid Id { get; set; }
    public required string FullName { get; set; }
    public required string Email { get; set; }
    public string? Position { get; set; }
    public string? PhoneNumber { get; set; }
    public bool IsActive { get; set; }
    public string? NewPassword { get; set; }

    // برای آپدیت نقش‌ها
    public List<string>? Roles { get; set; }
}

public class UpdateUserCommandHandler(IUnitOfWork unitOfWork) : IRequestHandler<UpdateUserCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(UpdateUserCommand request, CancellationToken cancellationToken)
    {
        // لود کردن کاربر همراه با نقش‌ها
        var users = await unitOfWork.Repository<User>().GetAsync(u => u.Id == request.Id, "Roles");
        var user = users.FirstOrDefault();

        if (user == null)
            return await Result<Guid>.FailAsync("کاربر یافت نشد.");

        // آپدیت فیلدها از روی پراپرتی‌های کامند
        user.FullName = request.FullName;
        user.Email = request.Email;
        user.Position = request.Position;
        user.PhoneNumber = request.PhoneNumber;
        user.IsActive = request.IsActive;

        // تغییر رمز عبور در صورت وجود
        if (!string.IsNullOrEmpty(request.NewPassword))
        {
            // TODO: Use secure hashing
            user.PasswordHash = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(request.NewPassword));
        }

        // آپدیت نقش‌ها
        if (request.Roles != null)
        {
            user.Roles.Clear();
            if (request.Roles.Any())
            {
                var rolesDb = await unitOfWork.Repository<Role>()
                    .GetAsync(r => request.Roles.Contains(r.Name));

                foreach (var role in rolesDb)
                {
                    user.Roles.Add(role);
                }
            }
        }

        await unitOfWork.Repository<User>().UpdateAsync(user);
        await unitOfWork.CommitAsync(cancellationToken);

        return await Result<Guid>.SuccessAsync(user.Id, "کاربر با موفقیت ویرایش شد.");
    }
}
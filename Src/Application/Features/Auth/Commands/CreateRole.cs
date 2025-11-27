using Domain.Entities;
using Domain.Interfaces;
using MediatR;
using Shared.Wrapper;

namespace Application.Features.Roles.Commands.CreateRole;

public class CreateRoleCommand : IRequest<Result<Guid>>
{
    public required string Name { get; set; }
    public string? DisplayName { get; set; }
    public string? Description { get; set; }
}

public class CreateRoleCommandHandler(IUnitOfWork unitOfWork) : IRequestHandler<CreateRoleCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateRoleCommand request, CancellationToken cancellationToken)
    {
        var existingRoles = await unitOfWork.Repository<Role>()
            .GetAsync(r => r.Name == request.Name);

        if (existingRoles.Any())
        {
            return await Result<Guid>.FailAsync("نقشی با این نام قبلاً وجود دارد.");
        }

        var role = new Role
        {
            Name = request.Name,
            DisplayName = request.DisplayName,
            Description = request.Description,
            IsActive = true
        };

        await unitOfWork.Repository<Role>().AddAsync(role);
        await unitOfWork.CommitAsync(cancellationToken);

        return await Result<Guid>.SuccessAsync(role.Id, "نقش جدید با موفقیت ایجاد شد.");
    }
}
using Domain.Entities;
using Domain.Interfaces;
using MediatR;
using Shared.Wrapper;

namespace Application.Features.Roles.Commands.UpdateRole;

public class UpdateRoleCommand : IRequest<Result<Guid>>
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public string? DisplayName { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; }
}

public class UpdateRoleCommandHandler(IUnitOfWork unitOfWork) : IRequestHandler<UpdateRoleCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(UpdateRoleCommand request, CancellationToken cancellationToken)
    {
        var role = await unitOfWork.Repository<Role>().GetByIdAsync(request.Id);

        if (role == null)
            return await Result<Guid>.FailAsync("نقش یافت نشد.");

        role.Name = request.Name;
        role.DisplayName = request.DisplayName;
        role.Description = request.Description;
        role.IsActive = request.IsActive;

        await unitOfWork.Repository<Role>().UpdateAsync(role);
        await unitOfWork.CommitAsync(cancellationToken);

        return await Result<Guid>.SuccessAsync(role.Id, "نقش ویرایش شد.");
    }
}
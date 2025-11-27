using Application.Features.Roles.Commands.CreateRole;
using Application.Features.Roles.Commands.DeleteRole;
using Application.Features.Roles.Commands.UpdateRole;
using Application.Features.Roles.Queries.GetAllRoles;
using Application.Features.Roles.Queries.GetRoleById;
using Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Shared.Wrapper;

namespace Api.Controllers;

[Route("api/[controller]")]
[ApiController]
public class RolesController(IMediator mediator) : ControllerBase
{
    // دریافت همه نقش‌ها
    [HttpGet]
    public async Task<ActionResult<Result<List<Role>>>> GetAll()
    {
        var result = await mediator.Send(new GetAllRolesQuery());
        return Ok(result);
    }

    // دریافت نقش با شناسه
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<Result<Role>>> GetById(Guid id)
    {
        var result = await mediator.Send(new GetRoleByIdQuery(id));
        return Ok(result);
    }

    // ایجاد نقش جدید
    // ورودی: JSON شامل Name, DisplayName, Description
    [HttpPost]
    public async Task<ActionResult<Result<Guid>>> Create([FromBody] CreateRoleCommand command)
    {
        var result = await mediator.Send(command);

        if (result.Succeeded)
            return Ok(result);

        return BadRequest(result);
    }

    // ویرایش نقش
    // ورودی: JSON شامل Id, Name, ...
    [HttpPut]
    public async Task<ActionResult<Result<Guid>>> Update([FromBody] UpdateRoleCommand command)
    {
        var result = await mediator.Send(command);

        if (result.Succeeded)
            return Ok(result);

        return BadRequest(result);
    }

    // حذف نقش
    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<Result<Guid>>> Delete(Guid id)
    {
        var result = await mediator.Send(new DeleteRoleCommand(id));

        if (result.Succeeded)
            return Ok(result);

        return BadRequest(result);
    }
}
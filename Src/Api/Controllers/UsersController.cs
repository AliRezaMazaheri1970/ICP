using Application.Features.Users.Commands.CreateUser;
using Application.Features.Users.Commands.DeleteUser;
using Application.Features.Users.Commands.UpdateUser;
using Application.Features.Users.Queries.GetAllUsers;
using Application.Features.Users.Queries.GetUserById;
using Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Shared.Wrapper;

namespace Api.Controllers;

[Route("api/[controller]")]
[ApiController]
public class UsersController(IMediator mediator) : ControllerBase
{
    // دریافت لیست همه کاربران
    [HttpGet]
    public async Task<ActionResult<Result<List<User>>>> GetAll()
    {
        var result = await mediator.Send(new GetAllUsersQuery());
        return Ok(result);
    }

    // دریافت یک کاربر با شناسه
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<Result<User>>> GetById(Guid id)
    {
        var result = await mediator.Send(new GetUserByIdQuery(id));
        return Ok(result);
    }

    // ایجاد کاربر جدید
    // ورودی: JSON شامل UserName, Email, Password, ConfirmPassword و ...
    [HttpPost]
    public async Task<ActionResult<Result<Guid>>> Create([FromBody] CreateUserCommand command)
    {
        var result = await mediator.Send(command);

        if (result.Succeeded)
            return Ok(result);

        return BadRequest(result);
    }

    // ویرایش کاربر
    // ورودی: JSON شامل Id, FullName, Email و ...
    [HttpPut]
    public async Task<ActionResult<Result<Guid>>> Update([FromBody] UpdateUserCommand command)
    {
        var result = await mediator.Send(command);

        if (result.Succeeded)
            return Ok(result);

        return BadRequest(result);
    }

    // حذف کاربر
    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<Result<Guid>>> Delete(Guid id)
    {
        var result = await mediator.Send(new DeleteUserCommand(id));

        if (result.Succeeded)
            return Ok(result);

        return BadRequest(result);
    }
}
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using BCrypt.Net;

namespace Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthController> _logger;
    private readonly IsatisDbContext _db;

    public AuthController(IConfiguration configuration, ILogger<AuthController> logger, IsatisDbContext db)
    {
        _configuration = configuration;
        _logger = logger;
        _db = db;
    }

    /// <summary>
    /// Authenticates a user and generates a JWT token.
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request)
    {
        _logger.LogInformation("Login attempt for user: {Username}", request.Username);

        var user = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Username == request.Username);

        if (user == null)
        {
            _logger.LogWarning("Login failed: user not found {Username}", request.Username);
            return Unauthorized(new LoginResponse(false, "Invalid username or password"));
        }

        // validate password
        bool isValid = BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash);
        if (!isValid)
        {
            _logger.LogWarning("Login failed: invalid password for {Username}", request.Username);
            return Unauthorized(new LoginResponse(false, "Invalid username or password"));
        }

        var token = GenerateJwtToken(user);

        _logger.LogInformation("Successful login for user: {Username}", request.Username);

        return Ok(new LoginResponse(
            IsAuthenticated: true,
            Message: "Login successful",
            Name: user.FullName,
            Token: token,
            Position: user.Position
        ));
    }

    /// <summary>
    /// Registers a new user.
    /// </summary>
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<ActionResult<RegisterResponse>> Register([FromBody] RegisterRequest request)
    {
        if (await _db.Users.AnyAsync(u => u.Username == request.Username))
        {
            return Conflict(new RegisterResponse(false, "Username already exists"));
        }

        var hashedPassword = BCrypt.Net.BCrypt.HashPassword(request.Password);

        var newUser = new Domain.Entities.Users
        {
            Id = Guid.NewGuid(),
            Username = request.Username,
            PasswordHash = hashedPassword,
            FullName = request.FullName ?? request.Username,
            Position = request.Position ?? "User"
        };

        _db.Users.Add(newUser);
        await _db.SaveChangesAsync();

        _logger.LogInformation("New user registered: {Username}", request.Username);

        return Ok(new RegisterResponse(true, "Registration successful"));
    }

    /// <summary>
    /// Retrieves the current authenticated user's information.
    /// </summary>
    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<UserInfo>> GetCurrentUser()
    {
        var username = User.FindFirst(ClaimTypes.Name)?.Value;
        if (string.IsNullOrEmpty(username))
            return Unauthorized();

        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Username == username);
        if (user == null)
            return NotFound();

        return Ok(new UserInfo(user.Username, user.FullName, user.Position));
    }

    /// <summary>
    /// Checks if there is an active session.
    /// </summary>
    [HttpGet("check-session")]
    [AllowAnonymous]
    public ActionResult CheckSession()
    {
        return Ok(new LoginResponse(false, "No active session"));
    }

    /// <summary>
    /// Logs out the current user.
    /// </summary>
    [HttpPost("logout")]
    [AllowAnonymous]
    public ActionResult Logout()
    {
        return Ok(new { Message = "Logged out successfully" });
    }

    /// <summary>
    /// Generates a JWT token for the specified user.
    /// </summary>
    private string GenerateJwtToken(Domain.Entities.Users user)
    {
        var jwtSettings = _configuration.GetSection("Jwt");
        var secret = jwtSettings["Secret"] ?? "IsatisICP-SuperSecret-Key-2024-Must-Be-At-Least-32-Characters! ";
        var issuer = jwtSettings["Issuer"] ?? "IsatisICP";
        var audience = jwtSettings["Audience"] ?? "IsatisICP-Users";
        var expiryMinutes = int.Parse(jwtSettings["AccessTokenExpiryMinutes"] ?? "60");

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.GivenName, user.FullName),
            new Claim(ClaimTypes.Role, user.Position),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expiryMinutes),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

/// <summary>
/// DTOs
/// </summary>
public record LoginRequest(string Username, string Password, bool RememberMe = false);
public record LoginResponse(bool IsAuthenticated, string Message, string Name = "", string Token = "", string Position = "");
public record RegisterRequest(string Username, string Password, string? FullName, string? Position);
public record RegisterResponse(bool Succeeded, string Message);
public record UserInfo(string Username, string FullName, string Position);

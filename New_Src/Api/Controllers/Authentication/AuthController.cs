using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Api.Controllers;

/// <summary>
/// Handles user authentication, registration, and session management.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthController> _logger;

    private static readonly List<UserRecord> _users = new()
    {
        new UserRecord("admin", "admin123", "Administrator", "Admin"),
        new UserRecord("analyst", "analyst123", "Lab Analyst", "Analyst"),
        new UserRecord("guest", "guest", "Guest User", "Viewer")
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="AuthController"/> class.
    /// </summary>
    /// <param name="configuration">The configuration settings.</param>
    /// <param name="logger">The logger instance.</param>
    public AuthController(IConfiguration configuration, ILogger<AuthController> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Authenticates a user and generates a JWT token.
    /// </summary>
    /// <param name="request">The login request containing username and password.</param>
    /// <returns>A login response with the JWT token if successful.</returns>
    [HttpPost("login")]
    [AllowAnonymous]
    public ActionResult<LoginResponse> Login([FromBody] LoginRequest request)
    {
        _logger.LogInformation("Login attempt for user: {Username}", request.Username);

        var user = _users.FirstOrDefault(u =>
            u.Username.Equals(request.Username, StringComparison.OrdinalIgnoreCase) &&
            u.Password == request.Password);

        if (user == null)
        {
            _logger.LogWarning("Failed login attempt for user: {Username}", request.Username);
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
    /// <param name="request">The registration details.</param>
    /// <returns>A response indicating the result of the registration.</returns>
    [HttpPost("register")]
    [AllowAnonymous]
    public ActionResult<RegisterResponse> Register([FromBody] RegisterRequest request)
    {
        if (_users.Any(u => u.Username.Equals(request.Username, StringComparison.OrdinalIgnoreCase)))
        {
            return Conflict(new RegisterResponse(false, "Username already exists"));
        }

        var newUser = new UserRecord(
            request.Username,
            request.Password,
            request.FullName ?? request.Username,
            request.Position ?? "Analyst"
        );
        _users.Add(newUser);

        _logger.LogInformation("New user registered: {Username}", request.Username);

        return Ok(new RegisterResponse(true, "Registration successful"));
    }

    /// <summary>
    /// Retrieves the current authenticated user's information.
    /// </summary>
    /// <returns>The user information details.</returns>
    [HttpGet("me")]
    [Authorize]
    public ActionResult<UserInfo> GetCurrentUser()
    {
        var username = User.FindFirst(ClaimTypes.Name)?.Value;

        if (string.IsNullOrEmpty(username))
            return Unauthorized();

        var user = _users.FirstOrDefault(u => u.Username == username);
        if (user == null)
            return NotFound();

        return Ok(new UserInfo(user.Username, user.FullName, user.Position));
    }

    /// <summary>
    /// Checks if there is an active session.
    /// </summary>
    /// <returns>The session check result.</returns>
    [HttpGet("check-session")]
    [AllowAnonymous]
    public ActionResult CheckSession()
    {
        return Ok(new LoginResponse(false, "No active session"));
    }

    /// <summary>
    /// Logs out the current user.
    /// </summary>
    /// <returns>A message indicating logout success.</returns>
    [HttpPost("logout")]
    [AllowAnonymous]
    public ActionResult Logout()
    {
        return Ok(new { Message = "Logged out successfully" });
    }

    /// <summary>
    /// Generates a JWT token for the specified user.
    /// </summary>
    /// <param name="user">The user to generate the token for.</param>
    /// <returns>The generated JWT token string.</returns>
    private string GenerateJwtToken(UserRecord user)
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

public record LoginRequest(string Username, string Password, bool RememberMe = false);
public record LoginResponse(bool IsAuthenticated, string Message, string Name = "", string Token = "", string Position = "");
public record RegisterRequest(string Username, string Password, string? FullName, string? Position);
public record RegisterResponse(bool Succeeded, string Message);
public record UserInfo(string Username, string FullName, string Position);
public record UserRecord(string Username, string Password, string FullName, string Position);
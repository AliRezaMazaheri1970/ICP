using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthController> _logger;

    // In real version use database
    private static readonly List<UserRecord> _users = new()
    {
        new UserRecord("admin", "admin123", "Administrator", "Admin"),
        new UserRecord("analyst", "analyst123", "Lab Analyst", "Analyst"),
        new UserRecord("guest", "guest", "Guest User", "Viewer")
    };

    public AuthController(IConfiguration configuration, ILogger<AuthController> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// POST /api/auth/login
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    public ActionResult<LoginResponse> Login([FromBody] LoginRequest request)
    {
        _logger.LogInformation("Login attempt for user: {Username}", request.Username);

        // Find user
        var user = _users.FirstOrDefault(u =>
            u.Username.Equals(request.Username, StringComparison.OrdinalIgnoreCase) &&
            u.Password == request.Password);

        if (user == null)
        {
            _logger.LogWarning("Failed login attempt for user: {Username}", request.Username);
            return Unauthorized(new LoginResponse(false, "Invalid username or password"));
        }

        // Generate JWT Token
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
    /// POST /api/auth/register
    /// </summary>
    [HttpPost("register")]
    [AllowAnonymous]
    public ActionResult<RegisterResponse> Register([FromBody] RegisterRequest request)
    {
        // Check if user already exists
        if (_users.Any(u => u.Username.Equals(request.Username, StringComparison.OrdinalIgnoreCase)))
        {
            return Conflict(new RegisterResponse(false, "Username already exists"));
        }

        // Add new user
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
    /// GET /api/auth/me - Get current user info
    /// </summary>
    [HttpGet("me")]
    [Authorize]
    public ActionResult<UserInfo> GetCurrentUser()
    {
        var username = User.FindFirst(ClaimTypes.Name)?.Value;
        var position = User.FindFirst(ClaimTypes.Role)?.Value;

        if (string.IsNullOrEmpty(username))
            return Unauthorized();

        var user = _users.FirstOrDefault(u => u.Username == username);
        if (user == null)
            return NotFound();

        return Ok(new UserInfo(user.Username, user.FullName, user.Position));
    }

    /// <summary>
    /// GET /api/auth/check-session - Check active session
    /// </summary>
    [HttpGet("check-session")]
    [AllowAnonymous]
    public ActionResult CheckSession()
    {
        // In real version use Cookie or Session
        return Ok(new LoginResponse(false, "No active session"));
    }

    /// <summary>
    /// POST /api/auth/logout
    /// </summary>
    [HttpPost("logout")]
    [AllowAnonymous]
    public ActionResult Logout()
    {
        // In real version clear Cookie/Session
        return Ok(new { Message = "Logged out successfully" });
    }

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
            new Claim(ClaimTypes.Role, user. Position),
            new Claim(JwtRegisteredClaimNames. Jti, Guid.NewGuid().ToString())
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

// DTOs
public record LoginRequest(string Username, string Password, bool RememberMe = false);
public record LoginResponse(bool IsAuthenticated, string Message, string Name = "", string Token = "", string Position = "");
public record RegisterRequest(string Username, string Password, string? FullName, string? Position);
public record RegisterResponse(bool Succeeded, string Message);
public record UserInfo(string Username, string FullName, string Position);
public record UserRecord(string Username, string Password, string FullName, string Position);
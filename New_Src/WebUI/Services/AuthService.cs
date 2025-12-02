using System.Text.Json;
using System.Text.Json.Serialization;

namespace WebUI.Services;

// ============================================
// DTOs - مطابق با فرمت API
// ============================================

public class LoginRequest
{
    [JsonPropertyName("username")]
    public string Username { get; set; } = "";

    [JsonPropertyName("password")]
    public string Password { get; set; } = "";
}

public class RegisterRequest
{
    [JsonPropertyName("username")]
    public string Username { get; set; } = "";

    [JsonPropertyName("email")]
    public string Email { get; set; } = "";

    [JsonPropertyName("password")]
    public string Password { get; set; } = "";

    [JsonPropertyName("firstName")]
    public string? FirstName { get; set; }

    [JsonPropertyName("lastName")]
    public string? LastName { get; set; }

    [JsonPropertyName("role")]
    public string? Role { get; set; }
}

public class UserDto
{
    [JsonPropertyName("userId")]
    public string? UserId { get; set; }

    [JsonPropertyName("username")]
    public string? Username { get; set; }

    [JsonPropertyName("email")]
    public string? Email { get; set; }

    [JsonPropertyName("firstName")]
    public string? FirstName { get; set; }

    [JsonPropertyName("lastName")]
    public string? LastName { get; set; }

    [JsonPropertyName("role")]
    public string? Role { get; set; }

    [JsonPropertyName("isActive")]
    public bool IsActive { get; set; }

    public string FullName => $"{FirstName} {LastName}".Trim();
}

public class ApiLoginResponse
{
    [JsonPropertyName("succeeded")]
    public bool Succeeded { get; set; }

    [JsonPropertyName("accessToken")]
    public string? AccessToken { get; set; }

    [JsonPropertyName("refreshToken")]
    public string? RefreshToken { get; set; }

    [JsonPropertyName("expiresAt")]
    public DateTime? ExpiresAt { get; set; }

    [JsonPropertyName("user")]
    public UserDto? User { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }
}

public class ApiRegisterResponse
{
    [JsonPropertyName("succeeded")]
    public bool Succeeded { get; set; }

    [JsonPropertyName("user")]
    public UserDto? User { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }
}

// نتیجه داخلی برای UI
public record AuthResult(
    bool IsAuthenticated,
    string Message,
    string Name = "User",
    string Token = "",
    string Position = "User"
);

// ============================================
// Auth Service
// ============================================

public class AuthService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<AuthService> _logger;
    private static AuthResult? _currentUser;
    private static string? _accessToken;
    private static UserDto? _userInfo;

    public AuthService(IHttpClientFactory clientFactory, ILogger<AuthService> logger)
    {
        _httpClient = clientFactory.CreateClient("Api");
        _logger = logger;
    }

    /// <summary>
    /// ورود کاربر
    /// </summary>
    public async Task<AuthResult> LoginAsync(string username, string password, bool rememberMe = false)
    {
        try
        {
            var request = new LoginRequest
            {
                Username = username,
                Password = password
            };

            _logger.LogInformation("Attempting login for user: {Username}", username);

            var response = await _httpClient.PostAsJsonAsync("auth/login", request);
            var content = await response.Content.ReadAsStringAsync();

            _logger.LogDebug("Login response: {Content}", content);

            var loginResponse = JsonSerializer.Deserialize<ApiLoginResponse>(content,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (loginResponse == null)
            {
                return new AuthResult(false, "Invalid server response");
            }

            if (loginResponse.Succeeded && loginResponse.User != null)
            {
                _accessToken = loginResponse.AccessToken;
                _userInfo = loginResponse.User;

                var fullName = loginResponse.User.FullName;
                if (string.IsNullOrWhiteSpace(fullName))
                {
                    fullName = loginResponse.User.Username ?? "User";
                }

                _currentUser = new AuthResult(
                    IsAuthenticated: true,
                    Message: "Login successful",
                    Name: fullName,
                    Token: loginResponse.AccessToken ?? "",
                    Position: loginResponse.User.Role ?? "User"
                );

                _logger.LogInformation("User {Username} logged in successfully", username);
                return _currentUser;
            }

            var errorMessage = loginResponse.Error ?? "Invalid username or password";
            _logger.LogWarning("Login failed for {Username}: {Error}", username, errorMessage);
            return new AuthResult(false, errorMessage);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Cannot connect to API");
            return new AuthResult(false, "Cannot connect to server. Please try again later.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Login error");
            return new AuthResult(false, "An error occurred during login.");
        }
    }

    /// <summary>
    /// ثبت‌نام کاربر جدید
    /// </summary>
    public async Task<AuthResult> RegisterAsync(string username, string password, string? fullName, string? position)
    {
        try
        {
            // جدا کردن نام و نام خانوادگی
            var nameParts = (fullName ?? "").Split(' ', 2);
            var firstName = nameParts.Length > 0 ? nameParts[0] : username;
            var lastName = nameParts.Length > 1 ? nameParts[1] : "";

            var request = new RegisterRequest
            {
                Username = username,
                Email = username.Contains("@") ? username : $"{username}@isatis.local",
                Password = password,
                FirstName = firstName,
                LastName = lastName,
                Role = position ?? "Viewer"
            };

            _logger.LogInformation("Attempting registration for user: {Username}", username);

            var response = await _httpClient.PostAsJsonAsync("auth/register", request);
            var content = await response.Content.ReadAsStringAsync();

            _logger.LogDebug("Register response: {Content}", content);

            if (response.IsSuccessStatusCode)
            {
                var registerResponse = JsonSerializer.Deserialize<ApiRegisterResponse>(content,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (registerResponse?.Succeeded == true)
                {
                    _logger.LogInformation("User {Username} registered successfully", username);
                    return new AuthResult(true, "Account created successfully!  Please sign in.");
                }

                return new AuthResult(false, registerResponse?.Error ?? "Registration failed");
            }

            if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
            {
                return new AuthResult(false, "Username or email already exists");
            }

            // Try to parse error from response
            try
            {
                var errorResponse = JsonSerializer.Deserialize<ApiRegisterResponse>(content,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                return new AuthResult(false, errorResponse?.Error ?? "Registration failed");
            }
            catch
            {
                return new AuthResult(false, $"Registration failed: {response.StatusCode}");
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Cannot connect to API for registration");
            return new AuthResult(false, "Cannot connect to server. Please try again later.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Registration error");
            return new AuthResult(false, "An error occurred during registration.");
        }
    }

    /// <summary>
    /// بررسی session فعال
    /// </summary>
    public Task<AuthResult> CheckAutoLoginAsync()
    {
        if (_currentUser != null && _currentUser.IsAuthenticated)
        {
            return Task.FromResult(_currentUser);
        }
        return Task.FromResult(new AuthResult(false, "No active session"));
    }

    /// <summary>
    /// ورود مهمان
    /// </summary>
    public AuthResult GuestLogin()
    {
        _currentUser = new AuthResult(true, "Guest Login", "Guest", "", "Guest");
        _userInfo = null;
        _accessToken = null;
        return _currentUser;
    }

    /// <summary>
    /// خروج
    /// </summary>
    public async Task LogoutAsync()
    {
        try
        {
            if (!string.IsNullOrEmpty(_accessToken))
            {
                _httpClient.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _accessToken);
                await _httpClient.PostAsync("auth/logout", null);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Logout error");
        }
        finally
        {
            _currentUser = null;
            _accessToken = null;
            _userInfo = null;
        }
    }

    /// <summary>
    /// دریافت کاربر فعلی
    /// </summary>
    public AuthResult? GetCurrentUser() => _currentUser;

    /// <summary>
    /// دریافت اطلاعات کامل کاربر
    /// </summary>
    public UserDto? GetUserInfo() => _userInfo;

    /// <summary>
    /// دریافت Access Token
    /// </summary>
    public string? GetAccessToken() => _accessToken;
}
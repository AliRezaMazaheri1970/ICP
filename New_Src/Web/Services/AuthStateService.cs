using System.Security.Claims;
using Application.DTOs;
using Microsoft.AspNetCore.Components.Authorization;
using Web.Interface;

namespace Web.Services;

public class AuthStateService : IAuthStateService
{
    private readonly CustomAuthStateProvider _authStateProvider;

    public UserDto? CurrentUser { get; private set; }
    public string? AccessToken { get; private set; }

    public AuthStateService(AuthenticationStateProvider authStateProvider)
    {
        _authStateProvider = (CustomAuthStateProvider)authStateProvider;
    }

    public Task<bool> LoginAsync(AuthResult authResult)
    {
        if (!authResult.Succeeded || authResult.User == null)
            return Task.FromResult(false);

        CurrentUser = authResult.User;
        AccessToken = authResult.AccessToken;

        var claims = new List<Claim>
        {
            new(ClaimTypes. NameIdentifier, authResult.User.UserId.ToString()),
            new(ClaimTypes.Name, authResult.User.Username),
            new(ClaimTypes.Email, authResult.User.Email),
            new(ClaimTypes.Role, authResult.User.Role)
        };

        var identity = new ClaimsIdentity(claims, "jwt");
        var user = new ClaimsPrincipal(identity);

        _authStateProvider.SetUser(user);

        return Task.FromResult(true);
    }

    public void Logout()
    {
        CurrentUser = null;
        AccessToken = null;
        _authStateProvider.Logout();
    }
}
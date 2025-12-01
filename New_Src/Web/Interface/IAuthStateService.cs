using Application.DTOs;

namespace Web.Interface;

public interface IAuthStateService
{
    Task<bool> LoginAsync(AuthResult authResult);
    void Logout();
    UserDto? CurrentUser { get; }
    string? AccessToken { get; }
}
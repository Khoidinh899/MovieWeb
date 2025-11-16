using MovieWeb.Models.Entities;
using MovieWeb.Models.DTOs;

namespace MovieWeb.Services.Interfaces
{
    public interface IAuthService
    {
        Task<AuthResult> RegisterAsync(RegisterDto model);
        Task<AuthResult> LoginAsync(LoginDto model);
        Task<bool> LogoutAsync();
        Task<bool> ForgotPasswordAsync(string email);
        Task<AuthResult> ResetPasswordAsync(ResetPasswordDto model);
        Task<User?> GetCurrentUserAsync();
        string GenerateJwtToken(User user);
    }
}

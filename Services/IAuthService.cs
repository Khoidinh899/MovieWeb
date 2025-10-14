using MovieWeb.Models.Entities;
using MovieWeb.Models.DTOs;

namespace MovieWeb.Services
{
    public interface IAuthService
    {
        Task<AuthResult> RegisterAsync(RegisterDto model);
        Task<AuthResult> LoginAsync(LoginDto model);
        Task<bool> LogoutAsync();
        Task<AuthResult> ConfirmEmailAsync(string userId, string token);
        Task<bool> ForgotPasswordAsync(string email);
        Task<AuthResult> ResetPasswordAsync(ResetPasswordDto model);
        Task<User?> GetCurrentUserAsync();
        string GenerateJwtToken(User user);
        // ✅ THÊM 2 DÒNG NÀY VÀO ✅
        Task<UserProfileDto?> GetUserProfileAsync(int userId);
        Task<ProfileResult> UpdateUserProfileAsync(int userId, UpdateProfileDto model);
    }
}
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Net;
using MovieWeb.Models.Entities;
using MovieWeb.Models.DTOs;
using MovieWeb.Data;
using Microsoft.EntityFrameworkCore;
using MovieWeb.Services.Interfaces;

namespace MovieWeb.Services
{
    public class AuthService : IAuthService
    {
        private readonly UserManager<User> _userManager;
        private readonly SignInManager<User> _signInManager;
        private readonly MovieWebDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly IEmailService _emailService;
        private readonly ILogger<AuthService> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public AuthService(
            UserManager<User> userManager,
            SignInManager<User> signInManager,
            MovieWebDbContext context,
            IConfiguration configuration,
            IEmailService emailService,
            ILogger<AuthService> logger,
            IHttpContextAccessor httpContextAccessor)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _context = context;
            _configuration = configuration;
            _emailService = emailService;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
        }

        // ==========================================================
        // REGISTER
        // ==========================================================

        public async Task<AuthResult> RegisterAsync(RegisterDto model)
        {
            try
            {
                if (await _userManager.FindByEmailAsync(model.Email) != null)
                    return AuthResult.Failed("Email đã được sử dụng");

                if (await _userManager.FindByNameAsync(model.Username) != null)
                    return AuthResult.Failed("Tên đăng nhập đã được sử dụng");

                // Handle fullname → firstname + lastname
                var parts = model.FullName?.Trim().Split(' ') ?? Array.Empty<string>();
                var firstName = parts.Length > 0 ? parts.Last() : "";
                var lastName = parts.Length > 1 ? string.Join(" ", parts.Take(parts.Length - 1)) : "";

                var user = new User
                {
                    UserName = model.Username,
                    Email = model.Email,
                    FirstName = firstName,
                    LastName = lastName,
                    RoleId = 2,
                    IsActive = true,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };

                var result = await _userManager.CreateAsync(user, model.Password);
                if (!result.Succeeded)
                    return AuthResult.Failed(result.Errors.Select(e => e.Description).ToList());

                var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                var baseUrl = _configuration["AppSettings:BaseUrl"] ?? "https://localhost:5001";
                var confirmUrl = $"{baseUrl}/auth/confirm-email?userId={user.Id}&token={Uri.EscapeDataString(token)}";

                await _emailService.SendEmailConfirmationAsync(user.Email!, model.FullName, confirmUrl);

                return AuthResult.Success("Đăng ký thành công. Vui lòng xác thực email.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during registration");
                return AuthResult.Failed("Có lỗi xảy ra trong quá trình đăng ký");
            }
        }

        // ==========================================================
        // LOGIN
        // ==========================================================

        public async Task<AuthResult> LoginAsync(LoginDto model)
        {
            try
            {
                _logger.LogInformation($"Attempting login for user: {model.Email}");

                var user = await _userManager.FindByEmailAsync(model.Email);
                if (user == null)
                {
                    return AuthResult.Failed("Email hoặc mật khẩu không đúng");
                }

                var result = await _signInManager.CheckPasswordSignInAsync(user, model.Password, lockoutOnFailure: false);
                if (!result.Succeeded)
                {
                    return AuthResult.Failed("Email hoặc mật khẩu không đúng");
                }

                // ==== 💡 KIỂM TRA TÀI KHOẢN BỊ KHÓA HOẶC BỊ HẠN CHẾ ====
                if (user.IsActive == false)
                {
                    _logger.LogWarning($"Login failed for user {model.Email}: Account is locked/disabled (IsActive = false).");
                    return AuthResult.Failed("Tài khoản (Email) này đã bị khóa hoặc hạn chế truy cập. Vui lòng liên hệ Quản trị viên.");
                }

                // ⭐ CẬP NHẬT LastLogin
                user.LastLogin = DateTime.Now;
                await _userManager.UpdateAsync(user);

                // ⭐ TẠO DANH SÁCH CLAIMS
                var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email ?? ""),
            new Claim(ClaimTypes.Name, user.UserName ?? ""),
            new Claim("RoleId", user.RoleId.ToString()), // ⭐ QUAN TRỌNG
            new Claim(ClaimTypes.Role, user.RoleId == 1 ? "Admin" : "User")
        };

                // ⭐ THÊM SUBSCRIPTIONTYPE
                if (!string.IsNullOrEmpty(user.SubscriptionType))
                {
                    claims.Add(new Claim("SubscriptionType", user.SubscriptionType.ToLower()));
                    _logger.LogInformation($"Added SubscriptionType claim: {user.SubscriptionType.ToLower()}");
                }
                else
                {
                    claims.Add(new Claim("SubscriptionType", "free"));
                    _logger.LogInformation("Added default SubscriptionType claim: free");
                }

                // ⭐ LOG ĐỂ DEBUG
                _logger.LogInformation($"Login claims for user {user.Email}: RoleId={user.RoleId}, SubscriptionType={user.SubscriptionType ?? "null"}");

                // ⭐ ĐĂNG NHẬP VỚI CLAIMS
                await _signInManager.SignInWithClaimsAsync(user, model.RememberMe, claims);

                return AuthResult.Success("Đăng nhập thành công", GenerateJwtToken(user), user);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during login");
                return AuthResult.Failed("Có lỗi xảy ra trong quá trình đăng nhập");
            }
        }

        // ==========================================================
        // LOGOUT
        // ==========================================================

        public async Task<bool> LogoutAsync()
        {
            try
            {
                await _signInManager.SignOutAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during logout");
                return false;
            }
        }

        // ==========================================================
        // FORGOT PASSWORD
        // ==========================================================

        public async Task<bool> ForgotPasswordAsync(string email)
        {
            try
            {
                var user = await _userManager.FindByEmailAsync(email);
                if (user == null || !user.EmailConfirmed) return true;

                var token = await _userManager.GeneratePasswordResetTokenAsync(user);

                var baseUrl = _configuration["AppSettings:BaseUrl"] ?? "https://localhost:5001";
                var resetLink = $"{baseUrl}/auth/reset-password?userId={user.Id}&token={Uri.EscapeDataString(token)}";

                await _emailService.SendPasswordResetAsync(user.Email!, user.FullName, resetLink);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during forgot password");
                return false;
            }
        }

        // ==========================================================
        // RESET PASSWORD
        // ==========================================================

        public async Task<AuthResult> ResetPasswordAsync(ResetPasswordDto model)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(model.UserId);
                if (user == null) return AuthResult.Failed("Người dùng không tồn tại");

                if (string.IsNullOrWhiteSpace(model.NewPassword))
                    return AuthResult.Failed("Mật khẩu mới không hợp lệ");

                var result = await _userManager.ResetPasswordAsync(user, model.Token, model.NewPassword);

                return result.Succeeded
                    ? AuthResult.Success("Đặt lại mật khẩu thành công")
                    : AuthResult.Failed(result.Errors.Select(e => e.Description).ToList());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during reset password");
                return AuthResult.Failed("Có lỗi xảy ra khi đặt lại mật khẩu");
            }
        }

        // ==========================================================
        // GET CURRENT USER
        // ==========================================================

        public async Task<User?> GetCurrentUserAsync()
        {
            try
            {
                var userId = _httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
                return string.IsNullOrEmpty(userId) ? null : await _userManager.FindByIdAsync(userId);
            }
            catch
            {
                return null;
            }
        }

        // ==========================================================
        // JWT GENERATION
        // ==========================================================

        public string GenerateJwtToken(User user)
        {
            var keyString = _configuration["JwtSettings:SecretKey"]
                ?? throw new InvalidOperationException("JWT SecretKey missing");

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(keyString));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Email, user.Email!),
                new Claim(ClaimTypes.Name, user.UserName ?? ""),
                new Claim("RoleId", user.RoleId.ToString()),
                new Claim(ClaimTypes.Role, user.RoleId == 1 ? "Admin" : "User"),
                new Claim("IsPremium", user.IsPremium.ToString())
            };

            var token = new JwtSecurityToken(
                issuer: _configuration["JwtSettings:Issuer"],
                audience: _configuration["JwtSettings:Audience"],
                expires: DateTime.UtcNow.AddMinutes(_configuration.GetValue<int>("JwtSettings:ExpiryMinutes", 1440)),
                claims: claims,
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}

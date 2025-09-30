// Services/AuthService.cs
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Net;
using MovieWeb.Models.Entities;
using MovieWeb.Models.DTOs;
using MovieWeb.Data;

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

        public AuthService(
            UserManager<User> userManager,
            SignInManager<User> signInManager,
            MovieWebDbContext context,
            IConfiguration configuration,
            IEmailService emailService,
            ILogger<AuthService> logger)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _context = context;
            _configuration = configuration;
            _emailService = emailService;
            _logger = logger;
        }

        public async Task<AuthResult> RegisterAsync(RegisterDto model)
        {
            try
            {
                if (await _userManager.FindByEmailAsync(model.Email) != null)
                    return AuthResult.Failed("Email đã được sử dụng");

                if (await _userManager.FindByNameAsync(model.Username) != null)
                    return AuthResult.Failed("Tên đăng nhập đã được sử dụng");

                // Tách họ và tên
                string firstName = "";
                string lastName = "";
                if (!string.IsNullOrWhiteSpace(model.FullName))
                {
                    var parts = model.FullName.Trim().Split(' ');
                    firstName = parts.Last(); // lấy từ cuối
                    lastName = string.Join(" ", parts.Take(parts.Length - 1)); // phần còn lại
                }

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

                // Build confirmation link using BaseUrl from config and encode token
                var baseUrl = _configuration["AppSettings:BaseUrl"] ?? "https://localhost:5001";
                var confirmationLink = $"{baseUrl.TrimEnd('/')}/auth/confirm-email?userId={user.Id}&token={Uri.EscapeDataString(token)}";

                await _emailService.SendEmailConfirmationAsync(user.Email!, model.FullName, confirmationLink);

                return AuthResult.Success("Đăng ký thành công. Vui lòng kiểm tra email để xác thực tài khoản.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during registration");
                return AuthResult.Failed("Có lỗi xảy ra trong quá trình đăng ký");
            }
        }

        public async Task<AuthResult> LoginAsync(LoginDto model)
        {
            try
            {
                var user = await _userManager.FindByEmailAsync(model.Email);
                if (user == null)
                    return AuthResult.Failed("Email hoặc mật khẩu không chính xác");

                if (user.IsActive != true)
                    return AuthResult.Failed("Tài khoản đã bị khóa");

                // Nếu chưa xác thực email -> gửi lại mail xác thực (encode token)
                if (!user.EmailConfirmed)
                {
                    var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                    var baseUrl = _configuration["AppSettings:BaseUrl"] ?? "https://localhost:5001";
                    var confirmationLink = $"{baseUrl.TrimEnd('/')}/auth/confirm-email?userId={user.Id}&token={Uri.EscapeDataString(token)}";

                    await _emailService.SendEmailConfirmationAsync(user.Email!, user.FullName, confirmationLink);

                    return AuthResult.Failed("Tài khoản chưa được xác thực. Một email xác thực mới đã được gửi.");
                }

                var result = await _signInManager.PasswordSignInAsync(
                    user.UserName!, model.Password, model.RememberMe, lockoutOnFailure: true);

                if (result.Succeeded)
                {
                    user.LastLogin = DateTime.Now;
                    await _userManager.UpdateAsync(user);

                    var token = GenerateJwtToken(user);
                    return AuthResult.Success("Đăng nhập thành công", token, user);
                }

                if (result.RequiresTwoFactor)
                    return AuthResult.Failed("Cần xác thực 2 bước");

                if (result.IsLockedOut)
                    return AuthResult.Failed("Tài khoản bị khóa tạm thời do nhập sai nhiều lần");

                return AuthResult.Failed("Email hoặc mật khẩu không chính xác");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during login");
                return AuthResult.Failed("Có lỗi xảy ra trong quá trình đăng nhập");
            }
        }

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

        public async Task<AuthResult> ConfirmEmailAsync(string userId, string token)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return AuthResult.Failed("Người dùng không tồn tại");
            }

            var decodedToken = WebUtility.UrlDecode(token);
            var result = await _userManager.ConfirmEmailAsync(user, decodedToken);

            if (result.Succeeded)
            {
                await _signInManager.SignInAsync(user, isPersistent: true);
            }

            return AuthResult.Failed("Token xác thực không hợp lệ hoặc đã hết hạn");
        }

        public async Task<bool> ForgotPasswordAsync(string email)
        {
            try
            {
                var user = await _userManager.FindByEmailAsync(email);
                if (user == null || !user.EmailConfirmed)
                {
                    return true; // tránh lộ thông tin user có tồn tại hay không
                }

                var token = await _userManager.GeneratePasswordResetTokenAsync(user);

                var baseUrl = _configuration["AppSettings:BaseUrl"] ?? "https://localhost:5001";
                var resetLink = $"{baseUrl.TrimEnd('/')}/auth/reset-password?userId={user.Id}&token={Uri.EscapeDataString(token)}";

                await _emailService.SendPasswordResetAsync(user.Email!, user.FullName, resetLink);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during forgot password");
                return false;
            }
        }

        public async Task<AuthResult> ResetPasswordAsync(ResetPasswordDto model)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(model.UserId);
                if (user == null)
                    return AuthResult.Failed("Người dùng không tồn tại");

                // Decode token before calling Identity
                var decodedToken = WebUtility.UrlDecode(model.Token);

                var result = await _userManager.ResetPasswordAsync(user, decodedToken, model.NewPassword);
                if (result.Succeeded)
                    return AuthResult.Success("Đặt lại mật khẩu thành công");

                return AuthResult.Failed(result.Errors.Select(e => e.Description).ToList());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during reset password");
                return AuthResult.Failed("Có lỗi xảy ra trong quá trình đặt lại mật khẩu");
            }
        }

        public async Task<User?> GetCurrentUserAsync()
        {
            try
            {
                var claimsPrincipal = _signInManager.Context.User;
                return await _userManager.GetUserAsync(claimsPrincipal);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting current user");
                return null;
            }
        }

        public string GenerateJwtToken(User user)
        {
            var secretKey = _configuration["JwtSettings:SecretKey"]
                            ?? throw new InvalidOperationException("JWT SecretKey not found");
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.UserName ?? ""),
                new Claim(ClaimTypes.Email, user.Email ?? ""),
                new Claim("FirstName", user.FirstName ?? ""),
                new Claim("LastName", user.LastName ?? ""),
                new Claim("RoleId", user.RoleId.ToString()),
                new Claim(ClaimTypes.Role, user.RoleId == 1 ? "Admin" : "User")
            };

            var token = new JwtSecurityToken(
                issuer: _configuration["JwtSettings:Issuer"] ?? "MovieWeb",
                audience: _configuration["JwtSettings:Audience"] ?? "MovieWebUsers",
                claims: claims,
                expires: DateTime.Now.AddMinutes(
                    _configuration.GetValue<int>("JwtSettings:ExpiryMinutes", 1440)),
                signingCredentials: creds);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}

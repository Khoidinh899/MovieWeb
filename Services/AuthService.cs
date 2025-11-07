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
using Microsoft.EntityFrameworkCore;

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
        // ===== PHẦN QUẢN LÝ PROFILE ĐÃ CẬP NHẬT =====
        // ==========================================================

        public async Task<UserProfileDto?> GetUserProfileAsync(int userId)
        {
            var user = await _context.Users
                .Where(u => u.Id == userId)
                .Select(u => new UserProfileDto
                {
                    UserId = u.Id,
                    Username = u.UserName ?? string.Empty,
                    Email = u.Email ?? string.Empty,
                    FirstName = u.FirstName ?? string.Empty,
                    LastName = u.LastName ?? string.Empty,
                    Avatar = string.IsNullOrEmpty(u.Avatar) ? "/images/nouser.png" : u.Avatar,
                    PhoneNumber = u.PhoneNumber,
                    DateOfBirth = u.DateOfBirth,
                    Gender = u.Gender,
                    Address = u.Address,
                    Bio = u.Bio,
                    CreatedAt = u.CreatedAt ?? DateTime.MinValue,
                    SubscriptionType = u.SubscriptionType,
                    SubscriptionEndDate = u.SubscriptionEndDate,
                    // SubscriptionDisplayName = u.SubscriptionDisplayName,

                    // ✅ SỬA CHÍNH XÁC Ở ĐÂY
                    IsStudentVerified = u.IsStudentVerified,
                    StudentEmail = u.StudentEmail,
                    StudentEmailVerifiedAt = u.StudentEmailVerifiedAt,
                    StudentEmailVerificationExpiry = u.StudentEmailVerificationExpiry
                })
                .FirstOrDefaultAsync();

            return user;
        }
        public async Task<ProfileResult> UpdateUserProfileAsync(int userId, UpdateProfileDto model)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null)
            {
                return ProfileResult.Failed("Không tìm thấy người dùng.");
            }

            user.FirstName = model.FirstName;
            user.LastName = model.LastName;
            user.PhoneNumber = model.PhoneNumber;
            user.DateOfBirth = model.DateOfBirth;
            user.Gender = model.Gender;
            user.Address = model.Address;
            user.Bio = model.Bio;
            user.UpdatedAt = DateTime.Now;

            var result = await _userManager.UpdateAsync(user);

            if (result.Succeeded)
            {
                var updatedProfile = await GetUserProfileAsync(userId);
                return ProfileResult.Success("Cập nhật thông tin thành công!", updatedProfile);
            }

            return ProfileResult.Failed(result.Errors.Select(e => e.Description).ToList());
        }
        public async Task<AuthResult> RegisterAsync(RegisterDto model)
        {
            try
            {
                if (await _userManager.FindByEmailAsync(model.Email) != null)
                    return AuthResult.Failed("Email đã được sử dụng");

                if (await _userManager.FindByNameAsync(model.Username) != null)
                    return AuthResult.Failed("Tên đăng nhập đã được sử dụng");

                string firstName = "";
                string lastName = "";
                if (!string.IsNullOrWhiteSpace(model.FullName))
                {
                    var parts = model.FullName.Trim().Split(' ');
                    firstName = parts.Last();
                    lastName = string.Join(" ", parts.Take(parts.Length - 1));
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
                    await _signInManager.SignOutAsync();
                    var claims = new List<Claim>
                    {
                        new Claim(ClaimTypes.Role, user.RoleId == 1 ? "Admin" : "User"),
                        new Claim("RoleId", user.RoleId.ToString())
                    };
                    if (user.IsPremium) // Dùng thuộc tính IsPremium trong User.cs
                    {
                        claims.Add(new Claim("IsPremium", "true"));
                    }
                    await _signInManager.SignInWithClaimsAsync(user, model.RememberMe, claims);
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
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.Role, user.RoleId == 1 ? "Admin" : "User"),
                    new Claim("RoleId", user.RoleId.ToString())
                };

                if (user.IsPremium) 
                {
                    claims.Add(new Claim("IsPremium", "true"));
                }

                await _signInManager.SignInWithClaimsAsync(user, isPersistent: true, claims);
                
                return AuthResult.Success("Xác thực email thành công!");
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
                    return true;
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

                // ✅ SỬA LẠI: Kiểm tra null cho an toàn
                if (string.IsNullOrEmpty(model.NewPassword))
                {
                    return AuthResult.Failed("Mật khẩu mới không được để trống.");
                }

                _logger.LogInformation("Reset password - UserId: {UserId}", model.UserId);
                _logger.LogInformation("Reset password - Token: {Token}", model.Token);
                _logger.LogInformation("Reset password - New password length: {Length}", model.NewPassword?.Length);

                var result = await _userManager.ResetPasswordAsync(user, model.Token, model.NewPassword);

                if (result.Succeeded)
                {
                    _logger.LogInformation("Password reset SUCCESS for user {Email}", user.Email);
                    return AuthResult.Success("Đặt lại mật khẩu thành công");
                }

                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                _logger.LogWarning("Password reset FAILED for user {Email}. Errors: {Errors}", user.Email, errors);
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
            // ✅ SỬA LẠI: Dùng IHttpContextAccessor để ổn định hơn
            try
            {
                var userId = _httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userId))
                {
                    return null;
                }
                return await _userManager.FindByIdAsync(userId);
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
                new Claim(ClaimTypes.Role, user.RoleId == 1 ? "Admin" : "User"),
                new Claim("IsPremium", user.IsPremium.ToString().ToLower())
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
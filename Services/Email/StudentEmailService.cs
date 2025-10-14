using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MovieWeb.Models.Entities;
using MovieWeb.Models.DTOs;
using MovieWeb.Data;

namespace MovieWeb.Services
{
    public interface IStudentEmailService
    {
        Task<ServiceResult> SendVerificationCodeAsync(int userId, string studentEmail);
        Task<ServiceResult> VerifyStudentEmailAsync(int userId, string otpCode);
        Task<bool> IsStudentEmailValidAsync(string email);
        Task<bool> IsStudentEmailAlreadyUsedAsync(string email, int? excludeUserId = null);
    }

    public class StudentEmailService : IStudentEmailService
    {
        private readonly UserManager<User> _userManager;
        private readonly IEmailService _emailService;
        private readonly IConfiguration _configuration;
        private readonly ILogger<StudentEmailService> _logger;
        private readonly MovieWebDbContext _context;

        public StudentEmailService(
            UserManager<User> userManager,
            IEmailService emailService,
            IConfiguration configuration,
            ILogger<StudentEmailService> logger,
            MovieWebDbContext context)
        {
            _userManager = userManager;
            _emailService = emailService;
            _configuration = configuration;
            _logger = logger;
            _context = context;
        }

        public async Task<ServiceResult> SendVerificationCodeAsync(int userId, string studentEmail)
        {
            try
            {
                // 1. Validate email format
                if (!IsValidEmail(studentEmail))
                {
                    return ServiceResult.Failed("Email không hợp lệ");
                }

                // 2. Check if it's a student email
                if (!await IsStudentEmailValidAsync(studentEmail))
                {
                    return ServiceResult.Failed("Email phải là email sinh viên (.edu, .edu.vn, .ac.vn)");
                }

                // 3. Check if email already used by another user
                if (await IsStudentEmailAlreadyUsedAsync(studentEmail, userId))
                {
                    return ServiceResult.Failed("Email sinh viên này đã được sử dụng bởi tài khoản khác");
                }

                // 4. Get user
                var user = await _userManager.FindByIdAsync(userId.ToString());
                if (user == null)
                {
                    return ServiceResult.Failed("Không tìm thấy người dùng");
                }

                // 5. Generate OTP (6 digits)
                var otpCode = GenerateOTP();

                // 6. Save OTP and expiry time to database
                user.StudentEmail = studentEmail;
                user.StudentEmailVerificationCode = otpCode;
                user.StudentEmailVerificationExpiry = DateTime.Now.AddMinutes(5); // Hết hạn sau 5 phút
                user.IsStudentVerified = false; // Reset verification status
                user.UpdatedAt = DateTime.Now;

                var updateResult = await _userManager.UpdateAsync(user);
                if (!updateResult.Succeeded)
                {
                    return ServiceResult.Failed("Không thể lưu mã xác thực");
                }

                // 7. Send OTP via email
                await _emailService.SendStudentEmailOtpAsync(studentEmail, user.FullName, otpCode);

                _logger.LogInformation($"OTP sent to {studentEmail} for user {userId}");

                return ServiceResult.Success("Mã xác thực đã được gửi đến email sinh viên của bạn. Vui lòng kiểm tra hộp thư.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending student email verification code");
                return ServiceResult.Failed("Có lỗi xảy ra khi gửi mã xác thực");
            }
        }

        public async Task<ServiceResult> VerifyStudentEmailAsync(int userId, string otpCode)
        {
            try
            {
                // 1. Get user
                var user = await _userManager.FindByIdAsync(userId.ToString());
                if (user == null)
                {
                    return ServiceResult.Failed("Không tìm thấy người dùng");
                }

                // 2. Check if user has OTP
                if (string.IsNullOrEmpty(user.StudentEmailVerificationCode))
                {
                    return ServiceResult.Failed("Vui lòng yêu cầu gửi mã xác thực trước");
                }

                // 3. Check if OTP expired
                if (!user.StudentEmailVerificationExpiry.HasValue || 
                    DateTime.Now > user.StudentEmailVerificationExpiry.Value)
                {
                    return ServiceResult.Failed("Mã xác thực đã hết hạn. Vui lòng yêu cầu mã mới.");
                }

                // 4. Verify OTP
                if (user.StudentEmailVerificationCode != otpCode.Trim())
                {
                    return ServiceResult.Failed("Mã xác thực không đúng");
                }

                // 5. Mark as verified
                user.IsStudentVerified = true;
                user.StudentEmailVerifiedAt = DateTime.Now;
                user.StudentEmailVerificationCode = null; // Clear OTP
                user.StudentEmailVerificationExpiry = null;
                user.UpdatedAt = DateTime.Now;

                var updateResult = await _userManager.UpdateAsync(user);
                if (!updateResult.Succeeded)
                {
                    return ServiceResult.Failed("Không thể xác thực email");
                }

                _logger.LogInformation($"Student email verified successfully for user {userId}");

                return ServiceResult.Success("Xác thực email sinh viên thành công! Bạn có thể mua gói Student ngay bây giờ.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying student email");
                return ServiceResult.Failed("Có lỗi xảy ra khi xác thực email");
            }
        }

        public async Task<bool> IsStudentEmailValidAsync(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return false;

            var allowedDomains = _configuration.GetSection("SubscriptionSettings:StudentEmailDomains")
                .Get<string[]>() ?? new[] { ".edu", ".edu.vn", ".ac.vn" };

            return allowedDomains.Any(domain => 
                email.EndsWith(domain, StringComparison.OrdinalIgnoreCase));
        }

        public async Task<bool> IsStudentEmailAlreadyUsedAsync(string email, int? excludeUserId = null)
        {
            var query = _context.Users.Where(u => u.StudentEmail == email);

            if (excludeUserId.HasValue)
            {
                query = query.Where(u => u.Id != excludeUserId.Value);
            }

            return await query.AnyAsync();
        }

        private string GenerateOTP()
        {
            var random = new Random();
            return random.Next(100000, 999999).ToString(); // 6 digits
        }

        private bool IsValidEmail(string email)
        {
            try
            {
                var addr = new System.Net.Mail.MailAddress(email);
                return addr.Address == email;
            }
            catch
            {
                return false;
            }
        }
    }

    // ===== SERVICE RESULT DTO =====
    public class ServiceResult
    {
        public bool IsSuccess { get; set; }
        public string Message { get; set; } = string.Empty;
        public List<string> Errors { get; set; } = new();

        public static ServiceResult Success(string message = "")
        {
            return new ServiceResult
            {
                IsSuccess = true,
                Message = message
            };
        }

        public static ServiceResult Failed(string error)
        {
            return new ServiceResult
            {
                IsSuccess = false,
                Errors = new List<string> { error }
            };
        }

        public static ServiceResult Failed(List<string> errors)
        {
            return new ServiceResult
            {
                IsSuccess = false,
                Errors = errors
            };
        }
    }
}
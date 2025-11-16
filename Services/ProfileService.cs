using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MovieWeb.Models.Entities;
using MovieWeb.Models.DTOs; // Giả sử DTOs của bạn ở đây
using MovieWeb.Models; // Giả sử PaymentHistoryViewModel ở đây
using MovieWeb.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System;
using MovieWeb.Services.Interfaces;

namespace MovieWeb.Services
{
    public interface IProfileService
    {
        Task<UserProfileDto?> GetUserProfileAsync(int userId);
        Task<ProfileResult> UpdateProfileAsync(int userId, UpdateProfileDto model);
        Task<ProfileResult> ChangePasswordAsync(int userId, ChangePasswordDto model);
        Task<ProfileResult> UpdateAvatarAsync(int userId, IFormFile avatarFile);
        Task<ProfileResult> DeleteAvatarAsync(int userId);

        // ===== THÊM HÀM MỚI NÀY =====
        Task<PaymentHistoryDto> GetPaymentHistoryAsync(int userId, int page, int pageSize = 10);
        // =============================

        // Admin methods
        Task<List<UserProfileDto>> GetAllUsersAsync();
        Task<ProfileResult> AdminUpdateUserAsync(AdminUpdateUserDto model);
        Task<ProfileResult> AdminChangePasswordAsync(AdminChangePasswordDto model);
        Task<ProfileResult> AdminToggleUserStatusAsync(int userId);
        Task<ProfileResult> AdminDeleteUserAsync(int userId);
    }

    public class ProfileService : IProfileService
    {
        private readonly UserManager<User> _userManager;
        private readonly MovieWebDbContext _context;
        private readonly ILogger<ProfileService> _logger;
        private readonly IWebHostEnvironment _environment;
        private readonly IAuthService _authService;
        private readonly IEmailService _emailService;
        private readonly IConfiguration _configuration;
        private readonly IHttpContextAccessor _httpContextAccessor;

        private const string _userUploadPath = "/images/uploads/avatars/";
        private const string _defaultAvatarPath = "/images/nouser.png";


        public ProfileService(
            UserManager<User> userManager,
            MovieWebDbContext context,
            ILogger<ProfileService> logger,
            IWebHostEnvironment environment,
            IAuthService authService,
            IEmailService emailService,
            IConfiguration configuration,
            IHttpContextAccessor httpContextAccessor)
        {
            _userManager = userManager;
            _context = context;
            _logger = logger;
            _environment = environment;
            _authService = authService;
            _emailService = emailService;
            _configuration = configuration;
            _httpContextAccessor = httpContextAccessor;
        }

        // Lấy thông tin profile đầy đủ
        public async Task<UserProfileDto?> GetUserProfileAsync(int userId)
        {
            try
            {
                var userProfile = await _context.Users
                    .Where(u => u.Id == userId)
                    .Select(user => new UserProfileDto
                    {
                        UserId = user.Id,
                        Username = user.UserName ?? "",
                        Email = user.Email ?? "",
                        FirstName = user.FirstName ?? "",
                        LastName = user.LastName ?? "",
                        Avatar = string.IsNullOrEmpty(user.Avatar) ? _defaultAvatarPath : user.Avatar,
                        IsActive = user.IsActive ?? false,
                        EmailConfirmed = user.EmailConfirmed,
                        CreatedAt = user.CreatedAt ?? DateTime.Now,
                        LastLogin = user.LastLogin,
                        RoleId = user.RoleId,
                        PhoneNumber = user.PhoneNumber,
                        DateOfBirth = user.DateOfBirth,
                        Gender = user.Gender,
                        Address = user.Address,
                        Bio = user.Bio,

                        TotalFavorites = (user.Favorites == null) ? 0 : user.Favorites.Count,
                        TotalComments = (user.Comments == null) ? 0 : user.Comments.Count,
                        TotalRatings = (user.Ratings == null) ? 0 : user.Ratings.Count,
                        TotalWatchHistory = (user.WatchHistories == null) ? 0 : user.WatchHistories.Count,

                        SubscriptionType = user.SubscriptionType ?? "free",
                        SubscriptionStartDate = user.SubscriptionStartDate,
                        SubscriptionEndDate = user.SubscriptionEndDate,

                        IsStudentVerified = user.IsStudentVerified,
                        StudentEmail = user.StudentEmail,
                        StudentEmailVerifiedAt = user.StudentEmailVerifiedAt,
                        StudentEmailVerificationExpiry = user.StudentEmailVerificationExpiry
                    })
                    .FirstOrDefaultAsync();

                if (userProfile == null) return null;

                var activeSubscription = await _context.UserSubscriptions
                    .Where(s => s.UserId == userId
                        && s.EndDate > DateTime.Now
                        && (s.Status == "active" || s.Status == "cancelled"))
                    .OrderByDescending(s => s.EndDate)
                    .FirstOrDefaultAsync();

                if (activeSubscription != null)
                {
                    userProfile.RemainingDaysFromPreviousPackage = activeSubscription.BonusDaysFromPreviousPackage;
                    userProfile.IsCancelled = activeSubscription.Status == "cancelled"
                                              && activeSubscription.EndDate > DateTime.Now;
                }

                return userProfile;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user profile for userId: {UserId}", userId);
                return null;
            }
        }

        // Cập nhật thông tin cá nhân
        public async Task<ProfileResult> UpdateProfileAsync(int userId, UpdateProfileDto model)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(userId.ToString());
                if (user == null)
                    return ProfileResult.Failed("Người dùng không tồn tại");

                user.FirstName = model.FirstName;
                user.LastName = model.LastName;

                if (model.Email != user.Email)
                {
                    var existingUser = await _userManager.FindByEmailAsync(model.Email);
                    if (existingUser != null && existingUser.Id != userId)
                        return ProfileResult.Failed("Email đã được sử dụng bởi tài khoản khác");

                    user.Email = model.Email;
                    user.EmailConfirmed = false;
                    try
                    {
                        // Tạo token mới
                        var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);

                        // Lấy đúng Base URL thực tế từ request (trùng logic AuthController)
                        var request = _httpContextAccessor.HttpContext?.Request;
                        var actualBaseUrl = $"{request?.Scheme}://{request?.Host}".TrimEnd('/');

                        var confirmationLink = $"{actualBaseUrl}/auth/confirm-email?userId={user.Id}&token={Uri.EscapeDataString(token)}";

                        // Gửi email xác thực mới
                        await _emailService.SendEmailConfirmationAsync(
                            user.Email!,
                            user.FullName ?? user.Email!,
                            confirmationLink
                        );

                        _logger.LogInformation(
                            "Sent NEW confirmation email to {Email} after profile update.",
                            user.Email
                        );
                    }
                    catch (Exception exEmail)
                    {
                        _logger.LogError(
                            exEmail,
                            "Failed to send NEW confirmation email to {Email}",
                            user.Email
                        );
                        // Không chặn flow, chỉ log lỗi
                    }

                }
                user.PhoneNumber = model.PhoneNumber;
                user.DateOfBirth = model.DateOfBirth;
                user.Gender = model.Gender;
                user.Address = model.Address;
                user.Bio = model.Bio;

                user.UpdatedAt = DateTime.Now;

                var result = await _userManager.UpdateAsync(user);
                if (!result.Succeeded)
                    return ProfileResult.Failed(result.Errors.Select(e => e.Description).ToList());

                var profileDto = await GetUserProfileAsync(userId);
                return ProfileResult.Success("Cập nhật thông tin thành công", profileDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating profile for userId: {UserId}", userId);
                return ProfileResult.Failed("Có lỗi xảy ra khi cập nhật thông tin");
            }
        }

        // Thay đổi mật khẩu
        public async Task<ProfileResult> ChangePasswordAsync(int userId, ChangePasswordDto model)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(userId.ToString());
                if (user == null)
                    return ProfileResult.Failed("Người dùng không tồn tại");

                var result = await _userManager.ChangePasswordAsync(
                    user, model.CurrentPassword, model.NewPassword);

                if (!result.Succeeded)
                    return ProfileResult.Failed(result.Errors.Select(e => e.Description).ToList());

                return ProfileResult.Success("Đổi mật khẩu thành công");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error changing password for userId: {UserId}", userId);
                return ProfileResult.Failed("Có lỗi xảy ra khi đổi mật khẩu");
            }
        }

        // Upload avatar
        public async Task<ProfileResult> UpdateAvatarAsync(int userId, IFormFile avatarFile)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(userId.ToString());
                if (user == null)
                    return ProfileResult.Failed("Người dùng không tồn tại");

                if (!user.IsPremium)
                {
                    return ProfileResult.Failed("Chỉ thành viên MoonPro/MoonStu mới được tải avatar.");
                }

                if (avatarFile == null || avatarFile.Length == 0)
                {
                    return ProfileResult.Failed("Không có file nào được chọn.");
                }

                long maxFileSize = 5 * 1024 * 1024; // 5MB
                if (avatarFile.Length > maxFileSize)
                {
                    return ProfileResult.Failed("File quá lớn. Vui lòng chọn file dưới 5MB.");
                }

                var allowedTypes = new[] { "image/jpeg", "image/png", "image/gif", "image/webp" };
                if (!allowedTypes.Contains(avatarFile.ContentType.ToLower()))
                {
                    return ProfileResult.Failed("Định dạng file không hợp lệ. Chỉ chấp nhận .jpg, .png, .gif, .webp.");
                }

                if (!string.IsNullOrEmpty(user.Avatar) && user.Avatar.StartsWith(_userUploadPath))
                {
                    var oldAvatarPath = Path.Combine(_environment.WebRootPath, user.Avatar.TrimStart('/'));
                    if (File.Exists(oldAvatarPath))
                    {
                        File.Delete(oldAvatarPath);
                    }
                }

                var uploadsFolder = Path.Combine(_environment.WebRootPath, _userUploadPath.TrimStart('/'));
                if (!Directory.Exists(uploadsFolder))
                    Directory.CreateDirectory(uploadsFolder);

                var extension = Path.GetExtension(avatarFile.FileName).ToLowerInvariant();
                var uniqueFileName = $"user_{userId}_{Guid.NewGuid()}{extension}";
                var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await avatarFile.CopyToAsync(fileStream);
                }

                user.Avatar = $"{_userUploadPath}{uniqueFileName}";
                user.UpdatedAt = DateTime.Now;
                await _userManager.UpdateAsync(user);

                var profileDto = await GetUserProfileAsync(userId);
                return ProfileResult.Success("Cập nhật avatar thành công", profileDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating avatar for userId: {UserId}", userId);
                return ProfileResult.Failed("Có lỗi xảy ra khi cập nhật avatar");
            }
        }

        // Xóa avatar
        public async Task<ProfileResult> DeleteAvatarAsync(int userId)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(userId.ToString());
                if (user == null)
                    return ProfileResult.Failed("Người dùng không tồn tại");

                if (!string.IsNullOrEmpty(user.Avatar) && user.Avatar.StartsWith(_userUploadPath))
                {
                    var avatarPath = Path.Combine(_environment.WebRootPath, user.Avatar.TrimStart('/'));
                    if (File.Exists(avatarPath))
                    {
                        File.Delete(avatarPath);
                    }
                }

                user.Avatar = _defaultAvatarPath;
                user.UpdatedAt = DateTime.Now;
                await _userManager.UpdateAsync(user);

                var profileDto = await GetUserProfileAsync(userId);
                return ProfileResult.Success("Xóa avatar thành công", profileDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting avatar for userId: {UserId}", userId);
                return ProfileResult.Failed("Có lỗi xảy ra khi xóa avatar");
            }
        }

        // ===== THÊM HÀM MỚI NÀY VÀO =====
        public async Task<PaymentHistoryDto> GetPaymentHistoryAsync(int userId, int page, int pageSize = 10)
        {
            try
            {
                var totalTransactions = await _context.Transactions
                    .Where(t => t.UserId == userId)
                    .CountAsync();

                var transactions = await _context.Transactions
                    .Where(t => t.UserId == userId)
                    .Include(t => t.SubscriptionPlan)
                    .Include(t => t.UserSubscription)
                    .OrderByDescending(t => t.CreatedAt)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(t => new PaymentHistoryViewModel // Giả sử VM này đã được định nghĩa
                    {
                        TransactionId = t.TransactionId,
                        SubscriptionId = t.SubscriptionId,
                        TransactionCode = t.TransactionCode,
                        PlanName = t.SubscriptionPlan != null ? t.SubscriptionPlan.DisplayName : "Không xác định",
                        AmountVND = t.AmountVND,
                        Currency = t.Currency,
                        Status = t.Status,
                        PaymentMethod = t.PaymentMethod,
                        CreatedAt = t.CreatedAt,
                        StatusDisplay = t.StatusDisplay,
                        SubscriptionStatus = t.UserSubscription != null ? t.UserSubscription.Status : null,
                        SubscriptionEndDate = t.UserSubscription != null ? t.UserSubscription.EndDate : null
                    })
                    .ToListAsync();

                var totalPages = (int)Math.Ceiling(totalTransactions / (double)pageSize);

                return new PaymentHistoryDto
                {
                    Transactions = transactions,
                    CurrentPage = page,
                    TotalPages = totalPages,
                    TotalTransactions = totalTransactions,
                    PageSize = pageSize
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting payment history for userId: {UserId}", userId);
                // Trả về DTO rỗng khi có lỗi
                return new PaymentHistoryDto { CurrentPage = page, PageSize = pageSize };
            }
        }

        // Lấy danh sách tất cả users
        public async Task<List<UserProfileDto>> GetAllUsersAsync()
        {
            try
            {
                var users = await _context.Users
                    .OrderByDescending(u => u.CreatedAt)
                    .Select(u => new UserProfileDto
                    {
                        UserId = u.Id,
                        Username = u.UserName ?? "",
                        Email = u.Email ?? "",
                        FirstName = u.FirstName ?? "",
                        LastName = u.LastName ?? "",
                        Avatar = string.IsNullOrEmpty(u.Avatar) ? _defaultAvatarPath : u.Avatar,
                        IsActive = u.IsActive ?? false,
                        EmailConfirmed = u.EmailConfirmed,
                        CreatedAt = u.CreatedAt ?? DateTime.Now,
                        LastLogin = u.LastLogin,
                        RoleId = u.RoleId,
                        TotalFavorites = (u.Favorites == null) ? 0 : u.Favorites.Count,
                        TotalComments = (u.Comments == null) ? 0 : u.Comments.Count,
                        TotalRatings = (u.Ratings == null) ? 0 : u.Ratings.Count,
                        TotalWatchHistory = (u.WatchHistories == null) ? 0 : u.WatchHistories.Count,
                        SubscriptionType = u.SubscriptionType ?? "free",
                        SubscriptionStartDate = u.SubscriptionStartDate,
                        SubscriptionEndDate = u.SubscriptionEndDate,
                        IsStudentVerified = u.IsStudentVerified,
                        StudentEmail = u.StudentEmail,
                        StudentEmailVerifiedAt = u.StudentEmailVerifiedAt,
                        StudentEmailVerificationExpiry = u.StudentEmailVerificationExpiry
                    })
                    .ToListAsync();

                return users;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all users");
                return new List<UserProfileDto>();
            }
        }

        // Admin cập nhật user
        public async Task<ProfileResult> AdminUpdateUserAsync(AdminUpdateUserDto model)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(model.UserId.ToString());
                if (user == null)
                    return ProfileResult.Failed("Người dùng không tồn tại");

                user.FirstName = model.FirstName;
                user.LastName = model.LastName;
                user.RoleId = model.RoleId;
                user.IsActive = model.IsActive;

                if (model.Email != user.Email)
                {
                    var existingUser = await _userManager.FindByEmailAsync(model.Email);
                    if (existingUser != null && existingUser.Id != model.UserId)
                        return ProfileResult.Failed("Email đã được sử dụng");

                    user.Email = model.Email;
                }
                user.PhoneNumber = model.PhoneNumber;
                user.DateOfBirth = model.DateOfBirth;
                user.Gender = model.Gender;
                user.Address = model.Address;
                user.UpdatedAt = DateTime.Now;

                var result = await _userManager.UpdateAsync(user);
                if (!result.Succeeded)
                    return ProfileResult.Failed(result.Errors.Select(e => e.Description).ToList());

                var profileDto = await GetUserProfileAsync(model.UserId);
                return ProfileResult.Success("Cập nhật người dùng thành công", profileDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error admin updating user: {UserId}", model.UserId);
                return ProfileResult.Failed("Có lỗi xảy ra khi cập nhật người dùng");
            }
        }

        // Admin đổi mật khẩu user
        public async Task<ProfileResult> AdminChangePasswordAsync(AdminChangePasswordDto model)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(model.UserId.ToString());
                if (user == null)
                    return ProfileResult.Failed("Người dùng không tồn tại");

                var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                var resetResult = await _userManager.ResetPasswordAsync(user, token, model.NewPassword);

                if (resetResult.Succeeded)
                {
                    return ProfileResult.Success("Đổi mật khẩu thành công");
                }

                var removeResult = await _userManager.RemovePasswordAsync(user);
                var addResult = await _userManager.AddPasswordAsync(user, model.NewPassword);

                if (addResult.Succeeded)
                {
                    return ProfileResult.Success("Đổi mật khẩu thành công");
                }

                return ProfileResult.Failed(resetResult.Errors.Any() ?
                    resetResult.Errors.Select(e => e.Description).ToList() :
                    addResult.Errors.Select(e => e.Description).ToList());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error admin changing password for userId: {UserId}", model.UserId);
                return ProfileResult.Failed("Có lỗi xảy ra khi đổi mật khẩu");
            }
        }

        // Admin kích hoạt/vô hiệu hóa user
        public async Task<ProfileResult> AdminToggleUserStatusAsync(int userId)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(userId.ToString());
                if (user == null)
                    return ProfileResult.Failed("Người dùng không tồn tại");

                user.IsActive = !(user.IsActive ?? false);
                user.UpdatedAt = DateTime.Now;

                var result = await _userManager.UpdateAsync(user);
                if (!result.Succeeded)
                    return ProfileResult.Failed("Có lỗi xảy ra");

                var status = user.IsActive == true ? "kích hoạt" : "vô hiệu hóa";
                return ProfileResult.Success($"Đã {status} tài khoản thành công");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling user status for userId: {UserId}", userId);
                return ProfileResult.Failed("Có lỗi xảy ra");
            }
        }

        // Admin xóa user
        public async Task<ProfileResult> AdminDeleteUserAsync(int userId)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(userId.ToString());
                if (user == null)
                    return ProfileResult.Failed("Người dùng không tồn tại");

                if (!string.IsNullOrEmpty(user.Avatar) && user.Avatar.StartsWith(_userUploadPath))
                {
                    var avatarPath = Path.Combine(_environment.WebRootPath, user.Avatar.TrimStart('/'));
                    if (File.Exists(avatarPath))
                    {
                        File.Delete(avatarPath);
                    }
                }

                var result = await _userManager.DeleteAsync(user);
                if (!result.Succeeded)
                    return ProfileResult.Failed(result.Errors.Select(e => e.Description).ToList());

                return ProfileResult.Success("Xóa người dùng thành công");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting user: {UserId}", userId);
                return ProfileResult.Failed("Có lỗi xảy ra khi xóa người dùng");
            }
        }
    }

    // Class helper để trả về kết quả
    public class ProfileResult
    {
        public bool IsSuccess { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
        public string Message { get; set; } = string.Empty;
        public UserProfileDto? Profile { get; set; }

        public static ProfileResult Success(string message, UserProfileDto? profile = null)
        {
            return new ProfileResult { IsSuccess = true, Message = message, Profile = profile };
        }

        public static ProfileResult Failed(string error)
        {
            return new ProfileResult { IsSuccess = false, Errors = new List<string> { error }, Message = error };
        }

        public static ProfileResult Failed(List<string> errors)
        {
            return new ProfileResult { IsSuccess = false, Errors = errors, Message = errors.FirstOrDefault() ?? "Lỗi" };
        }
    }
}
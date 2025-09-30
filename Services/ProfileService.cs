using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MovieWeb.Models.Entities;
using MovieWeb.Models.DTOs;
using MovieWeb.Data;

namespace MovieWeb.Services
{
    public interface IProfileService
    {
        Task<UserProfileDto?> GetUserProfileAsync(int userId);
        Task<ProfileResult> UpdateProfileAsync(int userId, UpdateProfileDto model);
        Task<ProfileResult> ChangePasswordAsync(int userId, ChangePasswordDto model);
        Task<ProfileResult> UpdateAvatarAsync(int userId, IFormFile avatarFile);
        Task<ProfileResult> DeleteAvatarAsync(int userId);
        
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

        public ProfileService(
            UserManager<User> userManager,
            MovieWebDbContext context,
            ILogger<ProfileService> logger,
            IWebHostEnvironment environment)
        {
            _userManager = userManager;
            _context = context;
            _logger = logger;
            _environment = environment;
        }

        // Lấy thông tin profile đầy đủ
        public async Task<UserProfileDto?> GetUserProfileAsync(int userId)
        {
            try
            {
                var user = await _context.Users
                    .Include(u => u.Favorites)
                    .Include(u => u.Comments)
                    .Include(u => u.Ratings)
                    .Include(u => u.WatchHistories)
                    .FirstOrDefaultAsync(u => u.Id == userId);

                if (user == null) return null;

                return new UserProfileDto
                {
                    UserId = user.Id,
                    Username = user.UserName ?? "",
                    Email = user.Email ?? "",
                    FirstName = user.FirstName ?? "",
                    LastName = user.LastName ?? "",
                    Avatar = user.Avatar,
                    IsActive = user.IsActive ?? false,
                    EmailConfirmed = user.EmailConfirmed,
                    CreatedAt = user.CreatedAt ?? DateTime.Now,
                    LastLogin = user.LastLogin,
                    RoleId = user.RoleId,
                    TotalFavorites = user.Favorites.Count,
                    TotalComments = user.Comments.Count,
                    TotalRatings = user.Ratings.Count,
                    TotalWatchHistory = user.WatchHistories.Count
                };
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

                // Cập nhật thông tin
                user.FirstName = model.FirstName;
                user.LastName = model.LastName;

                // Kiểm tra email mới có trùng không
                if (model.Email != user.Email)
                {
                    var existingUser = await _userManager.FindByEmailAsync(model.Email);
                    if (existingUser != null && existingUser.Id != userId)
                        return ProfileResult.Failed("Email đã được sử dụng bởi tài khoản khác");

                    user.Email = model.Email;
                    user.EmailConfirmed = false; // Yêu cầu xác thực lại email mới
                }

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

                // Validate file
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };
                var extension = Path.GetExtension(avatarFile.FileName).ToLowerInvariant();
                
                if (!allowedExtensions.Contains(extension))
                    return ProfileResult.Failed("Chỉ chấp nhận file ảnh (.jpg, .jpeg, .png, .gif)");

                if (avatarFile.Length > 5 * 1024 * 1024) // 5MB
                    return ProfileResult.Failed("Kích thước file không được vượt quá 5MB");

                // Xóa avatar cũ nếu có
                if (!string.IsNullOrEmpty(user.Avatar))
                {
                    var oldAvatarPath = Path.Combine(_environment.WebRootPath, user.Avatar.TrimStart('/'));
                    if (File.Exists(oldAvatarPath))
                        File.Delete(oldAvatarPath);
                }

                // Lưu avatar mới
                var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "avatars");
                if (!Directory.Exists(uploadsFolder))
                    Directory.CreateDirectory(uploadsFolder);

                var uniqueFileName = $"{userId}_{Guid.NewGuid()}{extension}";
                var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await avatarFile.CopyToAsync(fileStream);
                }

                // Cập nhật database
                user.Avatar = $"/uploads/avatars/{uniqueFileName}";
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

                if (!string.IsNullOrEmpty(user.Avatar))
                {
                    var avatarPath = Path.Combine(_environment.WebRootPath, user.Avatar.TrimStart('/'));
                    if (File.Exists(avatarPath))
                        File.Delete(avatarPath);

                    user.Avatar = null;
                    user.UpdatedAt = DateTime.Now;
                    await _userManager.UpdateAsync(user);
                }

                return ProfileResult.Success("Xóa avatar thành công");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting avatar for userId: {UserId}", userId);
                return ProfileResult.Failed("Có lỗi xảy ra khi xóa avatar");
            }
        }

        // ==================== ADMIN METHODS ====================

        // Lấy danh sách tất cả users
        public async Task<List<UserProfileDto>> GetAllUsersAsync()
        {
            try
            {
                var users = await _context.Users
                    .Include(u => u.Favorites)
                    .Include(u => u.Comments)
                    .Include(u => u.Ratings)
                    .Include(u => u.WatchHistories)
                    .OrderByDescending(u => u.CreatedAt)
                    .ToListAsync();

                return users.Select(u => new UserProfileDto
                {
                    UserId = u.Id,
                    Username = u.UserName ?? "",
                    Email = u.Email ?? "",
                    FirstName = u.FirstName ?? "",
                    LastName = u.LastName ?? "",
                    Avatar = u.Avatar,
                    IsActive = u.IsActive ?? false,
                    EmailConfirmed = u.EmailConfirmed,
                    CreatedAt = u.CreatedAt ?? DateTime.Now,
                    LastLogin = u.LastLogin,
                    RoleId = u.RoleId,
                    TotalFavorites = u.Favorites.Count,
                    TotalComments = u.Comments.Count,
                    TotalRatings = u.Ratings.Count,
                    TotalWatchHistory = u.WatchHistories.Count
                }).ToList();
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

                // Cập nhật thông tin
                user.FirstName = model.FirstName;
                user.LastName = model.LastName;
                user.RoleId = model.RoleId;
                user.IsActive = model.IsActive;

                // Kiểm tra email mới
                if (model.Email != user.Email)
                {
                    var existingUser = await _userManager.FindByEmailAsync(model.Email);
                    if (existingUser != null && existingUser.Id != model.UserId)
                        return ProfileResult.Failed("Email đã được sử dụng");

                    user.Email = model.Email;
                }

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

                // Xóa mật khẩu cũ và đặt mật khẩu mới
                var removeResult = await _userManager.RemovePasswordAsync(user);
                if (!removeResult.Succeeded)
                    return ProfileResult.Failed("Không thể xóa mật khẩu cũ");

                var addResult = await _userManager.AddPasswordAsync(user, model.NewPassword);
                if (!addResult.Succeeded)
                    return ProfileResult.Failed(addResult.Errors.Select(e => e.Description).ToList());

                return ProfileResult.Success("Đổi mật khẩu thành công");
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

                // Không cho phép vô hiệu hóa chính mình
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

                // Xóa avatar nếu có
                if (!string.IsNullOrEmpty(user.Avatar))
                {
                    var avatarPath = Path.Combine(_environment.WebRootPath, user.Avatar.TrimStart('/'));
                    if (File.Exists(avatarPath))
                        File.Delete(avatarPath);
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
}
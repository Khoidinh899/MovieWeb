using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MovieWeb.Models.DTOs;
using MovieWeb.Models.Entities;
using MovieWeb.Services;
using MovieWeb.Services.Interfaces;
using System.Security.Claims;

namespace MovieWeb.Controllers.API
{
    [ApiController]
    [Route("api/profile")]
    [Authorize(AuthenticationSchemes = "JwtScheme")]
    public class ApiProfileController : ControllerBase
    {
        private readonly IProfileService _profileService;
        private readonly UserManager<User> _userManager;
        private readonly ILogger<ApiProfileController> _logger;
        private readonly IWebHostEnvironment _environment;

        public ApiProfileController(
            IProfileService profileService,
            UserManager<User> userManager,
            ILogger<ApiProfileController> logger,
            IWebHostEnvironment environment)
        {
            _profileService = profileService;
            _userManager = userManager;
            _logger = logger;
            _environment = environment;
        }

        // GET: /api/profile
        [HttpGet]
        public async Task<IActionResult> GetProfile()
        {
            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized(new { success = false, message = "Vui lòng đăng nhập" });

            var profile = await _profileService.GetUserProfileAsync(userId.Value);
            if (profile == null) return NotFound(new { success = false, message = "Không tìm thấy người dùng" });

            return Ok(new { success = true, data = profile });
        }

        // POST: /api/profile/update
        [HttpPost("update")]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileDto model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Dữ liệu không hợp lệ",
                    errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList()
                });
            }

            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized(new { success = false, message = "Vui lòng đăng nhập" });

            var result = await _profileService.UpdateProfileAsync(userId.Value, model);
            if (result.IsSuccess)
            {
                return Ok(new { success = true, message = result.Message, data = result.Profile });
            }

            return BadRequest(new { success = false, message = result.Message, errors = result.Errors });
        }

        // POST: /api/profile/change-password
        [HttpPost("change-password")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Dữ liệu không hợp lệ",
                    errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList()
                });
            }

            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized(new { success = false, message = "Vui lòng đăng nhập" });

            var result = await _profileService.ChangePasswordAsync(userId.Value, model);
            if (result.IsSuccess)
            {
                return Ok(new { success = true, message = result.Message });
            }

            return BadRequest(new { success = false, message = result.Message, errors = result.Errors });
        }

        // POST: /api/profile/select-avatar
        [HttpPost("select-avatar")]
        public async Task<IActionResult> SelectAvatar([FromBody] SelectAvatarDto model)
        {
            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized(new { success = false, message = "Vui lòng đăng nhập" });

            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null) return NotFound(new { success = false, message = "Không tìm thấy người dùng" });

            // Danh sách avatar mặc định hợp lệ: default0.png đến default12.png hoặc các ảnh nằm trong preset
            var validPresets = Enumerable.Range(0, 13).Select(i => $"default{i}.png").ToList();
            
            // Cho phép nouser.png là mặc định khi chưa chọn gì
            validPresets.Add("nouser.png");

            string avatarName = Path.GetFileName(model.AvatarName);
            if (!validPresets.Contains(avatarName))
            {
                return BadRequest(new { success = false, message = "Ảnh đại diện mặc định không hợp lệ" });
            }

            user.Avatar = $"/images/{avatarName}";
            user.UpdatedAt = DateTime.Now;
            var result = await _userManager.UpdateAsync(user);

            if (result.Succeeded)
            {
                var profile = await _profileService.GetUserProfileAsync(userId.Value);
                return Ok(new { success = true, message = "Cập nhật ảnh đại diện thành công", data = profile });
            }

            return BadRequest(new { success = false, message = "Không thể cập nhật ảnh đại diện" });
        }

        // POST: /api/profile/upload-avatar
        [HttpPost("upload-avatar")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadAvatar([FromForm] IFormFile avatarFile)
        {
            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized(new { success = false, message = "Vui lòng đăng nhập" });

            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null) return NotFound(new { success = false, message = "Không tìm thấy người dùng" });

            // Kiểm tra Premium
            if (!user.IsPremium)
            {
                return StatusCode(403, new { success = false, message = "Chức năng tải ảnh đại diện tùy chỉnh chỉ dành cho tài khoản MoonPro/MoonStu" });
            }

            if (avatarFile == null || avatarFile.Length == 0)
            {
                return BadRequest(new { success = false, message = "Vui lòng chọn ảnh để tải lên" });
            }

            // Giới hạn dung lượng file: 2MB (2 * 1024 * 1024)
            long maxFileSize = 2 * 1024 * 1024;
            if (avatarFile.Length > maxFileSize)
            {
                return BadRequest(new { success = false, message = "File ảnh quá lớn. Vui lòng chọn file dưới 2MB." });
            }

            // Kiểm tra định dạng (raw, jpeg, jpg, png)
            var extension = Path.GetExtension(avatarFile.FileName).ToLowerInvariant();
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".raw" };
            if (!allowedExtensions.Contains(extension))
            {
                return BadRequest(new { success = false, message = "Định dạng file không hợp lệ. Chỉ chấp nhận .jpg, .jpeg, .png, .raw." });
            }

            try
            {
                var uploadsFolder = Path.Combine(_environment.WebRootPath, "images", "uploads", "avatars");
                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                // Xóa avatar cũ nếu nó là avatar được upload trước đó
                string uploadDbPrefix = "/images/uploads/avatars/";
                if (!string.IsNullOrEmpty(user.Avatar) && user.Avatar.StartsWith(uploadDbPrefix))
                {
                    var oldAvatarPath = Path.Combine(_environment.WebRootPath, user.Avatar.TrimStart('/'));
                    if (System.IO.File.Exists(oldAvatarPath))
                    {
                        System.IO.File.Delete(oldAvatarPath);
                    }
                }

                var uniqueFileName = $"user_{userId}_{Guid.NewGuid()}{extension}";
                var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await avatarFile.CopyToAsync(fileStream);
                }

                user.Avatar = $"{uploadDbPrefix}{uniqueFileName}";
                user.UpdatedAt = DateTime.Now;
                var result = await _userManager.UpdateAsync(user);

                if (result.Succeeded)
                {
                    var profile = await _profileService.GetUserProfileAsync(userId.Value);
                    return Ok(new { success = true, message = "Tải ảnh đại diện thành công", data = profile });
                }

                return BadRequest(new { success = false, message = "Không thể lưu ảnh đại diện vào tài khoản" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi upload avatar cho User ID {UserId}", userId);
                return StatusCode(500, new { success = false, message = "Có lỗi xảy ra trong quá trình lưu ảnh" });
            }
        }

        // POST: /api/profile/fcm-token
        [HttpPost("fcm-token")]
        public async Task<IActionResult> UpdateFcmToken([FromBody] RegisterFcmTokenDto model)
        {
            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized(new { success = false, message = "Vui lòng đăng nhập" });

            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null) return NotFound(new { success = false, message = "Không tìm thấy người dùng" });

            user.FcmToken = model.FcmToken;
            user.UpdatedAt = DateTime.Now;
            var result = await _userManager.UpdateAsync(user);

            if (result.Succeeded)
            {
                return Ok(new { success = true, message = "Đăng ký thiết bị nhận thông báo thành công" });
            }

            return BadRequest(new { success = false, message = "Không thể đăng ký thiết bị nhận thông báo" });
        }

        // POST: /api/profile/sync-student
        [HttpPost("sync-student")]
        public async Task<IActionResult> SyncStudentStatus()
        {
            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized(new { success = false, message = "Vui lòng đăng nhập" });

            var profile = await _profileService.GetUserProfileAsync(userId.Value);
            if (profile == null) return NotFound(new { success = false, message = "Không tìm thấy người dùng" });

            return Ok(new 
            { 
                success = true, 
                message = "Đồng bộ trạng thái sinh viên thành công",
                isStudentVerified = profile.IsStudentVerified,
                subscriptionType = profile.SubscriptionType,
                subscriptionEndDate = profile.SubscriptionEndDate
            });
        }

        private int? GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(userIdClaim, out int userId)) return userId;

            var userIdClaim2 = User.FindFirst("UserId")?.Value;
            if (int.TryParse(userIdClaim2, out int userId2)) return userId2;

            var subClaim = User.FindFirst("sub")?.Value;
            if (int.TryParse(subClaim, out int userId3)) return userId3;

            return null;
        }
    }

    public class SelectAvatarDto
    {
        public string AvatarName { get; set; } = string.Empty;
    }

    public class RegisterFcmTokenDto
    {
        public string FcmToken { get; set; } = string.Empty;
    }
}

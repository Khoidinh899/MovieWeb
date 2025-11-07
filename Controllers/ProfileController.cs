using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using MovieWeb.Services;
using MovieWeb.Models.DTOs;
using MovieWeb.Models.Entities;
using System.Security.Claims;
// using MovieWeb.Data; // <-- KHÔNG CẦN NỮA
// using Microsoft.EntityFrameworkCore; // <-- KHÔNG CẦN NỮA
using MovieWeb.Models;

namespace MovieWeb.Controllers
{
    [Authorize]
    [Route("user")]
    public class ProfileController : Controller
    {
        // private readonly MovieWebDbContext _context; // <-- ĐÃ XÓA
        private readonly IStudentEmailService _studentEmailService;
        private readonly IProfileService _profileService;
        private readonly ILogger<ProfileController> _logger;
        private readonly UserManager<User> _userManager;
        private readonly SignInManager<User> _signInManager;

        public ProfileController(
            // MovieWebDbContext context, // <-- ĐÃ XÓA
            IProfileService profileService,
            ILogger<ProfileController> logger,
            UserManager<User> userManager,
            SignInManager<User> signInManager,
            IStudentEmailService studentEmailService)
        {
            // _context = context; // <-- ĐÃ XÓA
            _profileService = profileService;
            _logger = logger;
            _userManager = userManager; 
            _signInManager = signInManager; 
            _studentEmailService = studentEmailService;
        }

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(userIdClaim, out var userId) ? userId : 0;
        }

        private bool IsAdmin() => User.HasClaim("RoleId", "1");

        // ====================== API khu vực profile ======================
        // (Các API StudentEmailService giữ nguyên)

        [HttpPost("/api/profile/send-student-otp")]
        public async Task<IActionResult> SendStudentEmailOtp([FromBody] SendStudentOtpRequest request)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == 0)
                    return Json(new { isSuccess = false, message = "Vui lòng đăng nhập" });

                var result = await _studentEmailService.SendVerificationCodeAsync(userId, request.StudentEmail);
                return Json(new { isSuccess = result.IsSuccess, message = result.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending student email OTP");
                return Json(new { isSuccess = false, message = "Có lỗi xảy ra" });
            }
        }

        [HttpPost("/api/profile/verify-student-email")]
        public async Task<IActionResult> VerifyStudentEmail([FromBody] VerifyStudentEmailRequest request)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == 0)
                    return Json(new { isSuccess = false, message = "Vui lòng đăng nhập" });

                var result = await _studentEmailService.VerifyStudentEmailAsync(userId, request.OtpCode);
                return Json(new { isSuccess = result.IsSuccess, message = result.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying student email");
                return Json(new { isSuccess = false, message = "Có lỗi xảy ra" });
            }
        }

        [HttpGet("/api/profile/check-student-email-status")]
        public async Task<IActionResult> CheckStudentEmailStatus()
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == 0)
                    return Json(new { success = false, message = "Vui lòng đăng nhập" });

                var user = await _userManager.FindByIdAsync(userId.ToString());
                if (user == null)
                    return Json(new { success = false, message = "Không tìm thấy người dùng" });

                return Json(new
                {
                    success = true,
                    isVerified = user.IsStudentVerified,
                    studentEmail = user.StudentEmail,
                    verifiedAt = user.StudentEmailVerifiedAt,
                    needsReverification = user.NeedsStudentEmailReverification()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking student email status");
                return Json(new { success = false, message = "Có lỗi xảy ra" });
            }
        }

        // ====================== View Routes ======================
        // (Profile, Edit GET, Edit POST, ChangePassword GET đã ổn)

        [HttpGet("profile")]
        public async Task<IActionResult> Profile()
        {
            var userId = GetCurrentUserId();
            if (userId == 0)
                return RedirectToAction("Login", "Auth");

            var profile = await _profileService.GetUserProfileAsync(userId);
            if (profile == null)
                return NotFound();

            return View("~/Views/User/Profile.cshtml", profile);
        }

        [HttpGet("edit")]
        public async Task<IActionResult> Edit()
        {
            var userId = GetCurrentUserId();
            var profile = await _profileService.GetUserProfileAsync(userId);

            if (profile == null) return NotFound();

            var model = new UpdateProfileDto
            {
                FirstName = profile.FirstName,
                LastName = profile.LastName,
                Email = profile.Email,
                CurrentEmail = profile.Email
            };

            return View("~/Views/User/Edit.cshtml", model);
        }

        [HttpPost("edit")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(UpdateProfileDto model)
        {
            if (!ModelState.IsValid)
                return View("~/Views/User/Edit.cshtml", model);

            var userId = GetCurrentUserId();
            var result = await _profileService.UpdateProfileAsync(userId, model);

            if (result.IsSuccess)
            {
                TempData["SuccessMessage"] = result.Message;
                return RedirectToAction(nameof(Profile));
            }

            foreach (var error in result.Errors)
                ModelState.AddModelError(string.Empty, error);

            return View("~/Views/User/Edit.cshtml", model);
        }

        [HttpGet("change-password")]
        public IActionResult ChangePassword()
        {
            return View("~/Views/User/ChangePassword.cshtml");
        }

        // ====================== CÁC HÀNH ĐỘNG ĐƯỢC REFACTOR ======================

        [HttpPost("change-password")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(ChangePasswordDto model)
        {
            if (!ModelState.IsValid)
                return View("~/Views/User/ChangePassword.cshtml", model);

            var user = await _userManager.GetUserAsync(User); // Cần user để RefreshSignIn
            if (user == null)
                return NotFound();

            var result = await _profileService.ChangePasswordAsync(user.Id, model);

            if (result.IsSuccess)
            {
                await _signInManager.RefreshSignInAsync(user);
                TempData["SuccessMessage"] = "Đổi mật khẩu thành công!";
                return RedirectToAction(nameof(Profile));
            }

            foreach (var error in result.Errors)
            {
                if (error.Contains("Mật khẩu hiện tại không đúng") || error.Contains("Incorrect password"))
                {
                    ModelState.AddModelError("CurrentPassword", "Mật khẩu hiện tại không đúng.");
                }
                else
                {
                    ModelState.AddModelError(string.Empty, error);
                }
            }

            return View("~/Views/User/ChangePassword.cshtml", model);
        }

        [HttpPost("upload-avatar")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadAvatar(IFormFile avatarFile)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == 0)
                    return Json(new { success = false, message = "Vui lòng đăng nhập" });

                var result = await _profileService.UpdateAvatarAsync(userId, avatarFile);

                if (result.IsSuccess)
                {
                    return Json(new
                    {
                        success = true,
                        message = result.Message,
                        avatar = result.Profile?.Avatar // Trả về avatar mới từ service
                    });
                }
                
                return Json(new { success = false, message = result.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading avatar");
                return Json(new { success = false, message = "Có lỗi server xảy ra: " + ex.Message });
            }
        }

        [HttpPost("select-avatar")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SelectAvatar([FromBody] SelectAvatarRequest request)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == 0)
                    return Json(new { success = false, message = "Vui lòng đăng nhập" });
                
                var allowedAvatars = new[] {
                    "default0.png", "default1.png", "default2.png", "default3.png",
                    "default4.png", "default5.png", "default6.png", "default7.png",
                    "default8.png", "default9.png", "default10.png", "default11.png",
                    "default12.png", "nouser.png"
                };

                if (!allowedAvatars.Contains(request.AvatarName))
                    return Json(new { success = false, message = "Avatar không hợp lệ" });

                await _profileService.DeleteAvatarAsync(userId);

                var user = await _userManager.FindByIdAsync(userId.ToString());
                if (user == null) 
                    return Json(new { success = false, message = "Không tìm thấy người dùng" });

                user.Avatar = $"/images/{request.AvatarName}";
                var result = await _userManager.UpdateAsync(user);

                if (result.Succeeded)
                {
                    return Json(new
                    {
                        success = true,
                        message = "Cập nhật avatar thành công!",
                        avatar = user.Avatar
                    });
                }

                return Json(new { success = false, message = "Cập nhật avatar thất bại" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error selecting avatar");
                return Json(new { success = false, message = "Có lỗi xảy ra: " + ex.Message });
            }
        }
        
        [HttpPost("delete-avatar")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteAvatar()
        {
            var userId = GetCurrentUserId();
            if (userId == 0)
                return Json(new { success = false, message = "Vui lòng đăng nhập" });

            var result = await _profileService.DeleteAvatarAsync(userId);

            if (result.IsSuccess)
            {
                return Json(new { 
                    success = true, 
                    message = result.Message, 
                    avatar = result.Profile?.Avatar // Trả về avatar mặc định mới
                });
            }
            
            return Json(new { success = false, message = result.Message });
        }


        // ===== [REFACTORED] =====
        [HttpGet("payment-history")]
        public async Task<IActionResult> PaymentHistory(int page = 1)
        {
            var userId = GetCurrentUserId();
            if (userId == 0)
                return RedirectToAction("Login", "Auth");

            const int pageSize = 10; 
            
            // === GỌI SERVICE ===
            var historyDto = await _profileService.GetPaymentHistoryAsync(userId, page, pageSize);

            // Gán dữ liệu cho ViewBag từ DTO
            ViewBag.CurrentPage = historyDto.CurrentPage;
            ViewBag.TotalPages = historyDto.TotalPages;
            ViewBag.TotalTransactions = historyDto.TotalTransactions;
            ViewBag.PageSize = historyDto.PageSize;

            if (!historyDto.HasTransactions && page == 1)
                ViewBag.Message = "Bạn chưa có giao dịch nào.";

            // Trả về list transactions từ DTO
            return View("~/Views/User/PaymentsHistory.cshtml", historyDto.Transactions);
        }

        // ====================== Request DTOs (giữ nguyên) ======================
        public class SelectAvatarRequest
        {
            public string AvatarName { get; set; } = string.Empty;
        }

        public class SendStudentOtpRequest
        {
            public string StudentEmail { get; set; } = string.Empty;
        }

        public class VerifyStudentEmailRequest
        {
            public string OtpCode { get; set; } = string.Empty;
        }
    }
}
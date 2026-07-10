using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using MovieWeb.Services;
using MovieWeb.Models.DTOs;
using MovieWeb.Models.Entities;
using System.Security.Claims;
using MovieWeb.Services.Interfaces;

namespace MovieWeb.Controllers
{
    [Authorize]
    [Route("user")]
    public class ProfileController : Controller
    {
        private readonly IStudentEmailService _studentEmailService;
        private readonly IProfileService _profileService;
        private readonly ILogger<ProfileController> _logger;
        private readonly UserManager<User> _userManager;
        private readonly SignInManager<User> _signInManager;
        private readonly IFavoriteService _favoriteService;
        private readonly IWatchHistoryService _watchHistoryService;
        private readonly INotificationService _notificationService;

        public ProfileController(
            IProfileService profileService,
            ILogger<ProfileController> logger,
            UserManager<User> userManager,
            SignInManager<User> signInManager,
            IStudentEmailService studentEmailService,
            IFavoriteService favoriteService,
            IWatchHistoryService watchHistoryService,
            INotificationService notificationService)
        {
            _profileService = profileService;
            _logger = logger;
            _userManager = userManager;
            _signInManager = signInManager;
            _studentEmailService = studentEmailService;
            _favoriteService = favoriteService;
            _watchHistoryService = watchHistoryService;
            _notificationService = notificationService;
        }

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(userIdClaim, out var userId) ? userId : 0;
        }

        private bool IsAdmin() => User.HasClaim("RoleId", "1");

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

        [HttpGet("profile")]
        public async Task<IActionResult> Profile()
        {
            var userId = GetCurrentUserId();
            if (userId == 0) return RedirectToAction("Login", "Auth");

            var profile = await _profileService.GetUserProfileAsync(userId);
            if (profile == null) return NotFound();

            ViewBag.UserProfile = profile;
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
                CurrentEmail = profile.Email,
                PhoneNumber = profile.PhoneNumber,
                DateOfBirth = profile.DateOfBirth,
                Gender = profile.Gender,
                Address = profile.Address,
                Bio = profile.Bio
            };

            ViewBag.UserProfile = profile;
            return View("~/Views/User/Edit.cshtml", model);
        }

        [HttpPost("edit")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(UpdateProfileDto model)
        {
            var userId = GetCurrentUserId();

            if (!ModelState.IsValid)
            {
                ViewBag.UserProfile = await _profileService.GetUserProfileAsync(userId);
                return View("~/Views/User/Edit.cshtml", model);
            }

            var result = await _profileService.UpdateProfileAsync(userId, model);

            if (result.IsSuccess)
            {
                TempData["SuccessMessage"] = result.Message;
                return RedirectToAction(nameof(Profile));
            }

            foreach (var error in result.Errors)
                ModelState.AddModelError(string.Empty, error);

            ViewBag.UserProfile = await _profileService.GetUserProfileAsync(userId);
            return View("~/Views/User/Edit.cshtml", model);
        }

        [HttpGet("change-password")]
        public async Task<IActionResult> ChangePassword()
        {
            ViewBag.UserProfile = await _profileService.GetUserProfileAsync(GetCurrentUserId());
            return View("~/Views/User/ChangePassword.cshtml");
        }

        [HttpPost("change-password")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(ChangePasswordDto model)
        {
            var userId = GetCurrentUserId();

            if (!ModelState.IsValid)
            {
                ViewBag.UserProfile = await _profileService.GetUserProfileAsync(userId);
                return View("~/Views/User/ChangePassword.cshtml", model);
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            var result = await _profileService.ChangePasswordAsync(user.Id, model);

            if (result.IsSuccess)
            {
                await _signInManager.RefreshSignInAsync(user);
                TempData["SuccessMessage"] = "Đổi mật khẩu thành công!";
                return RedirectToAction(nameof(Profile));
            }

            foreach (var error in result.Errors)
            {
                if (error.Contains("Incorrect password"))
                    ModelState.AddModelError("CurrentPassword", "Mật khẩu hiện tại không đúng.");
                else
                    ModelState.AddModelError(string.Empty, error);
            }

            ViewBag.UserProfile = await _profileService.GetUserProfileAsync(userId);
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

                return Json(new
                {
                    success = result.IsSuccess,
                    message = result.Message,
                    avatar = result.Profile?.Avatar
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading avatar");
                return Json(new { success = false, message = "Có lỗi server xảy ra" });
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

                var allowedAvatars = new[]
                {
                    "default0.png","default1.png","default2.png","default3.png",
                    "default4.png","default5.png","default6.png","default7.png",
                    "default8.png","default9.png","default10.png","default11.png",
                    "default12.png","nouser.png"
                };

                if (!allowedAvatars.Contains(request.AvatarName))
                    return Json(new { success = false, message = "Avatar không hợp lệ" });

                await _profileService.DeleteAvatarAsync(userId);

                var user = await _userManager.FindByIdAsync(userId.ToString());
                if (user == null)
                    return Json(new { success = false, message = "Không tìm thấy người dùng" });

                user.Avatar = $"/images/{request.AvatarName}";
                var updateResult = await _userManager.UpdateAsync(user);

                return Json(new
                {
                    success = updateResult.Succeeded,
                    message = updateResult.Succeeded ? "Cập nhật avatar thành công!" : "Cập nhật thất bại",
                    avatar = user.Avatar
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error selecting avatar");
                return Json(new { success = false, message = "Có lỗi xảy ra" });
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

            return Json(new
            {
                success = result.IsSuccess,
                message = result.Message,
                avatar = result.Profile?.Avatar
            });
        }

        [HttpGet("payment-history")]
        public async Task<IActionResult> PaymentHistory(string status = "all", int page = 1)
        {
            var userId = GetCurrentUserId();
            if (userId == 0) return RedirectToAction("Login", "Auth");

            ViewBag.UserProfile = await _profileService.GetUserProfileAsync(userId);
            const int pageSize = 10;

            var history = await _profileService.GetPaymentHistoryAsync(userId, page, pageSize, status);

            ViewBag.CurrentPage = history.CurrentPage;
            ViewBag.TotalPages = history.TotalPages;
            ViewBag.TotalTransactions = history.TotalTransactions;
            ViewBag.PageSize = history.PageSize;

            if (!history.HasTransactions && page == 1)
                ViewBag.Message = "Bạn chưa có giao dịch nào.";

            return View("~/Views/User/PaymentsHistory.cshtml", history.Transactions);
        }

        [HttpGet("favorite")]
        public async Task<IActionResult> Favorite(int page = 1)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == 0) return RedirectToAction("Login", "Auth");

                ViewBag.UserProfile = await _profileService.GetUserProfileAsync(userId);
                var favorites = await _favoriteService.GetUserFavoritesAsync(userId, page, 20);

                ViewBag.CurrentPage = page;
                ViewBag.TotalPages = favorites.TotalPages;
                ViewBag.HasPrevious = page > 1;
                ViewBag.HasNext = page < favorites.TotalPages;

                return View("~/Views/User/Favorite.cshtml", favorites.Movies);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading favorites");
                TempData["Error"] = "Không thể tải danh sách yêu thích";
                return RedirectToAction("Index", "Home");
            }
        }

        [HttpGet("history")]
        public async Task<IActionResult> History(int page = 1)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == 0) return RedirectToAction("Login", "Auth");

                ViewBag.UserProfile = await _profileService.GetUserProfileAsync(userId);

                var history = await _watchHistoryService.GetUserHistoryAsync(userId, page, 20);

                ViewBag.CurrentPage = page;
                ViewBag.TotalPages = history.TotalPages;
                ViewBag.HasPrevious = page > 1;
                ViewBag.HasNext = page < history.TotalPages;

                return View("~/Views/User/History.cshtml", history.Movies);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading watch history");
                TempData["Error"] = "Không thể tải lịch sử xem";
                return RedirectToAction("Index", "Home");
            }
        }

        [HttpGet("notifications")]
        public async Task<IActionResult> Notifications(string type = "all", string status = "all", int page = 1)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == 0) return RedirectToAction("Login", "Auth");

                // Lấy UserProfile cho sidebar
                ViewBag.UserProfile = await _profileService.GetUserProfileAsync(userId);

                // Lấy notifications với pagination
                const int pageSize = 20;
                var allNotifications = await _notificationService.GetNotificationsAsync(userId, type, 1000);

                // Lọc theo status
                var filteredNotifications = status switch
                {
                    "unread" => allNotifications.Where(n => n.IsRead == false).ToList(),
                    "read" => allNotifications.Where(n => n.IsRead == true).ToList(),
                    _ => allNotifications
                };

                // Pagination
                var totalNotifications = filteredNotifications.Count;
                var totalPages = (int)Math.Ceiling(totalNotifications / (double)pageSize);
                var notifications = filteredNotifications
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                // Lấy unread count
                var unreadCount = await _notificationService.GetUnreadCountAsync(userId);

                ViewBag.CurrentType = type;
                ViewBag.CurrentStatus = status;
                ViewBag.CurrentPage = page;
                ViewBag.TotalPages = totalPages;
                ViewBag.TotalNotifications = totalNotifications;
                ViewBag.UnreadCount = unreadCount;
                ViewBag.HasPrevious = page > 1;
                ViewBag.HasNext = page < totalPages;

                return View("~/Views/User/Notifications.cshtml", notifications);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading notifications page");
                TempData["Error"] = "Không thể tải danh sách thông báo";
                return RedirectToAction("Profile", "Profile");
            }
        }

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
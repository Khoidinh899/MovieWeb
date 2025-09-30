using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using MovieWeb.Services;
using MovieWeb.Models.DTOs;
using MovieWeb.Models.Entities;
using System.Security.Claims;

namespace MovieWeb.Controllers
{
    [Authorize]
    public class ProfileController : Controller
    {
        private readonly IProfileService _profileService;
        private readonly ILogger<ProfileController> _logger;
        private readonly UserManager<User> _userManager;
        private readonly SignInManager<User> _signInManager;

        public ProfileController(
            IProfileService profileService, 
            ILogger<ProfileController> logger,
            UserManager<User> userManager,
            SignInManager<User> signInManager)
        {
            _profileService = profileService;
            _logger = logger;
            _userManager = userManager;
            _signInManager = signInManager;
        }

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(userIdClaim, out var userId) ? userId : 0;
        }

        private bool IsAdmin()
        {
            return User.HasClaim("RoleId", "1");
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var userId = GetCurrentUserId();
            if (userId == 0) return RedirectToAction("Login", "Auth");

            var profile = await _profileService.GetUserProfileAsync(userId);
            if (profile == null) return NotFound();

            return View(profile);
        }

        [HttpGet]
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

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(UpdateProfileDto model)
        {
            if (!ModelState.IsValid) return View(model);

            var userId = GetCurrentUserId();
            var result = await _profileService.UpdateProfileAsync(userId, model);

            if (result.IsSuccess)
            {
                TempData["SuccessMessage"] = result.Message;
                return RedirectToAction(nameof(Index));
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error);
            }

            return View(model);
        }

        [HttpGet]
        public IActionResult ChangePassword()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(ChangePasswordDto model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound();
            }

            // Đổi mật khẩu bằng Identity
            var result = await _userManager.ChangePasswordAsync(
                user, 
                model.CurrentPassword, 
                model.NewPassword);

            if (result.Succeeded)
            {
                await _signInManager.RefreshSignInAsync(user);
                
                TempData["SuccessMessage"] = "Đổi mật khẩu thành công!";
                return RedirectToAction("Index");
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateAvatar(IFormFile avatar)
        {
            if (avatar == null)
                return Json(new { success = false, message = "Vui lòng chọn ảnh" });

            var userId = GetCurrentUserId();
            var result = await _profileService.UpdateAvatarAsync(userId, avatar);

            return Json(new
            {
                success = result.IsSuccess,
                message = result.Message,
                avatar = result.User?.Avatar
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteAvatar()
        {
            var userId = GetCurrentUserId();
            var result = await _profileService.DeleteAvatarAsync(userId);

            return Json(new
            {
                success = result.IsSuccess,
                message = result.Message
            });
        }

        [HttpGet]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> ManageUsers()
        {
            var users = await _profileService.GetAllUsersAsync();
            return View("~/Views/Admin/ManageUsers.cshtml", users);
        }

        [HttpGet]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> AdminEditUser(int id)
        {
            var profile = await _profileService.GetUserProfileAsync(id);
            if (profile == null) return NotFound();

            var model = new AdminUpdateUserDto
            {
                UserId = profile.UserId,
                FirstName = profile.FirstName,
                LastName = profile.LastName,
                Email = profile.Email,
                RoleId = profile.RoleId,
                IsActive = profile.IsActive
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> AdminEditUser(AdminUpdateUserDto model)
        {
            if (!ModelState.IsValid) return View(model);

            var currentUserId = GetCurrentUserId();
            if (model.UserId == currentUserId && model.RoleId != 1)
            {
                ModelState.AddModelError(string.Empty, "Bạn không thể thay đổi quyền của chính mình");
                return View(model);
            }

            var result = await _profileService.AdminUpdateUserAsync(model);

            if (result.IsSuccess)
            {
                TempData["SuccessMessage"] = result.Message;
                return RedirectToAction(nameof(ManageUsers));
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error);
            }

            return View(model);
        }

        [HttpGet]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> AdminChangePassword(int id)
        {
            var profile = await _profileService.GetUserProfileAsync(id);
            if (profile == null) return NotFound();

            var model = new AdminChangePasswordDto
            {
                UserId = id
            };

            ViewData["UserFullName"] = profile.FullName;
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> AdminChangePassword(AdminChangePasswordDto model)
        {
            if (!ModelState.IsValid) return View(model);

            var result = await _profileService.AdminChangePasswordAsync(model);

            if (result.IsSuccess)
            {
                TempData["SuccessMessage"] = result.Message;
                return RedirectToAction(nameof(ManageUsers));
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error);
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> AdminToggleStatus(int id)
        {
            var currentUserId = GetCurrentUserId();
            if (id == currentUserId)
            {
                return Json(new { success = false, message = "Bạn không thể vô hiệu hóa chính mình" });
            }

            var result = await _profileService.AdminToggleUserStatusAsync(id);

            return Json(new
            {
                success = result.IsSuccess,
                message = result.Message
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> AdminDeleteUser(int id)
        {
            var currentUserId = GetCurrentUserId();
            if (id == currentUserId)
            {
                return Json(new { success = false, message = "Bạn không thể xóa chính mình" });
            }

            var result = await _profileService.AdminDeleteUserAsync(id);

            return Json(new
            {
                success = result.IsSuccess,
                message = result.Message
            });
        }
    }
}
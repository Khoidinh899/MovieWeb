using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using MovieWeb.Services;
using MovieWeb.Models.DTOs;

namespace MovieWeb.Controllers
{
    public class AuthController : Controller
    {
        private readonly IAuthService _authService;
        private readonly ILogger<AuthController> _logger;

        public AuthController(IAuthService authService, ILogger<AuthController> logger)
        {
            _authService = authService;
            _logger = logger;
        }

        [HttpGet]
        public IActionResult Register()
        {
            return PartialView("~/Views/Shared/Partial/_AuthModal.cshtml", new RegisterDto());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterDto model)
        {
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Invalid model state for register: {Errors}", string.Join(", ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));
                return PartialView("~/Views/Shared/Partial/_AuthModal.cshtml", model);
            }

            try
            {
                var result = await _authService.RegisterAsync(model);

                if (result.IsSuccess)
                {
                    TempData["SuccessMessage"] = result.Message;
                    return RedirectToAction(nameof(Login));
                }

                ModelState.AddModelError(string.Empty, result.Message);
                return PartialView("~/Views/Shared/Partial/_AuthModal.cshtml", model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Register for {Email}", model.Email);
                ModelState.AddModelError(string.Empty, "Có lỗi xảy ra trong quá trình đăng ký");
                return PartialView("~/Views/Shared/Partial/_AuthModal.cshtml", model);
            }
        }

        [HttpGet]
        public IActionResult Login(string returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return PartialView("~/Views/Shared/Partial/_AuthModal.cshtml", new LoginDto());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginDto model, string returnUrl = null)
        {
            if (!ModelState.IsValid)
            {
                // Nếu là AJAX request, trả về JSON
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new
                    {
                        success = false,
                        message = "Dữ liệu không hợp lệ",
                        errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList()
                    });
                }
                return PartialView("~/Views/Shared/Partial/_AuthModal.cshtml", model);
            }

            try
            {
                var result = await _authService.LoginAsync(model);

                if (result.IsSuccess)
                {
                    // Xác định URL redirect
                    string redirectUrl;
                    if (result.User?.IsAdmin == true)
                    {
                        redirectUrl = Url.Action("Dashboard", "Admin")!;
                    }
                    else if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                    {
                        redirectUrl = returnUrl;
                    }
                    else
                    {
                        redirectUrl = Url.Action("TrangChu", "TrangChu")!;
                    }

                    // Nếu là AJAX, trả JSON với redirectUrl
                    if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    {
                        return Json(new
                        {
                            success = true,
                            message = result.Message,
                            redirectUrl = redirectUrl,
                            isAdmin = result.User?.IsAdmin ?? false
                        });
                    }

                    // Nếu không phải AJAX, redirect bình thường
                    return Redirect(redirectUrl);
                }

                // Login failed
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { success = false, message = result.Message });
                }

                ModelState.AddModelError(string.Empty, result.Message);
                return PartialView("~/Views/Shared/Partial/_AuthModal.cshtml", model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Login for {Email}", model.Email);

                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { success = false, message = "Có lỗi xảy ra trong quá trình đăng nhập" });
                }

                ModelState.AddModelError(string.Empty, "Có lỗi xảy ra trong quá trình đăng nhập");
                return PartialView("~/Views/Shared/Partial/_AuthModal.cshtml", model);
            }
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            try
            {
                await _authService.LogoutAsync();
                TempData["SuccessMessage"] = "Đăng xuất thành công";
                return RedirectToAction("TrangChu", "TrangChu");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Logout");
                TempData["ErrorMessage"] = "Có lỗi xảy ra khi đăng xuất";
                return RedirectToAction("TrangChu", "TrangChu");
            }
        }

        [HttpGet]
        public IActionResult ForgotPassword()
        {
            return PartialView("~/Views/Shared/Partial/_AuthModal.cshtml", new ForgotPasswordDto());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordDto model)
        {
            if (!ModelState.IsValid)
            {
                return PartialView("~/Views/Shared/Partial/_AuthModal.cshtml", model);
            }

            try
            {
                var result = await _authService.ForgotPasswordAsync(model.Email);

                if (result)
                {
                    TempData["SuccessMessage"] = "Nếu email tồn tại trong hệ thống, chúng tôi đã gửi link đặt lại mật khẩu.";
                    return RedirectToAction(nameof(Login));
                }

                ModelState.AddModelError(string.Empty, "Có lỗi xảy ra, vui lòng thử lại.");
                return PartialView("~/Views/Shared/Partial/_AuthModal.cshtml", model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ForgotPassword for {Email}", model.Email);
                ModelState.AddModelError(string.Empty, "Có lỗi xảy ra khi gửi yêu cầu đặt lại mật khẩu");
                return PartialView("~/Views/Shared/Partial/_AuthModal.cshtml", model);
            }
        }

        [HttpGet]
        public IActionResult ResetPassword(string userId, string token)
        {
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(token))
            {
                TempData["ErrorMessage"] = "Link đặt lại mật khẩu không hợp lệ.";
                return RedirectToAction(nameof(Login));
            }

            var model = new ResetPasswordDto
            {
                UserId = userId,
                Token = token
            };

            return PartialView("~/Views/Shared/Partial/_AuthModal.cshtml", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordDto model)
        {
            if (!ModelState.IsValid)
            {
                return PartialView("~/Views/Shared/Partial/_AuthModal.cshtml", model);
            }

            try
            {
                var result = await _authService.ResetPasswordAsync(model);

                if (result.IsSuccess)
                {
                    TempData["SuccessMessage"] = result.Message;
                    return RedirectToAction(nameof(Login));
                }

                ModelState.AddModelError(string.Empty, result.Message);
                return PartialView("~/Views/Shared/Partial/_AuthModal.cshtml", model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ResetPassword for {UserId}", model.UserId);
                ModelState.AddModelError(string.Empty, "Có lỗi xảy ra khi đặt lại mật khẩu");
                return PartialView("~/Views/Shared/Partial/_AuthModal.cshtml", model);
            }
        }

        [HttpGet]
        public async Task<IActionResult> ConfirmEmail(string userId, string token)
        {
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(token))
            {
                TempData["ErrorMessage"] = "Link xác thực email không hợp lệ.";
                return RedirectToAction(nameof(Login));
            }

            try
            {
                var result = await _authService.ConfirmEmailAsync(userId, token);

                if (result.IsSuccess)
                {
                    TempData["SuccessMessage"] = result.Message;
                }
                else
                {
                    TempData["ErrorMessage"] = result.Message;
                }

                return RedirectToAction(nameof(Login));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ConfirmEmail for {UserId}", userId);
                TempData["ErrorMessage"] = "Có lỗi xảy ra khi xác thực email";
                return RedirectToAction(nameof(Login));
            }
        }
    }
}
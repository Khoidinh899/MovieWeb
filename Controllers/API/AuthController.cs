using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using MovieWeb.Services;
using MovieWeb.Models.DTOs;
using MovieWeb.Models.Entities;
using Microsoft.Extensions.Configuration;

namespace MovieWeb.Controllers
{
    public class AuthController : Controller
    {
        private readonly IAuthService _authService;
        private readonly ILogger<AuthController> _logger;
        private readonly UserManager<User> _userManager;
        private readonly SignInManager<User> _signInManager;
        private readonly IConfiguration _configuration;
        private readonly IEmailService _emailService;

        public AuthController(
            IAuthService authService,
            ILogger<AuthController> logger,
            UserManager<User> userManager,
            SignInManager<User> signInManager,
            IConfiguration configuration,
            IEmailService emailService)
        {
            _authService = authService;
            _logger = logger;
            _userManager = userManager;
            _signInManager = signInManager;
            _configuration = configuration;
            _emailService = emailService;
        }

        // GET: /Auth/Register
        [HttpGet]
        public IActionResult Register()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToAction("TrangChu", "TrangChu");
            }

            return PartialView("~/Views/Shared/Partial/_AuthModal.cshtml", new RegisterDto());
        }

        // POST: /Auth/Register
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterDto model)
        {
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Invalid model state for register: {Errors}",
                    string.Join(", ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));
                return PartialView("~/Views/Shared/Partial/_AuthModal.cshtml", model);
            }

            try
            {
                var result = await _authService.RegisterAsync(model);

                if (result.IsSuccess)
                {
                    // Nếu AppSettings:BaseUrl cấu hình sai (vd: port khác), chúng ta gửi lại email với base url thực tế
                    var configuredBaseUrl = (_configuration["AppSettings:BaseUrl"] ?? string.Empty).TrimEnd('/');
                    var actualBaseUrl = $"{Request.Scheme}://{Request.Host}".TrimEnd('/');

                    if (!string.Equals(configuredBaseUrl, actualBaseUrl, StringComparison.OrdinalIgnoreCase))
                    {
                        // Lấy user vừa tạo (theo email)
                        var user = await _userManager.FindByEmailAsync(model.Email);
                        if (user != null)
                        {
                            // Tạo token mới và build link đúng
                            var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                            var confirmationLink = $"{actualBaseUrl}/auth/confirm-email?userId={user.Id}&token={Uri.EscapeDataString(token)}";

                            try
                            {
                                await _emailService.SendEmailConfirmationAsync(user.Email!, model.FullName, confirmationLink);
                                _logger.LogInformation("Sent confirmation email using actual base URL to {Email}", user.Email);
                            }
                            catch (Exception exEmail)
                            {
                                _logger.LogError(exEmail, "Failed to send fallback confirmation email to {Email}", user.Email);
                            }
                        }
                    }

                    TempData["SuccessMessage"] = result.Message;
                    return RedirectToAction(nameof(Login));
                }

                // Thêm lỗi vào ModelState
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error);
                }

                return PartialView("~/Views/Shared/Partial/_AuthModal.cshtml", model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Register for {Email}", model.Email);
                ModelState.AddModelError(string.Empty, "Có lỗi xảy ra trong quá trình đăng ký. Vui lòng thử lại.");
                return PartialView("~/Views/Shared/Partial/_AuthModal.cshtml", model);
            }
        }

        // GET: /Auth/Login
        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToAction("TrangChu", "TrangChu");
            }

            ViewData["ReturnUrl"] = returnUrl;
            return PartialView("~/Views/Shared/Partial/_AuthModal.cshtml", new LoginDto());
        }

        // POST: /Auth/Login
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginDto model, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;

            if (!ModelState.IsValid)
            {
                // AJAX request
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new
                    {
                        success = false,
                        message = "Dữ liệu không hợp lệ",
                        errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList()
                    });
                }

                _logger.LogWarning("Invalid model state for login: {Errors}",
                    string.Join(", ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));
                return PartialView("~/Views/Shared/Partial/_AuthModal.cshtml", model);
            }

            try
            {
                var result = await _authService.LoginAsync(model);

                if (result.IsSuccess)
                {
                    // Xác định redirect URL
                    string redirectUrl;
                    if (result.User?.IsAdmin == true)
                    {
                        redirectUrl = Url.Action("Dashboard", "Admin") ?? "/";
                    }
                    else if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                    {
                        redirectUrl = returnUrl;
                    }
                    else
                    {
                        redirectUrl = Url.Action("TrangChu", "TrangChu") ?? "/";
                    }

                    // AJAX response
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

                    TempData["SuccessMessage"] = result.Message;
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
                    return Json(new { success = false, message = "Có lỗi xảy ra trong quá trình đăng nhập. Vui lòng thử lại." });
                }

                ModelState.AddModelError(string.Empty, "Có lỗi xảy ra trong quá trình đăng nhập. Vui lòng thử lại.");
                return PartialView("~/Views/Shared/Partial/_AuthModal.cshtml", model);
            }
        }

        // POST: /Auth/Logout
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            try
            {
                var success = await _authService.LogoutAsync();

                if (success)
                {
                    TempData["SuccessMessage"] = "Đăng xuất thành công";
                }
                else
                {
                    TempData["ErrorMessage"] = "Có lỗi xảy ra khi đăng xuất";
                }

                return RedirectToAction("TrangChu", "TrangChu");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Logout");
                TempData["ErrorMessage"] = "Có lỗi xảy ra khi đăng xuất";
                return RedirectToAction("TrangChu", "TrangChu");
            }
        }

        // GET: /Auth/ForgotPassword
        [HttpGet]
        public IActionResult ForgotPassword()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToAction("TrangChu", "TrangChu");
            }

            return PartialView("~/Views/Shared/Partial/_AuthModal.cshtml", new ForgotPasswordDto());
        }

        // POST: /Auth/ForgotPassword
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

                // Luôn hiển thị thông báo thành công để tránh lộ thông tin user
                TempData["SuccessMessage"] = "Nếu email tồn tại trong hệ thống, chúng tôi đã gửi link đặt lại mật khẩu.";
                return RedirectToAction(nameof(Login));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ForgotPassword for {Email}", model.Email);
                ModelState.AddModelError(string.Empty, "Có lỗi xảy ra khi gửi yêu cầu đặt lại mật khẩu. Vui lòng thử lại.");
                return PartialView("~/Views/Shared/Partial/_AuthModal.cshtml", model);
            }
        }

        // GET: /Auth/ResetPassword
[HttpGet]
public IActionResult ResetPassword(string userId, string token)
{
    if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(token))
    {
        TempData["ErrorMessage"] = "Link đặt lại mật khẩu không hợp lệ.";
        return RedirectToAction("TrangChu", "TrangChu");
    }

    // Lưu vào TempData thay vì query params
    TempData["ResetPasswordUserId"] = userId;
    TempData["ResetPasswordToken"] = token;
    
    return RedirectToAction("TrangChu", "TrangChu");
}

        // POST: /Auth/ResetPassword
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

                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error);
                }

                return PartialView("~/Views/Shared/Partial/_AuthModal.cshtml", model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ResetPassword for {UserId}", model.UserId);
                ModelState.AddModelError(string.Empty, "Có lỗi xảy ra khi đặt lại mật khẩu. Vui lòng thử lại.");
                return PartialView("~/Views/Shared/Partial/_AuthModal.cshtml", model);
            }
        }

        // ✅ GET: /Auth/ConfirmEmail - Xác thực email và tự động đăng nhập
        [HttpGet("auth/confirm-email")]
        public async Task<IActionResult> ConfirmEmail(string userId, string token)
        {
            try
            {
                if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(token))
                {
                    _logger.LogWarning("ConfirmEmail called with missing userId or token");
                    TempData["ErrorMessage"] = "Link xác thực không hợp lệ.";
                    return RedirectToAction("TrangChu", "TrangChu");
                }

                var user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                {
                    _logger.LogWarning("ConfirmEmail: User not found with ID {UserId}", userId);
                    TempData["ErrorMessage"] = "Người dùng không tồn tại.";
                    return RedirectToAction("TrangChu", "TrangChu");
                }

                // Kiểm tra xem email đã được xác thực chưa
                if (user.EmailConfirmed)
                {
                    _logger.LogInformation("Email already confirmed for user {Email}", user.Email);

                    // Nếu đã xác thực rồi, đăng nhập luôn
                    await _signInManager.SignInAsync(user, isPersistent: true);
                    TempData["InfoMessage"] = "Email đã được xác thực trước đó. Bạn đã được đăng nhập.";
                    return RedirectToAction("TrangChu", "TrangChu");
                }

                // Xác thực email
                var result = await _userManager.ConfirmEmailAsync(user, token);

                if (result.Succeeded)
                {
                    _logger.LogInformation("Email confirmed successfully for user {Email}", user.Email);

                    // ✅ Đăng nhập tự động sau khi xác thực thành công
                    // isPersistent: true = Cookie sẽ tồn tại 30 ngày (theo cấu hình ExpireTimeSpan)
                    await _signInManager.SignInAsync(user, isPersistent: true);

                    TempData["SuccessMessage"] = "Xác thực email thành công! Chào mừng bạn đến với MoonPhim.";
                    return RedirectToAction("TrangChu", "TrangChu");
                }
                else
                {
                    _logger.LogWarning("Failed to confirm email for user {Email}. Errors: {Errors}",
                        user.Email,
                        string.Join(", ", result.Errors.Select(e => e.Description)));

                    TempData["ErrorMessage"] = "Xác thực email thất bại. Link có thể đã hết hạn hoặc không hợp lệ.";
                    return RedirectToAction("TrangChu", "TrangChu");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ConfirmEmail for userId: {UserId}", userId);
                TempData["ErrorMessage"] = "Có lỗi xảy ra trong quá trình xác thực email. Vui lòng thử lại.";
                return RedirectToAction("TrangChu", "TrangChu");
            }
        }

        // ✅ POST: Gửi lại email xác thực (nếu cần)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResendEmailConfirmation(string email)
        {
            try
            {
                if (string.IsNullOrEmpty(email))
                {
                    return Json(new { success = false, message = "Email không hợp lệ." });
                }

                var user = await _userManager.FindByEmailAsync(email);
                if (user == null)
                {
                    // Không tiết lộ user có tồn tại hay không
                    return Json(new { success = true, message = "Nếu email tồn tại, chúng tôi đã gửi lại link xác thực." });
                }

                if (user.EmailConfirmed)
                {
                    return Json(new { success = false, message = "Email đã được xác thực." });
                }

                // Tạo token mới
                var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                var actualBaseUrl = $"{Request.Scheme}://{Request.Host}".TrimEnd('/');
                var confirmationLink = $"{actualBaseUrl}/auth/confirm-email?userId={user.Id}&token={Uri.EscapeDataString(token)}";

                // Gửi email
                await _emailService.SendEmailConfirmationAsync(user.Email!, user.FullName ?? user.Email, confirmationLink);

                _logger.LogInformation("Resent confirmation email to {Email}", user.Email);

                return Json(new { success = true, message = "Email xác thực đã được gửi lại. Vui lòng kiểm tra hộp thư." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ResendEmailConfirmation for {Email}", email);
                return Json(new { success = false, message = "Có lỗi xảy ra. Vui lòng thử lại sau." });
            }
        }

        // GET: /Auth/AccessDenied
        [HttpGet]
        public IActionResult AccessDenied()
        {
            ViewData["Title"] = "Truy cập bị từ chối";
            return View();
        }
    }
}
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using MovieWeb.Services;
using MovieWeb.Models.DTOs;
using MovieWeb.Models.Entities;
using Microsoft.Extensions.Configuration;
using MovieWeb.Services.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using System.Security.Claims;

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
        private readonly ITurnstileService _turnstileService;

        public AuthController(
            IAuthService authService,
            ILogger<AuthController> logger,
            UserManager<User> userManager,
            SignInManager<User> signInManager,
            IConfiguration configuration,
            IEmailService emailService,
            ITurnstileService turnstileService)
        {
            _authService = authService;
            _logger = logger;
            _userManager = userManager;
            _signInManager = signInManager;
            _configuration = configuration;
            _emailService = emailService;
            _turnstileService = turnstileService;
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
            var turnstileResponse = Request.Form["cf-turnstile-response"].ToString();
            var clientIp = Request.HttpContext.Connection.RemoteIpAddress?.ToString();

            if (!await _turnstileService.VerifyTokenAsync(turnstileResponse, clientIp))
            {
                _logger.LogWarning("CAPTCHA verification failed for email: {Email}", model.Email);
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { success = false, message = "Xác thực CAPTCHA không thành công. Vui lòng thử lại." });
                }
                return BadRequest(new { success = false, message = "Xác thực CAPTCHA không thành công." });
            }

            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Invalid model state for register: {Errors}",
                    string.Join(", ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));

                // Trả về lỗi
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { success = false, message = "Dữ liệu không hợp lệ", errors });
                }
                return BadRequest(new { success = false, message = "Dữ liệu không hợp lệ", errors });
            }

            try
            {
                var result = await _authService.RegisterAsync(model);

                if (result.IsSuccess)
                {
                    // Kiểm tra URL cấu hình với URL thực tế — gửi email xác thực đúng domain
                    var configuredBaseUrl = (_configuration["AppSettings:BaseUrl"] ?? string.Empty).TrimEnd('/');
                    var actualBaseUrl = $"{Request.Scheme}://{Request.Host}".TrimEnd('/');

                    if (!string.Equals(configuredBaseUrl, actualBaseUrl, StringComparison.OrdinalIgnoreCase))
                    {
                        var user = await _userManager.FindByEmailAsync(model.Email);

                        if (user != null)
                        {
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

                    // Đăng ký thành công
                    if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    {
                        return Json(new { success = true, message = "Đăng ký thành công. Vui lòng xác thực email.", email = model.Email });
                    }
                    return RedirectToAction(nameof(Login));
                }

                // Trả về lỗi nếu đăng ký thất bại
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { success = false, message = result.Message ?? "Đăng ký thất bại", errors = result.Errors });
                }
                return BadRequest(new { success = false, message = result.Message ?? "Đăng ký thất bại", errors = result.Errors });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Register for {Email}", model.Email);

                return StatusCode(500, new
                {
                    success = false,
                    message = "Có lỗi xảy ra trong quá trình đăng ký. Vui lòng thử lại."
                });
            }
        }


        // GET: /Auth/Login
        // Tìm method Login [HttpGet] và sửa thành:

        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            // Nếu đã đăng nhập rồi thì về trang chủ
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToAction("TrangChu", "TrangChu");
            }

            // ✅ Lưu returnUrl vào TempData và redirect về trang chủ
            if (!string.IsNullOrEmpty(returnUrl))
            {
                TempData["LoginReturnUrl"] = returnUrl;
                _logger.LogInformation("Saving LoginReturnUrl: {ReturnUrl}", returnUrl);
            }

            // ✅ Redirect về trang chủ (không render PartialView nữa)
            return RedirectToAction("TrangChu", "TrangChu");
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

                // Trả Redirect (hoạt động cho cả AJAX hoặc form submit)
                return RedirectToAction("TrangChu", "TrangChu");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Logout");
                TempData["ErrorMessage"] = "Có lỗi xảy ra khi đăng xuất";

                return RedirectToAction("TrangChu", "TrangChu");
            }
        }

        // ==========================================
        // GOOGLE OAUTH
        // ==========================================

        [HttpGet]
        [AllowAnonymous]
        public IActionResult ExternalLogin(string provider, string? returnUrl = null)
        {
            var redirectUrl = Url.Action(nameof(ExternalLoginCallback), "Auth", new { returnUrl });
            var properties = _signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
            return Challenge(properties, provider);
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> ExternalLoginCallback(string? returnUrl = null, string? remoteError = null)
        {
            returnUrl = returnUrl ?? Url.Content("~/");

            if (remoteError != null)
            {
                _logger.LogWarning("Remote error from external login: {Error}", remoteError);
                return RedirectToAction("TrangChu", "TrangChu", new { auth = "login" });
            }

            var info = await _signInManager.GetExternalLoginInfoAsync();
            if (info == null)
            {
                TempData["ErrorMessage"] = "Đăng nhập bị hủy hoặc có lỗi xảy ra.";
                return RedirectToAction("TrangChu", "TrangChu", new { auth = "login" });
            }

            // Đăng nhập bằng provider (Google) nếu user đã từng liên kết
            var result = await _signInManager.ExternalLoginSignInAsync(info.LoginProvider, info.ProviderKey, isPersistent: true, bypassTwoFactor: true);

            if (result.Succeeded)
            {
                _logger.LogInformation("{Name} logged in with {LoginProvider} provider.", info.Principal.Identity?.Name, info.LoginProvider);
                return LocalRedirect(returnUrl);
            }

            if (result.IsLockedOut)
            {
                return RedirectToAction("AccessDenied");
            }

            // Nếu user chưa có tài khoản, tạo mới
            var email = info.Principal.FindFirstValue(ClaimTypes.Email);
            if (email != null)
            {
                var user = await _userManager.FindByEmailAsync(email);

                if (user == null)
                {
                    // Tạo user mới
                    var fullName = info.Principal.FindFirstValue(ClaimTypes.Name) ?? email;
                    var parts = fullName.Trim().Split(' ');
                    var firstName = parts.Length > 0 ? parts.Last() : "";
                    var lastName = parts.Length > 1 ? string.Join(" ", parts.Take(parts.Length - 1)) : "";

                    user = new User
                    {
                        UserName = email,
                        Email = email,
                        FirstName = firstName,
                        LastName = lastName,
                        EmailConfirmed = true, // Google đã xác thực email
                        RoleId = 2, // User role
                        CreatedAt = DateTime.Now,
                        IsActive = true
                    };

                    var createResult = await _userManager.CreateAsync(user);
                    if (createResult.Succeeded)
                    {
                        createResult = await _userManager.AddLoginAsync(user, info);
                        if (createResult.Succeeded)
                        {
                            await _signInManager.SignInAsync(user, isPersistent: true);
                            _logger.LogInformation("User created an account using {Name} provider.", info.LoginProvider);
                            return LocalRedirect(returnUrl);
                        }
                    }

                    foreach (var error in createResult.Errors)
                    {
                        ModelState.AddModelError(string.Empty, error.Description);
                    }
                }
                else
                {
                    // Liên kết account hiện tại với Google
                    var addLoginResult = await _userManager.AddLoginAsync(user, info);
                    if (addLoginResult.Succeeded)
                    {
                        await _signInManager.SignInAsync(user, isPersistent: true);
                        return LocalRedirect(returnUrl);
                    }
                }
            }

            TempData["ErrorMessage"] = "Không thể đăng nhập bằng Google. Vui lòng thử lại.";
            return RedirectToAction("TrangChu", "TrangChu");
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
                // ❌ Model không hợp lệ → trả về 400 để JS xử lý
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();

                return BadRequest(new
                {
                    success = false,
                    message = "Dữ liệu không hợp lệ",
                    errors
                });
            }

            try
            {
                // Không để lộ tài khoản có tồn tại hay không
                await _authService.ForgotPasswordAsync(model.Email);

                // ⛔ Luôn trả về 200 (OK) để bảo mật
                return Ok(new
                {
                    success = true,
                    message = "Nếu email tồn tại, hệ thống đã gửi hướng dẫn đặt lại mật khẩu."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ForgotPassword for {Email}", model.Email);

                // ⚠️ Server lỗi → trả về 500
                return StatusCode(500, new
                {
                    success = false,
                    message = "Có lỗi xảy ra khi xử lý yêu cầu. Vui lòng thử lại sau."
                });
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

            // ✅ Redirect với query params
            return RedirectToAction("TrangChu", "TrangChu", new { userId, token });
        }

        // POST: /Auth/ResetPassword
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordDto model)
        {
            if (!ModelState.IsValid)
            {
                // ❌ Model không hợp lệ → trả về 400 (BadRequest)
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();

                return BadRequest(new
                {
                    success = false,
                    message = "Dữ liệu không hợp lệ",
                    errors
                });
            }

            try
            {
                var result = await _authService.ResetPasswordAsync(model);

                if (result.IsSuccess)
                {
                    // ✅ Thành công → trả về 200 (OK)
                    return Ok(new
                    {
                        success = true,
                        message = "Đặt lại mật khẩu thành công."
                    });
                }

                // ❌ Thất bại → trả về 400 (BadRequest)
                return BadRequest(new
                {
                    success = false,
                    message = "Đặt lại mật khẩu thất bại.",
                    errors = result.Errors
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ResetPassword for {UserId}", model.UserId);

                // ⚠️ Lỗi server → trả về 500
                return StatusCode(500, new
                {
                    success = false,
                    message = "Có lỗi xảy ra khi đặt lại mật khẩu."
                });
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
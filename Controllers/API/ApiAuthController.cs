using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using MovieWeb.Services.Interfaces;
using MovieWeb.Models.DTOs;
using MovieWeb.Models.Entities;
using System.Security.Claims;

namespace MovieWeb.Controllers.API
{
    [ApiController]
    [Route("api/auth")]
    public class ApiAuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly UserManager<User> _userManager;
        private readonly ILogger<ApiAuthController> _logger;
        private readonly IConfiguration _configuration;

        public ApiAuthController(
            IAuthService authService,
            UserManager<User> userManager,
            ILogger<ApiAuthController> logger,
            IConfiguration configuration)
        {
            _authService = authService;
            _userManager = userManager;
            _logger = logger;
            _configuration = configuration;
        }

        // POST: /api/auth/login
        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<IActionResult> Login([FromBody] LoginDto model)
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

            try
            {
                var result = await _authService.LoginAsync(model);

                if (result.IsSuccess)
                {
                    return Ok(new
                    {
                        success = true,
                        message = result.Message,
                        token = result.Token,
                        user = new
                        {
                            id = result.User?.Id,
                            email = result.User?.Email,
                            userName = result.User?.UserName,
                            fullName = result.User?.FullName,
                            isAdmin = result.User?.IsAdmin ?? false,
                            isPremium = result.User?.IsPremium ?? false,
                            subscriptionType = result.User?.SubscriptionType ?? "free"
                        }
                    });
                }

                return Unauthorized(new { success = false, message = result.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Api Login for {Email}", model.Email);
                return StatusCode(500, new { success = false, message = "Có lỗi xảy ra trong quá trình đăng nhập." });
            }
        }

        // POST: /api/auth/register
        [HttpPost("register")]
        [AllowAnonymous]
        public async Task<IActionResult> Register([FromBody] RegisterDto model)
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

            try
            {
                var result = await _authService.RegisterAsync(model);

                if (result.IsSuccess)
                {
                    // Lấy user vừa tạo để sinh token đăng nhập luôn (nếu muốn auto-login sau khi đăng ký)
                    // Hoặc yêu cầu người dùng xác thực email trước tùy theo chính sách.
                    // Ở đây chúng ta tuân theo chính sách của AuthService (yêu cầu xác thực email).
                    
                    return Ok(new
                    {
                        success = true,
                        message = result.Message // "Đăng ký thành công. Vui lòng xác thực email."
                    });
                }

                return BadRequest(new { success = false, message = result.Message, errors = result.Errors });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Api Register for {Email}", model.Email);
                return StatusCode(500, new { success = false, message = "Có lỗi xảy ra trong quá trình đăng ký." });
            }
        }

        // POST: /api/auth/google-login
        [HttpPost("google-login")]
        [AllowAnonymous]
        public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginDto model)
        {
            if (string.IsNullOrEmpty(model.IdToken))
            {
                return BadRequest(new { success = false, message = "IdToken không được để trống." });
            }

            try
            {
                var payload = await Google.Apis.Auth.GoogleJsonWebSignature.ValidateAsync(model.IdToken);

                if (payload == null)
                    return BadRequest(new { success = false, message = "Token không hợp lệ." });

                var user = await _userManager.FindByEmailAsync(payload.Email);
                if (user == null)
                {
                    // Split Name into FirstName and LastName
                    var parts = payload.Name?.Split(' ') ?? Array.Empty<string>();
                    var firstName = parts.Length > 0 ? parts.Last() : "";
                    var lastName = parts.Length > 1 ? string.Join(" ", parts.Take(parts.Length - 1)) : "";

                    // Tạo tài khoản mới nếu chưa tồn tại
                    user = new User
                    {
                        UserName = payload.Email,
                        Email = payload.Email,
                        FirstName = firstName,
                        LastName = lastName,
                        EmailConfirmed = true,
                        IsActive = true,
                        RoleId = 2 // Mặc định là User
                    };

                    var createResult = await _userManager.CreateAsync(user);
                    if (!createResult.Succeeded)
                    {
                        return BadRequest(new { success = false, message = "Không thể tạo tài khoản từ Google." });
                    }
                }

                // Cấp phát JWT Token
                var token = _authService.GenerateJwtToken(user);

                return Ok(new
                {
                    success = true,
                    message = "Đăng nhập Google thành công",
                    token = token,
                    user = new
                    {
                        id = user.Id,
                        email = user.Email,
                        userName = user.UserName,
                        fullName = user.FullName,
                        isAdmin = user.IsAdmin,
                        isPremium = user.IsPremium,
                        subscriptionType = user.SubscriptionType ?? "free"
                    }
                });
            }
            catch (Google.Apis.Auth.InvalidJwtException)
            {
                return BadRequest(new { success = false, message = "Token Google không hợp lệ hoặc đã hết hạn." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Api Google Login");
                return StatusCode(500, new { success = false, message = "Có lỗi xảy ra khi đăng nhập bằng Google." });
            }
        }

        // GET: /api/auth/me
        [HttpGet("me")]
        [Authorize(AuthenticationSchemes = "JwtScheme")]
        public async Task<IActionResult> GetMe()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound();

            return Ok(new
            {
                success = true,
                user = new
                {
                    id = user.Id,
                    email = user.Email,
                    userName = user.UserName,
                    fullName = user.FullName,
                    isAdmin = user.IsAdmin,
                    isPremium = user.IsPremium,
                    subscriptionType = user.SubscriptionType ?? "free"
                }
            });
        }
    }

    public class GoogleLoginDto
    {
        public string IdToken { get; set; } = string.Empty;
    }
}

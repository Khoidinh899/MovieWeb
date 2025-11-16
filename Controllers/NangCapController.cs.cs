using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MovieWeb.Services.Interfaces;
using MovieWeb.Models;
using System.Security.Claims;
using MovieWeb.Services;

namespace MovieWeb.Controllers
{
    [Authorize]
    public class NangCapController : Controller
    {
        private readonly IProfileService _profileService;
        private readonly ISubscriptionService _subscriptionService;
        private readonly ILogger<NangCapController> _logger;

        public NangCapController(
            IProfileService profileService,
            ISubscriptionService subscriptionService,
            ILogger<NangCapController> logger)
        {
            _profileService = profileService;
            _subscriptionService = subscriptionService;
            _logger = logger;
        }

        // GET: /NangCap
        public async Task<IActionResult> Index()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var userProfile = await _profileService.GetUserProfileAsync(int.Parse(userId));

            if (userProfile == null)
                return NotFound("Không tìm thấy người dùng.");

            var plans = await _subscriptionService.GetAllPlansAsync();

            var viewModel = new NangCapViewModel
            {
                UserName = userProfile.Username,
                Avatar = userProfile.Avatar ?? string.Empty,
                CurrentStatus = userProfile.SubscriptionType == "free"
                    ? "Bạn đang là thành viên miễn phí."
                    : $"Bạn là thành viên {userProfile.SubscriptionType?.ToUpper()}",
                Balance = 0,
                Plans = plans
            };

            return View("NangCap", viewModel);
        }

        // GET: /NangCap/KetQuaThanhToan
        public async Task<IActionResult> KetQuaThanhToan([FromQuery] bool success, [FromQuery] string? session_id)
        {
            if (success && !string.IsNullOrEmpty(session_id))
            {
                try
                {
                    var result = await _subscriptionService.FulfillOrderAsync(session_id);

                    if (result)
                    {
                        ViewBag.Message = "Thanh toán thành công! Gói đăng ký của bạn đã được kích hoạt.";
                        ViewBag.IsSuccess = true;
                    }
                    else
                    {
                        ViewBag.Message = "Thanh toán hoàn tất nhưng xảy ra lỗi khi kích hoạt gói cước. Vui lòng liên hệ hỗ trợ.";
                        ViewBag.IsSuccess = false;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi khi xử lý kết quả thanh toán cho session: {SessionId}", session_id);
                    ViewBag.Message = "Có lỗi xảy ra trong quá trình xử lý thanh toán.";
                    ViewBag.IsSuccess = false;
                }
            }
            else
            {
                // Thanh toán thất bại hoặc hủy
                ViewBag.Message = "Thanh toán đã bị hủy. Bạn có thể thử lại bất kỳ lúc nào.";
                ViewBag.IsSuccess = false;
            }

            return View();
        }
    }
}

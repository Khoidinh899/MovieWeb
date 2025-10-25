using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MovieWeb.Services;
using MovieWeb.Services.Interfaces;
using MovieWeb.Models;
using System.Security.Claims;
using System.Threading.Tasks;

namespace MovieWeb.Controllers
{
    [Authorize]
    public class NangCapController : Controller
    {
        private readonly IAuthService _authService;
        private readonly ISubscriptionService _subscriptionService;
        private readonly ILogger<NangCapController> _logger;

        public NangCapController(
            IAuthService authService, 
            ISubscriptionService subscriptionService,
            ILogger<NangCapController> logger)
        {
            _authService = authService;
            _subscriptionService = subscriptionService;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            var userProfile = await _authService.GetUserProfileAsync(
                int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!)
            );

            if (userProfile == null)
            {
                return NotFound("Không tìm thấy người dùng.");
            }

            var plans = await _subscriptionService.GetAllPlansAsync();

            var viewModel = new NangCapViewModel
            {
                UserName = userProfile.Username,
                Avatar = userProfile.Avatar ?? string.Empty, // ✅ Fix null warning
                CurrentStatus = userProfile.IsPremium ? "Bạn là thành viên Premium" : "Bạn đang là thành viên miễn phí.",
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
                // ✅ Xử lý thanh toán thành công
                try
                {
                    // Gọi service để kích hoạt gói cước
                    var result = await _subscriptionService.FulfillOrderAsync(session_id);

                    if (result)
                    {
                        ViewBag.Message = "Thanh toán thành công! Gói đăng ký của bạn đã được kích hoạt.";
                        ViewBag.IsSuccess = true;
                    }
                    else
                    {
                        ViewBag.Message = "Thanh toán đã được xử lý nhưng có lỗi khi kích hoạt gói cước. Vui lòng liên hệ hỗ trợ.";
                        ViewBag.IsSuccess = false;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi khi xử lý kết quả thanh toán cho session: {SessionId}", session_id);
                    ViewBag.Message = "Có lỗi xảy ra khi xử lý thanh toán. Vui lòng liên hệ hỗ trợ.";
                    ViewBag.IsSuccess = false;
                }
            }
            else
            {
                // ✅ Xử lý trường hợp cancel hoặc thất bại
                ViewBag.Message = "Thanh toán đã bị hủy. Bạn có thể thử lại bất cứ lúc nào.";
                ViewBag.IsSuccess = false;
            }

            return View();
        }
    }
}
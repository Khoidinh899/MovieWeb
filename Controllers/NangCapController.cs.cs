using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MovieWeb.Services;
using MovieWeb.Services.Interfaces;
using MovieWeb.Models;
using System.Security.Claims;
using System.Threading.Tasks;

namespace MovieWeb.Controllers
{
    [Authorize] // Bắt buộc người dùng phải đăng nhập để truy cập
    public class NangCapController : Controller
    {
        private readonly IAuthService _authService;
        private readonly ISubscriptionService _subscriptionService;

        public NangCapController(IAuthService authService, ISubscriptionService subscriptionService)
        {
            _authService = authService;
            _subscriptionService = subscriptionService;
        }

        // Action này sẽ được gọi khi truy cập vào route /nang-cap
        public async Task<IActionResult> Index()
        {
            // Lấy thông tin người dùng hiện tại
            var userProfile = await _authService.GetUserProfileAsync(
                int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!)
            );

            if (userProfile == null)
            {
                // Nếu không tìm thấy user, trả về lỗi hoặc trang đăng nhập
                return NotFound("Không tìm thấy người dùng.");
            }

            // Lấy danh sách các gói cước
            var plans = await _subscriptionService.GetAllPlansAsync();

            // Tạo ViewModel để gửi dữ liệu ra View
            var viewModel = new NangCapViewModel
            {
                UserName = userProfile.Username,
                Avatar = userProfile.Avatar,
                CurrentStatus = userProfile.IsPremium ? "Bạn là thành viên Premium" : "Bạn đang là thành viên miễn phí.",
                Balance = 0, // Thay bằng số dư thật của user nếu có
                Plans = plans
            };

            return View("NangCap", viewModel);
        }
        // ✅ THÊM PHƯƠNG THỨC NÀY VÀO
        // Action này sẽ xử lý kết quả sau khi thanh toán
        // GET: /NangCap/KetQuaThanhToan
        public async Task<IActionResult> KetQuaThanhToan([FromQuery] bool? success, [FromQuery] string session_id)
        {
            if (success == true && !string.IsNullOrEmpty(session_id))
            {
                // Gọi service để kích hoạt gói cước
                var result = await _subscriptionService.FulfillOrderAsync(session_id);

                if (result)
                {
                    ViewBag.Message = "Thanh toán thành công! Gói cước của bạn đã được kích hoạt.";
                }
                else
                {
                    ViewBag.Message = "Thanh toán đã được xử lý nhưng có lỗi khi kích hoạt gói cước. Vui lòng liên hệ hỗ trợ.";
                }
            }
            else
            {
                ViewBag.Message = "Thanh toán đã bị hủy hoặc có lỗi xảy ra.";
            }

            return View(); // Sẽ tìm file View tên là KetQuaThanhToan.cshtml
        }
    }
}
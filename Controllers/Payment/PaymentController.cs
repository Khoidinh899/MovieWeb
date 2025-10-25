using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MovieWeb.Models.DTOs;
using MovieWeb.Services.Interfaces;
using System.Security.Claims;

namespace MovieWeb.Controllers.API
{
    [Route("api/[controller]")]
    [ApiController]
    public class PaymentController : ControllerBase
    {
        private readonly IStripeService _stripeService;
        private readonly ISubscriptionService _subscriptionService;
        private readonly IConfiguration _configuration;
        private readonly ILogger<PaymentController> _logger; // ✅ 1. THÊM DÒNG NÀY


        public PaymentController(
        IStripeService stripeService,
        ISubscriptionService subscriptionService,
        IConfiguration configuration,
        ILogger<PaymentController> logger) // ✅ 2. THÊM THAM SỐ NÀY
        {
            _stripeService = stripeService;
            _subscriptionService = subscriptionService;
            _configuration = configuration;
            _logger = logger; // ✅ 3. THÊM DÒNG NÀY
        }

        // POST: api/payment/create-checkout-session
        [HttpPost("create-checkout-session")]
        [Authorize]
        public async Task<IActionResult> CreateCheckoutSession([FromBody] CreateCheckoutSessionRequest request)
        {
            try
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
                var user = await GetUserAsync(userId);

                if (user == null)
                    return NotFound(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "User not found"
                    });

                var plan = await _subscriptionService.GetPlanByIdAsync(request.PlanId);
                if (plan == null)
                    return NotFound(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Plan not found"
                    });

                // Kiểm tra Student plan
                if (plan.PlanType == "Student" && !user.IsStudentVerified)
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Bạn cần xác thực email sinh viên (.edu) trước khi mua gói Student"
                    });
                }

                var planEntity = await GetPlanEntityAsync(request.PlanId);
                if (planEntity == null)
                    return NotFound(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Plan not found"
                    });

                var baseUrl = _configuration["AppSettings:BaseUrl"] ?? Request.Scheme + "://" + Request.Host;
                var successUrl = request.SuccessUrl ?? $"{baseUrl}/api/payment/success?session_id={{CHECKOUT_SESSION_ID}}";
                var cancelUrl = request.CancelUrl ?? $"{baseUrl}/api/payment/cancel";

                var session = await _stripeService.CreateCheckoutSessionAsync(
                    user,
                    planEntity,
                    successUrl,
                    cancelUrl
                );

                return Ok(new ApiResponse<CheckoutSessionResponse>
                {
                    Success = true,
                    Message = "Tạo phiên thanh toán thành công",
                    Data = new CheckoutSessionResponse
                    {
                        SessionId = session.Id,
                        SessionUrl = session.Url,
                        PublishableKey = _configuration["StripeSettings:PublishableKey"] ?? ""
                    }
                });
            }
            // ✅ BẮT LỖI InvalidOperationException MÀ StripeService NÉM RA
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning("User already has an active subscription: {Message}", ex.Message);
                // Trả về lỗi 400 (Bad Request) với thông điệp rõ ràng cho popup
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = ex.Message // Thông điệp này sẽ được hiển thị trên popup
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi tạo phiên thanh toán");
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Không thể tạo phiên thanh toán",
                    Errors = new List<string> { ex.Message }
                });
            }
        }
        // GET: api/payment/success
        [HttpGet("success")]
        public IActionResult PaymentSuccess([FromQuery] string session_id)
        {
            // Chuyển hướng đến action KetQuaThanhToan trong NangCapController
            return Redirect($"/NangCap/KetQuaThanhToan?success=true&session_id={session_id}");
        }

        // GET: api/payment/cancel
        [HttpGet("cancel")]
        public IActionResult PaymentCancel()
        {
            return Redirect("/NangCap/KetQuaThanhToan?success=false");
        }

        // POST: api/payment/webhook (Stripe webhook)
        [HttpPost("webhook")]
        public async Task<IActionResult> StripeWebhook()
        {
            try
            {
                var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();
                var stripeSignature = Request.Headers["Stripe-Signature"].ToString();

                await _stripeService.HandleWebhookAsync(json, stripeSignature);

                return Ok();
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        // GET: api/payment/config
        [HttpGet("config")]
        public IActionResult GetPublishableKey()
        {
            return Ok(new
            {
                publishableKey = _configuration["StripeSettings:PublishableKey"]
            });
        }

        // Helper methods
        private async Task<MovieWeb.Models.Entities.User?> GetUserAsync(int userId)
        {
            return await _subscriptionService.GetUserByIdAsync(userId);
        }

        private async Task<MovieWeb.Models.Entities.SubscriptionPlan?> GetPlanEntityAsync(int planId)
        {
            return await _subscriptionService.GetPlanEntityByIdAsync(planId);
        }
    }
}
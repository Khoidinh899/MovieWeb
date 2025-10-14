using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MovieWeb.Models.DTOs;
using MovieWeb.Services.Interfaces;
using System.Security.Claims;
using MovieWeb.Data;
using Microsoft.EntityFrameworkCore;

namespace MovieWeb.Controllers.API
{
    [Route("api/[controller]")]
    [ApiController]
    public class SubscriptionController : ControllerBase
    {
        private readonly ISubscriptionService _subscriptionService;
        private readonly IStripeService _stripeService;
        private readonly MovieWebDbContext _context; 

        public SubscriptionController(
            MovieWebDbContext context,
            ISubscriptionService subscriptionService,
            IStripeService stripeService)
        {
            _context = context;
            _subscriptionService = subscriptionService;
            _stripeService = stripeService;
        }

        // GET: api/subscription/plans
        [HttpGet("plans")]
        public async Task<IActionResult> GetAllPlans([FromQuery] string? type = null)
        {
            try
            {
                var plans = string.IsNullOrEmpty(type)
                    ? await _subscriptionService.GetAllPlansAsync()
                    : await _subscriptionService.GetPlansByTypeAsync(type);

                return Ok(new ApiResponse<List<SubscriptionPlanDto>>
                {
                    Success = true,
                    Data = plans
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = ex.Message
                });
            }
        }

        // GET: api/subscription/plans/{id}
        [HttpGet("plans/{id}")]
        public async Task<IActionResult> GetPlanById(int id)
        {
            try
            {
                var plan = await _subscriptionService.GetPlanByIdAsync(id);

                if (plan == null)
                    return NotFound(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Plan not found"
                    });

                return Ok(new ApiResponse<SubscriptionPlanDto>
                {
                    Success = true,
                    Data = plan
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = ex.Message
                });
            }
        }

        // GET: api/subscription/my-subscription
        [HttpGet("my-subscription")]
        [Authorize]
        public async Task<IActionResult> GetMySubscription()
        {
            try
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
                var subscription = await _subscriptionService.GetActiveSubscriptionAsync(userId);

                return Ok(new ApiResponse<UserSubscriptionDto?>
                {
                    Success = true,
                    Data = subscription
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = ex.Message
                });
            }
        }

        // GET: api/subscription/history
        [HttpGet("history")]
        [Authorize]
        public async Task<IActionResult> GetSubscriptionHistory()
        {
            try
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
                var history = await _subscriptionService.GetUserSubscriptionHistoryAsync(userId);

                return Ok(new ApiResponse<List<UserSubscriptionDto>>
                {
                    Success = true,
                    Data = history
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = ex.Message
                });
            }
        }

        // POST: api/subscription/cancel
        [HttpPost("cancel")]
        [Authorize]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> CancelSubscription([FromBody] CancelSubscriptionRequest request)
        {
            try
            {
                // Check authentication
                if (!User.Identity?.IsAuthenticated ?? true)
                {
                    return Unauthorized(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Bạn cần đăng nhập để thực hiện hành động này"
                    });
                }

                var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userIdClaim))
                {
                    return Unauthorized(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Không tìm thấy thông tin người dùng"
                    });
                }

                var userId = int.Parse(userIdClaim);

                // Verify ownership
                var subscription = await _subscriptionService.GetSubscriptionByIdAsync(request.SubscriptionId);

                if (subscription == null)
                {
                    return NotFound(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Không tìm thấy gói đăng ký"
                    });
                }

                if (subscription.UserId != userId)
                {
                    return StatusCode(403, new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Bạn không có quyền hủy gói đăng ký này"
                    });
                }

                var success = await _subscriptionService.CancelSubscriptionAsync(
                    request.SubscriptionId,
                    request.Reason,
                    request.CancelImmediately
                );

                if (!success)
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Không thể hủy gói đăng ký. Vui lòng thử lại sau."
                    });
                }

                // Cancel on Stripe if exists
                if (!string.IsNullOrEmpty(subscription.StripeSubscriptionId))
                {
                    await _stripeService.CancelSubscriptionAsync(
                        subscription.StripeSubscriptionId,
                        request.CancelImmediately
                    );
                }

                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "Đã hủy gói thành công! Bạn vẫn dùng được đến hết thời hạn. Mua gói mới sẽ được cộng dồn thêm thời gian."
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = "Có lỗi xảy ra: " + ex.Message
                });
            }
        }
        // GET: api/subscription/check-active
        [HttpGet("check-active")]
[Authorize] // Đảm bảo chỉ user đã đăng nhập mới gọi được
public async Task<IActionResult> CheckActiveSubscription()
{
    var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
    if (!int.TryParse(userIdString, out var userId))
    {
        return Unauthorized(new { success = false, message = "User not found" });
    }

    // ✅ SỬA LẠI CÂU TRUY VẤN - CHỈ TÌM GÓI CÓ STATUS LÀ "active"
    var activeSubscription = await _context.UserSubscriptions
        .Include(s => s.SubscriptionPlan) // Thêm Include để lấy được tên gói
        .FirstOrDefaultAsync(s => s.UserId == userId 
                               && s.EndDate > DateTime.Now 
                               && s.Status == "active");

    if (activeSubscription == null)
    {
        // Nếu không tìm thấy gói nào ACTIVE, trả về false để cho phép mua
        return Ok(new ApiResponse<object> 
        {
            Success = true,
            Data = new { hasActiveSubscription = false }
        });
    }

    // Nếu tìm thấy một gói ACTIVE, trả về true và thông tin để JS hiện popup
    return Ok(new ApiResponse<object>
    {
        Success = true,
        Data = new 
        {
            hasActiveSubscription = true,
            subscription = new 
            {
                planName = activeSubscription.SubscriptionPlan?.DisplayName ?? "hiện tại",
                daysRemaining = (activeSubscription.EndDate - DateTime.Now).Days
            }
        }
    });
}
        // GET: api/subscription/transactions
        [HttpGet("transactions")]
        [Authorize]
        public async Task<IActionResult> GetTransactions([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            try
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
                var transactions = await _subscriptionService.GetUserTransactionsAsync(userId, page, pageSize);

                return Ok(new ApiResponse<List<TransactionDto>>
                {
                    Success = true,
                    Data = transactions
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = ex.Message
                });
            }
        }

        // POST: api/subscription/verify-student
        [HttpPost("verify-student")]
        [Authorize]
        public async Task<IActionResult> SendStudentVerification([FromBody] StudentVerificationRequest request)
        {
            try
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

                if (!await _subscriptionService.IsStudentEmailValidAsync(request.StudentEmail))
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Email không hợp lệ. Vui lòng sử dụng email sinh viên (.edu, .edu.vn, .ac.vn)"
                    });

                var success = await _subscriptionService.SendStudentVerificationEmailAsync(userId, request.StudentEmail);

                if (!success)
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Failed to send verification email"
                    });

                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "Mã xác thực đã được gửi đến email sinh viên của bạn"
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = ex.Message
                });
            }
        }

        // POST: api/subscription/verify-student-code
        [HttpPost("verify-student-code")]
        [Authorize]
        public async Task<IActionResult> VerifyStudentCode([FromBody] VerifyStudentCodeRequest request)
        {
            try
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
                var success = await _subscriptionService.VerifyStudentEmailAsync(userId, request.VerificationCode);

                if (!success)
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Mã xác thực không đúng hoặc đã hết hạn"
                    });

                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "Xác thực sinh viên thành công! Bạn có thể mua gói Student với giá ưu đãi."
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = ex.Message
                });
            }
        }

        // GET: api/subscription/check-premium
        [HttpGet("check-premium")]
        [Authorize]
        public async Task<IActionResult> CheckPremiumStatus()
        {
            try
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
                var isPremium = await _subscriptionService.IsPremiumUserAsync(userId);

                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Data = new { isPremium }
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = ex.Message
                });
            }
        }
    }
}
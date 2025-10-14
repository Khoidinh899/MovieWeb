using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MovieWeb.Models.DTOs;
using MovieWeb.Services.Interfaces;

namespace MovieWeb.Controllers.API.Admin
{
    [Route("api/admin/[controller]")]
    [ApiController]
    [Authorize(Roles = "Admin")]
    public class SubscriptionController : ControllerBase
    {
        private readonly ISubscriptionService _subscriptionService;
        private readonly IStripeService _stripeService;

        public SubscriptionController(
            ISubscriptionService subscriptionService,
            IStripeService stripeService)
        {
            _subscriptionService = subscriptionService;
            _stripeService = stripeService;
        }

        // GET: api/admin/subscription/stats
        [HttpGet("stats")]
        public async Task<IActionResult> GetStats(
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null)
        {
            try
            {
                var stats = await _subscriptionService.GetRevenueStatsAsync(startDate, endDate);
                var totalActive = await _subscriptionService.GetTotalActiveSubscriptionsAsync();
                var totalPremium = await _subscriptionService.GetTotalPremiumUsersAsync();

                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Data = new
                    {
                        revenue = stats,
                        totalActiveSubscriptions = totalActive,
                        totalPremiumUsers = totalPremium
                    }
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

        // GET: api/admin/subscription/revenue-by-plan
        [HttpGet("revenue-by-plan")]
        public async Task<IActionResult> GetRevenueByPlan(
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null)
        {
            try
            {
                var revenue = await _subscriptionService.GetRevenueByPlanAsync(startDate, endDate);

                return Ok(new ApiResponse<List<PlanRevenueDto>>
                {
                    Success = true,
                    Data = revenue
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

        // GET: api/admin/subscription/revenue-trend
        [HttpGet("revenue-trend")]
        public async Task<IActionResult> GetRevenueTrend([FromQuery] int days = 30)
        {
            try
            {
                var trend = await _subscriptionService.GetRevenueTrendAsync(days);

                return Ok(new ApiResponse<List<RevenueTrendDto>>
                {
                    Success = true,
                    Data = trend
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

        // GET: api/admin/subscription/expiring
        [HttpGet("expiring")]
        public async Task<IActionResult> GetExpiringSubscriptions([FromQuery] int days = 7)
        {
            try
            {
                var subscriptions = await _subscriptionService.GetExpiringSubscriptionsAsync(days);

                return Ok(new ApiResponse<List<UserSubscriptionDto>>
                {
                    Success = true,
                    Data = subscriptions
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

        // POST: api/admin/subscription/sync-stripe-plans
        [HttpPost("sync-stripe-plans")]
        public async Task<IActionResult> SyncStripePlans()
        {
            try
            {
                var success = await _stripeService.SyncSubscriptionPlansToStripeAsync();

                if (!success)
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Failed to sync plans with Stripe"
                    });

                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "Plans synced successfully with Stripe"
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

        // GET: api/admin/subscription/all-subscriptions
        [HttpGet("all-subscriptions")]
        public async Task<IActionResult> GetAllSubscriptions(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? status = null)
        {
            try
            {
                var subscriptions = await _subscriptionService.GetAllSubscriptionsAsync(page, pageSize, status);

                return Ok(new ApiResponse<PaginatedResponse<UserSubscriptionDto>>
                {
                    Success = true,
                    Data = subscriptions
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

        // GET: api/admin/subscription/all-transactions
        [HttpGet("all-transactions")]
        public async Task<IActionResult> GetAllTransactions(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? status = null)
        {
            try
            {
                var transactions = await _subscriptionService.GetAllTransactionsAsync(page, pageSize, status);

                return Ok(new ApiResponse<PaginatedResponse<TransactionDto>>
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

        // POST: api/admin/subscription/{id}/extend
        [HttpPost("{id}/extend")]
        public async Task<IActionResult> ExtendSubscription(int id, [FromBody] ExtendSubscriptionRequest request)
        {
            try
            {
                var success = await _subscriptionService.ExtendSubscriptionAsync(id, request.Months);

                if (!success)
                    return NotFound(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Subscription not found"
                    });

                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = $"Subscription extended by {request.Months} months"
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

        // DELETE: api/admin/subscription/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> CancelSubscription(int id, [FromBody] AdminCancelRequest request)
        {
            try
            {
                var success = await _subscriptionService.CancelSubscriptionAsync(id, request.Reason, true);

                if (!success)
                    return NotFound(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Subscription not found"
                    });

                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "Subscription cancelled successfully"
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

        // POST: api/admin/subscription/send-reminders
        [HttpPost("send-reminders")]
        public async Task<IActionResult> SendExpiryReminders()
        {
            try
            {
                var subscriptions = await _subscriptionService.GetExpiringSubscriptionsAsync(7);
                
                foreach (var sub in subscriptions)
                {
                    await _subscriptionService.SendExpiryReminderAsync(sub.SubscriptionId);
                }

                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = $"Sent {subscriptions.Count} expiry reminders"
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

    public class ExtendSubscriptionRequest
    {
        public int Months { get; set; }
    }

    public class AdminCancelRequest
    {
        public string? Reason { get; set; }
    }
}
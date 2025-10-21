// Controllers/NotificationsController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MovieWeb.Models.Entities; // Cần cho Notification
using MovieWeb.Services.Interfaces; // ✅ DÙNG SERVICE
using System.Security.Claims;

namespace MovieWeb.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class NotificationsController : ControllerBase
    {
        // Bỏ _context, dùng _notificationService
        private readonly INotificationService _notificationService; 
        private readonly ILogger<NotificationsController> _logger;

        public NotificationsController(
            INotificationService notificationService, // ✅ Inject Service
            ILogger<NotificationsController> logger)
        {
            _notificationService = notificationService;
            _logger = logger;
        }

        // GET: api/notifications?type=payment|movie|all
        [HttpGet]
        public async Task<IActionResult> GetNotifications([FromQuery] string type = "all")
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == null)
                {
                    return Unauthorized(new { message = "Vui lòng đăng nhập" });
                }

                // ✅ Gọi Service
                var notifications = await _notificationService.GetNotificationsAsync(userId.Value, type, 20);
                
                // ✅ Vẫn gọi unreadCount để trả về cho JS (như code cũ của ông)
                var unreadCountDto = await _notificationService.GetUnreadCountAsync(userId.Value);

                return Ok(new
                {
                    success = true,
                    data = notifications, // Service đã trả về DTO, không cần map lại
                    unreadCount = unreadCountDto.Total 
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy danh sách notifications cho UserID {UserId}", GetCurrentUserId());
                return StatusCode(500, new { success = false, message = "Có lỗi xảy ra" });
            }
        }

        // GET: api/notifications/unread-count
        [HttpGet("unread-count")]
        public async Task<IActionResult> GetUnreadCount()
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == null)
                {
                    return Unauthorized(new { message = "Vui lòng đăng nhập" });
                }

                // ✅ Gọi Service
                var unreadCountDto = await _notificationService.GetUnreadCountAsync(userId.Value);

                return Ok(new
                {
                    success = true,
                    payment = unreadCountDto.Payment,
                    movie = unreadCountDto.Movie,
                    total = unreadCountDto.Total
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi đếm notifications chưa đọc cho UserID {UserId}", GetCurrentUserId());
                return StatusCode(500, new { success = false, message = "Có lỗi xảy ra" });
            }
        }

        // POST: api/notifications/{id}/mark-read
        [HttpPost("{id}/mark-read")]
        public async Task<IActionResult> MarkAsRead(int id)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == null)
                {
                    return Unauthorized(new { message = "Vui lòng đăng nhập" });
                }

                // ✅ Gọi Service
                var result = await _notificationService.MarkAsReadAsync(id, userId.Value);

                if (!result)
                {
                    return NotFound(new { success = false, message = "Không tìm thấy thông báo" });
                }

                return Ok(new { success = true, message = "Đã đánh dấu đã đọc" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi đánh dấu notification {NotificationId} đã đọc", id);
                return StatusCode(500, new { success = false, message = "Có lỗi xảy ra" });
            }
        }

        // POST: api/notifications/mark-all-read?type=payment|movie|all
        [HttpPost("mark-all-read")]
        public async Task<IActionResult> MarkAllAsRead([FromQuery] string type = "all")
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == null)
                {
                    return Unauthorized(new { message = "Vui lòng đăng nhập" });
                }

                // ✅ Gọi Service
                var count = await _notificationService.MarkAllAsReadAsync(userId.Value, type);

                return Ok(new
                {
                    success = true,
                    message = $"Đã đánh dấu {count} thông báo là đã đọc",
                    count = count
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi đánh dấu tất cả notifications đã đọc cho UserID {UserId}", GetCurrentUserId());
                return StatusCode(500, new { success = false, message = "Có lỗi xảy ra" });
            }
        }

        // DELETE: api/notifications/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteNotification(int id)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == null)
                {
                    return Unauthorized(new { message = "Vui lòng đăng nhập" });
                }

                // ✅ Gọi Service
                var result = await _notificationService.DeleteNotificationAsync(id, userId.Value);

                if (!result)
                {
                    return NotFound(new { success = false, message = "Không tìm thấy thông báo" });
                }

                return Ok(new { success = true, message = "Đã xóa thông báo" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi xóa notification {NotificationId}", id);
                return StatusCode(500, new { success = false, message = "Có lỗi xảy ra" });
            }
        }
        // Helper method: Lấy UserId từ Claims
        private int? GetCurrentUserId()
        {
            // Cách 1: Lấy từ ClaimTypes.NameIdentifier
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(userIdClaim, out int userId))
            {
                return userId;
            }

            // Cách 2: Lấy từ custom claim "UserId"
            var userIdClaim2 = User.FindFirst("UserId")?.Value;
            if (int.TryParse(userIdClaim2, out int userId2))
            {
                return userId2;
            }

            // Cách 3: Lấy từ claim "sub" (standard claim)
            var subClaim = User.FindFirst("sub")?.Value;
            if (int.TryParse(subClaim, out int userId3))
            {
                return userId3;
            }

            // Debug: Log tất cả claims
            var allClaims = string.Join(", ", User.Claims.Select(c => $"{c.Type}={c.Value}"));
            _logger.LogWarning("Cannot get UserId. All claims: {Claims}", allClaims);

            return null;
        }
    }
}
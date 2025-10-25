// Jobs/SendRealtimeNotificationJob.cs
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using MovieWeb.Data;
using MovieWeb.Hubs;

namespace MovieWeb.Jobs
{
    public class SendRealtimeNotificationJob
    {
        private readonly MovieWebDbContext _context;
        private readonly IHubContext<NotificationHub> _hubContext;
        private readonly ILogger<SendRealtimeNotificationJob> _logger;

        public SendRealtimeNotificationJob(
            MovieWebDbContext context,
            IHubContext<NotificationHub> hubContext,
            ILogger<SendRealtimeNotificationJob> logger)
        {
            _context = context;
            _hubContext = hubContext;
            _logger = logger;
        }

        /// <summary>
        /// Gửi TẤT CẢ notifications chưa đọc của 1 user qua SignalR
        /// </summary>
        public async Task Execute(int userId)
        {
            try
            {
                _logger.LogInformation("╔════════════════════════════════════════╗");
                _logger.LogInformation("║  SEND REALTIME NOTIFICATION JOB        ║");
                _logger.LogInformation("╚════════════════════════════════════════╝");
                _logger.LogInformation("🔔 Bắt đầu gửi notifications cho User {UserId}", userId);

                // Lấy tất cả notifications chưa đọc của user
                var notifications = await _context.Notifications
                    .Where(n => n.UserId == userId && n.IsRead == false)
                    .OrderByDescending(n => n.CreatedAt)
                    .ToListAsync();

                if (!notifications.Any())
                {
                    _logger.LogInformation("✅ Không có notification nào cần gửi cho User {UserId}", userId);
                    _logger.LogInformation("═══════════════════════════════════════════");
                    return;
                }

                _logger.LogInformation("📤 Tìm thấy {Count} notification(s) chưa đọc, chuẩn bị gửi...", notifications.Count);

                int successCount = 0;
                int failCount = 0;

                foreach (var notification in notifications)
                {
                    try
                    {
                        // Tạo object để gửi qua SignalR (khớp với JavaScript)
                        var notificationObject = new
                        {
                            notificationId = notification.NotificationId,
                            title = notification.Title,
                            content = notification.Content,
                            type = notification.Type,
                            url = notification.Url,
                            isRead = notification.IsRead,
                            createdAt = notification.CreatedAt
                        };

                        // ✅ GỬI QUA SIGNALR ĐỂN GROUP CỦA USER
                        await _hubContext.Clients
                            .Group($"User_{userId}")
                            .SendAsync("ReceiveNotification", notificationObject);

                        successCount++;
                        
                        _logger.LogInformation("✅ Sent notification {NotificationId} to User {UserId}", 
                            notification.NotificationId, userId);
                    }
                    catch (Exception ex)
                    {
                        failCount++;
                        _logger.LogError(ex, "❌ Lỗi khi gửi notification {NotificationId} cho User {UserId}", 
                            notification.NotificationId, userId);
                    }
                }

                _logger.LogInformation("╔════════════════════════════════════════╗");
                _logger.LogInformation("║  KẾT QUẢ: {SuccessCount} thành công, {FailCount} thất bại       ║", 
                    successCount, failCount);
                _logger.LogInformation("╚════════════════════════════════════════╝");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ LỖI NGHIÊM TRỌNG KHI CHẠY SendRealtimeNotificationJob cho User {UserId}", userId);
                throw;
            }
        }

        /// <summary>
        /// Gửi tất cả notifications chưa đọc của TẤT CẢ users (chạy manual hoặc schedule)
        /// </summary>
        public async Task ExecuteForAllUsers()
        {
            try
            {
                _logger.LogInformation("╔════════════════════════════════════════╗");
                _logger.LogInformation("║  SEND ALL REALTIME NOTIFICATIONS       ║");
                _logger.LogInformation("╚════════════════════════════════════════╝");

                // Lấy tất cả userId có notifications chưa đọc
                var userIds = await _context.Notifications
                    .Where(n => n.IsRead == false && n.UserId.HasValue && n.UserId.Value > 0)
                    .Select(n => n.UserId.Value) // ✅ Lấy .Value để convert từ int? → int
                    .Distinct()
                    .ToListAsync();

                if (!userIds.Any())
                {
                    _logger.LogInformation("✅ Không có notification nào cần gửi");
                    return;
                }

                _logger.LogInformation("🔍 Tìm thấy {Count} user(s) có notifications chưa đọc", userIds.Count);

                foreach (var userId in userIds)
                {
                    await Execute(userId); // ✅ Bây giờ userId đã là int, không phải int?
                }

                _logger.LogInformation("╔════════════════════════════════════════╗");
                _logger.LogInformation("║  HOÀN THÀNH GỬI CHO TẤT CẢ USERS       ║");
                _logger.LogInformation("╚════════════════════════════════════════╝");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ LỖI NGHIÊM TRỌNG KHI CHẠY ExecuteForAllUsers");
                throw;
            }
        }
    }
}
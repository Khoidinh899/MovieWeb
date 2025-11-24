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

        public async Task Execute(int userId)
        {
            try
            {
                // LOG: Tắt bớt log mở đầu cho đỡ rác console server
                // _logger.LogInformation("🔔 Bắt đầu gửi notifications cho User {UserId}", userId);

                // CHIẾN THUẬT MỚI: Chỉ lấy thông báo trong 40 giây gần nhất
                // Vì Job chạy 30s/lần, ta lấy dư ra 10s để tránh bị sót mạng lag
                var lookBackTime = DateTime.UtcNow.AddSeconds(-40);

                var notifications = await _context.Notifications
                    .Where(n => n.UserId == userId 
                                && n.IsRead == false 
                                && n.CreatedAt >= lookBackTime) // <--- CHỈ SỬA DÒNG NÀY
                    .OrderByDescending(n => n.CreatedAt)
                    .ToListAsync();

                if (!notifications.Any()) return; // Không có gì mới thì im lặng luôn

                foreach (var notification in notifications)
                {
                    try
                    {
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

                        await _hubContext.Clients
                            .Group($"User_{userId}")
                            .SendAsync("ReceiveNotification", notificationObject);
                        
                        // Log nhẹ 1 dòng thôi
                        _logger.LogInformation("✅ Đã bắn Realtime Noti ID: {Id} cho User {User}", notification.NotificationId, userId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError("❌ Lỗi gửi ID {Id}: {Msg}", notification.NotificationId, ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Lỗi Job Notification User {UserId}", userId);
            }
        }

        public async Task ExecuteForAllUsers()
        {
            try
            {
                // Tìm những user có thông báo MỚI (trong 40s đổ lại)
                var lookBackTime = DateTime.UtcNow.AddSeconds(-40);

                var userIds = await _context.Notifications
                    .Where(n => n.IsRead == false 
                                && n.UserId.HasValue 
                                && n.CreatedAt >= lookBackTime) // <--- Lọc ngay từ đầu để tối ưu
                    .Select(n => n.UserId.Value)
                    .Distinct()
                    .ToListAsync();

                if (!userIds.Any()) return;

                foreach (var userId in userIds)
                {
                    await Execute(userId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Lỗi Job All Users");
            }
        }
    }
}
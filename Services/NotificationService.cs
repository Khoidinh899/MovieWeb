// Services/NotificationService.cs
using MovieWeb.Data;
using MovieWeb.Models.Entities;
using MovieWeb.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace MovieWeb.Services
{
    public class NotificationService : INotificationService
    {
        private readonly MovieWebDbContext _context;
        private readonly ILogger<NotificationService> _logger;

        public NotificationService(MovieWebDbContext context, ILogger<NotificationService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<List<NotificationDto>> GetNotificationsAsync(int userId, string type = "all", int limit = 20)
        {
            try
            {
                _logger.LogInformation("GetNotificationsAsync: UserId={UserId}, Type={Type}, Limit={Limit}", userId, type, limit);

                IQueryable<Notification> query = _context.Notifications
                    .Where(n => n.UserId == userId);

                // Filter theo type - PHẢI filter trước Select
                if (type == "payment")
                {
                    query = query.Where(n =>
                        n.Type == "PaymentReminder" ||
                        n.Type == "PaymentSuccess" ||
                        n.Type == "SubscriptionCancelled");
                }
                else if (type == "movie")
                {
                    query = query.Where(n => n.Type == "MovieRequestSuccess");
                }

                var notifications = await query
                    .OrderByDescending(n => n.CreatedAt)
                    .Take(limit)
                    .Select(n => new NotificationDto
                    {
                        NotificationId = n.NotificationId,
                        Title = n.Title ?? string.Empty,
                        Content = n.Content ?? string.Empty,
                        Type = n.Type ?? string.Empty,
                        Url = n.Url,
                        IsRead = n.IsRead ?? false,
                        CreatedAt = n.CreatedAt ?? DateTime.UtcNow
                    })
                    .ToListAsync();

                _logger.LogInformation("GetNotificationsAsync: Found {Count} notifications for userId={UserId}", notifications.Count, userId);
                foreach (var notif in notifications)
                {
                    _logger.LogDebug("Notification: Id={Id}, Title={Title}, Type={Type}", notif.NotificationId, notif.Title, notif.Type);
                }
                return notifications;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetNotificationsAsync for userId={UserId}", userId);
                throw;
            }
        }

        public async Task<UnreadCountDto> GetUnreadCountAsync(int userId)
        {
            try
            {
                _logger.LogInformation("GetUnreadCountAsync: UserId={UserId}", userId);

                var paymentCount = await _context.Notifications
                    .Where(n => n.UserId == userId &&
                               (n.IsRead == false || n.IsRead == null) &&
                               (n.Type == "PaymentReminder" ||
                                n.Type == "PaymentSuccess" ||
                                n.Type == "SubscriptionCancelled"))
                    .CountAsync();

                var movieCount = await _context.Notifications
                    .Where(n => n.UserId == userId &&
                               (n.IsRead == false || n.IsRead == null) &&
                               n.Type == "MovieRequestSuccess")
                    .CountAsync();

                var total = paymentCount + movieCount;

                var result = new UnreadCountDto
                {
                    Payment = paymentCount,
                    Movie = movieCount,
                    Total = total
                };

                _logger.LogInformation("GetUnreadCountAsync: Payment={Payment}, Movie={Movie}, Total={Total}", 
                    paymentCount, movieCount, total);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetUnreadCountAsync");
                throw;
            }
        }

        public async Task<bool> MarkAsReadAsync(int notificationId, int userId)
        {
            try
            {
                _logger.LogInformation("MarkAsReadAsync: NotificationId={NotificationId}, UserId={UserId}", notificationId, userId);

                var notification = await _context.Notifications
                    .FirstOrDefaultAsync(n => n.NotificationId == notificationId && n.UserId == userId);

                if (notification == null)
                {
                    _logger.LogWarning("MarkAsReadAsync: Notification not found - NotificationId={NotificationId}, UserId={UserId}", 
                        notificationId, userId);
                    return false;
                }

                notification.IsRead = true;
                await _context.SaveChangesAsync();

                _logger.LogInformation("MarkAsReadAsync: Successfully marked notification as read");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in MarkAsReadAsync");
                throw;
            }
        }

        public async Task<int> MarkAllAsReadAsync(int userId, string type = "all")
        {
            try
            {
                _logger.LogInformation("MarkAllAsReadAsync: UserId={UserId}, Type={Type}", userId, type);

                IQueryable<Notification> query = _context.Notifications
                    .Where(n => n.UserId == userId && (n.IsRead == false || n.IsRead == null));

                if (type == "payment")
                {
                    query = query.Where(n =>
                        n.Type == "PaymentReminder" ||
                        n.Type == "PaymentSuccess" ||
                        n.Type == "SubscriptionCancelled");
                }
                else if (type == "movie")
                {
                    query = query.Where(n => n.Type == "MovieRequestSuccess");
                }

                var notifications = await query.ToListAsync();

                foreach (var notification in notifications)
                {
                    notification.IsRead = true;
                }

                var count = await _context.SaveChangesAsync();

                _logger.LogInformation("MarkAllAsReadAsync: Marked {Count} notifications as read", count);
                return count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in MarkAllAsReadAsync");
                throw;
            }
        }

        public async Task<bool> DeleteNotificationAsync(int notificationId, int userId)
        {
            try
            {
                _logger.LogInformation("DeleteNotificationAsync: NotificationId={NotificationId}, UserId={UserId}", notificationId, userId);

                var notification = await _context.Notifications
                    .FirstOrDefaultAsync(n => n.NotificationId == notificationId && n.UserId == userId);

                if (notification == null)
                {
                    _logger.LogWarning("DeleteNotificationAsync: Notification not found");
                    return false;
                }

                _context.Notifications.Remove(notification);
                await _context.SaveChangesAsync();

                _logger.LogInformation("DeleteNotificationAsync: Successfully deleted notification");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in DeleteNotificationAsync");
                throw;
            }
        }

        public async Task<int> DeleteAllNotificationsAsync(int userId, string type = "all")
        {
            try
            {
                _logger.LogInformation("DeleteAllNotificationsAsync: UserId={UserId}, Type={Type}", userId, type);

                IQueryable<Notification> query = _context.Notifications
                    .Where(n => n.UserId == userId);

                if (type == "payment")
                {
                    query = query.Where(n =>
                        n.Type == "PaymentReminder" ||
                        n.Type == "PaymentSuccess" ||
                        n.Type == "SubscriptionCancelled");
                }
                else if (type == "movie")
                {
                    query = query.Where(n => n.Type == "MovieRequestSuccess");
                }

                var notifications = await query.ToListAsync();

                _context.Notifications.RemoveRange(notifications);
                var count = await _context.SaveChangesAsync();

                _logger.LogInformation("DeleteAllNotificationsAsync: Deleted {Count} notifications", count);
                return count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in DeleteAllNotificationsAsync");
                throw;
            }
        }

        public async Task<Notification> CreateNotificationAsync(int userId, string title, string content, 
            string type, string? url = null)
        {
            try
            {
                _logger.LogInformation("CreateNotificationAsync: UserId={UserId}, Type={Type}, Title={Title}", userId, type, title);

                var notification = new Notification
                {
                    UserId = userId,
                    Title = title,
                    Content = content,
                    Type = type,
                    Url = url,
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Notifications.Add(notification);
                await _context.SaveChangesAsync();

                _logger.LogInformation("CreateNotificationAsync: Successfully created notification - NotificationId={NotificationId}", 
                    notification.NotificationId);

                return notification;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in CreateNotificationAsync");
                throw;
            }
        }
        public async Task CreatePaymentReminderAsync(int userId)
        {
            try
            {
                var today = DateTime.UtcNow.Date;

                // 1. Check xem hôm nay đã gửi cho user này chưa (để tránh retry bị trùng)
                var existing = await _context.Notifications
                    .AnyAsync(n => n.UserId == userId && 
                                   n.Type == "PaymentReminder" && 
                                   n.CreatedAt.HasValue && 
                                   n.CreatedAt.Value.Date == today);
                
                if (existing)
                {
                    _logger.LogWarning("[Hangfire Job] Đã gửi reminder cho User {UserId} hôm nay rồi, bỏ qua.", userId);
                    return;
                }

                // 2. Lấy thông tin user
                var user = await _context.Users
                    .AsNoTracking() // Tăng performance vì chỉ đọc
                    .FirstOrDefaultAsync(u => u.Id == userId);
                
                if (user == null || user.SubscriptionEndDate == null || user.SubscriptionType == "free")
                {
                    _logger.LogWarning("[Hangfire Job] User {UserId} không hợp lệ để gửi reminder, bỏ qua.", userId);
                    return;
                }

                // 3. Tạo 1 notification
                var notification = new Notification
                {
                    UserId = userId,
                    Title = "⏰ Thông báo gia hạn gói", // ✅ Ông có thể sửa lại title ở đây
                    Content = $"Gói {user.SubscriptionType} của bạn sẽ hết hạn vào {user.SubscriptionEndDate.Value:dd/MM/yyyy}. Gia hạn ngay!",
                    Type = "PaymentReminder",
                    Url = "/nang-cap",
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow // Luôn dùng UTC cho server
                };

                _context.Notifications.Add(notification);
                await _context.SaveChangesAsync();
                
                _logger.LogInformation("[Hangfire Job] Đã tạo PaymentReminder cho User {UserId} thành công.", userId);

                // 4. (NẾU DÙNG SIGNALR SAU NÀY)
                // Ông sẽ gọi push real-time ở đây
                // var unreadCount = await GetUnreadCountAsync(userId);
                // await _hubContext.Clients.User(userId.ToString())
                //    .SendAsync("ReceiveNewNotification", unreadCount);
            }
            catch (Exception ex)
            {
                 _logger.LogError(ex, "[Hangfire Job] Lỗi khi tạo PaymentReminder cho User {UserId}", userId);
                 throw; // Ném lỗi để Hangfire retry job này
            }
        }
        public async Task<List<Notification>> CreateNotificationsAsync(List<int> userIds, string title, 
            string content, string type, string? url = null)
        {
            try
            {
                _logger.LogInformation("CreateNotificationsAsync: UserCount={UserCount}, Type={Type}", userIds.Count, type);

                var notifications = userIds.Select(userId => new Notification
                {
                    UserId = userId,
                    Title = title,
                    Content = content,
                    Type = type,
                    Url = url,
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow
                }).ToList();

                _context.Notifications.AddRange(notifications);
                await _context.SaveChangesAsync();

                _logger.LogInformation("CreateNotificationsAsync: Successfully created {Count} notifications", notifications.Count);

                return notifications;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in CreateNotificationsAsync");
                throw;
            }
        }
    }
}
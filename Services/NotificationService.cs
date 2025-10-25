// Services/NotificationService.cs - FIXED VERSION
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

        public async Task CreateMovieRequestCompletedAsync(int userId, int movieId, string movieName)
        {
            try
            {
                var movieSlug = await _context.Movies
                    .Where(m => m.MovieId == movieId)
                    .Select(m => m.Slug)
                    .FirstOrDefaultAsync();

                if (movieSlug == null)
                {
                    _logger.LogWarning("Movie (ID: {MovieId}) not found for User (ID: {UserId})", movieId, userId);
                    return;
                }

                var notification = new Notification
                {
                    UserId = userId,
                    Title = "Phim yêu cầu đã có sẵn! 🎬",
                    Content = $"Phim '{movieName}' bạn yêu cầu đã được thêm vào hệ thống. Xem ngay!",
                    Type = "MovieRequestSuccess",
                    Url = $"/phim/{movieSlug}",
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Notifications.Add(notification);
                await _context.SaveChangesAsync();

                _logger.LogInformation("✅ Created MovieRequestSuccess notification for User {UserId}", userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error creating notification for User {UserId}", userId);
            }
        }

        public async Task<List<NotificationDto>> GetNotificationsAsync(int userId, string type = "all", int limit = 20)
        {
            try
            {
                _logger.LogInformation("GetNotificationsAsync: UserId={UserId}, Type={Type}", userId, type);

                IQueryable<Notification> query = _context.Notifications
                    .AsNoTracking()
                    .Where(n => n.UserId == userId);

                // ========== FIX: FILTER TẤT CẢ CÁC LOẠI MOVIE NOTIFICATIONS ==========
                if (type == "payment")
                {
                    query = query.Where(n =>
                        n.Type == "PaymentReminder" ||
                        n.Type == "PaymentSuccess" ||
                        n.Type == "SubscriptionCancelled");
                }
                else if (type == "movie")
                {
                    // ✅ FIX: Lấy TẤT CẢ các loại movie notifications
                    query = query.Where(n =>
                        n.Type == "MovieRequestSuccess" ||
                        n.Type == "MovieUpdate" ||
                        n.Type == "movie_request_completed"); // Thêm các type khác nếu có
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

                _logger.LogInformation("✅ Found {Count} notifications for User {UserId}", notifications.Count, userId);
                return notifications;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error in GetNotificationsAsync for User {UserId}", userId);
                throw;
            }
        }

        public async Task<UnreadCountDto> GetUnreadCountAsync(int userId)
        {
            try
            {
                var paymentCount = await _context.Notifications
                    .Where(n => n.UserId == userId &&
                               (n.IsRead == false || n.IsRead == null) &&
                               (n.Type == "PaymentReminder" ||
                                n.Type == "PaymentSuccess" ||
                                n.Type == "SubscriptionCancelled"))
                    .CountAsync();

                // ✅ FIX: Đếm TẤT CẢ các loại movie notifications
                var movieCount = await _context.Notifications
                    .Where(n => n.UserId == userId &&
                               (n.IsRead == false || n.IsRead == null) &&
                               (n.Type == "MovieRequestSuccess" ||
                                n.Type == "MovieUpdate" ||
                                n.Type == "movie_request_completed"))
                    .CountAsync();

                var total = paymentCount + movieCount;

                return new UnreadCountDto
                {
                    Payment = paymentCount,
                    Movie = movieCount,
                    Total = total
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error in GetUnreadCountAsync");
                throw;
            }
        }

        public async Task<bool> MarkAsReadAsync(int notificationId, int userId)
        {
            try
            {
                var notification = await _context.Notifications
                    .FirstOrDefaultAsync(n => n.NotificationId == notificationId && n.UserId == userId);

                if (notification == null)
                {
                    _logger.LogWarning("Notification {NotificationId} not found for User {UserId}", notificationId, userId);
                    return false;
                }

                notification.IsRead = true;
                await _context.SaveChangesAsync();

                _logger.LogInformation("✅ Marked notification {NotificationId} as read", notificationId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error in MarkAsReadAsync");
                throw;
            }
        }

        public async Task<int> MarkAllAsReadAsync(int userId, string type = "all")
        {
            try
            {
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
                    // ✅ FIX: Mark all movie types
                    query = query.Where(n =>
                        n.Type == "MovieRequestSuccess" ||
                        n.Type == "MovieUpdate" ||
                        n.Type == "movie_request_completed");
                }

                var notifications = await query.ToListAsync();

                foreach (var notification in notifications)
                {
                    notification.IsRead = true;
                }

                var count = await _context.SaveChangesAsync();
                _logger.LogInformation("✅ Marked {Count} notifications as read", count);
                return count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error in MarkAllAsReadAsync");
                throw;
            }
        }

        public async Task<bool> DeleteNotificationAsync(int notificationId, int userId)
        {
            try
            {
                var notification = await _context.Notifications
                    .FirstOrDefaultAsync(n => n.NotificationId == notificationId && n.UserId == userId);

                if (notification == null) return false;

                _context.Notifications.Remove(notification);
                await _context.SaveChangesAsync();

                _logger.LogInformation("✅ Deleted notification {NotificationId}", notificationId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error in DeleteNotificationAsync");
                throw;
            }
        }

        public async Task<int> DeleteAllNotificationsAsync(int userId, string type = "all")
        {
            try
            {
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
                    query = query.Where(n =>
                        n.Type == "MovieRequestSuccess" ||
                        n.Type == "MovieUpdate" ||
                        n.Type == "movie_request_completed");
                }

                var notifications = await query.ToListAsync();
                _context.Notifications.RemoveRange(notifications);
                var count = await _context.SaveChangesAsync();

                _logger.LogInformation("✅ Deleted {Count} notifications", count);
                return count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error in DeleteAllNotificationsAsync");
                throw;
            }
        }

        public async Task<Notification> CreateNotificationAsync(int userId, string title, string content,
            string type, string? url = null)
        {
            try
            {
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

                _logger.LogInformation("✅ Created notification {NotificationId}", notification.NotificationId);
                return notification;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error in CreateNotificationAsync");
                throw;
            }
        }

        public async Task CreatePaymentReminderAsync(int userId)
        {
            try
            {
                var today = DateTime.UtcNow.Date;

                var existing = await _context.Notifications
                    .AnyAsync(n => n.UserId == userId &&
                                   n.Type == "PaymentReminder" &&
                                   n.CreatedAt.HasValue &&
                                   n.CreatedAt.Value.Date == today);

                if (existing)
                {
                    _logger.LogWarning("PaymentReminder already sent to User {UserId} today", userId);
                    return;
                }

                var user = await _context.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.Id == userId);

                if (user == null || user.SubscriptionEndDate == null || user.SubscriptionType == "free")
                {
                    _logger.LogWarning("User {UserId} invalid for reminder", userId);
                    return;
                }

                var notification = new Notification
                {
                    UserId = userId,
                    Title = "⏰ Thông báo gia hạn gói",
                    Content = $"Gói {user.SubscriptionType} của bạn sẽ hết hạn vào {user.SubscriptionEndDate.Value:dd/MM/yyyy}. Gia hạn ngay!",
                    Type = "PaymentReminder",
                    Url = "/nang-cap",
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Notifications.Add(notification);
                await _context.SaveChangesAsync();

                _logger.LogInformation("✅ Created PaymentReminder for User {UserId}", userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error creating PaymentReminder for User {UserId}", userId);
                throw;
            }
        }

        /// <summary>
        /// Tạo thông báo thanh toán thành công
        /// </summary>
        public async Task CreatePaymentSuccessNotificationAsync(
            int userId,
            string subscriptionType,
            DateTime subscriptionEndDate,
            decimal amountVND)
        {
            try
            {
                var notification = new Notification
                {
                    UserId = userId,
                    Title = "✅ Thanh toán thành công!",
                    Content = $"Bạn đã nâng cấp lên gói {subscriptionType} thành công. Có hiệu lực đến {subscriptionEndDate:dd/MM/yyyy}. Cảm ơn bạn đã ủng hộ MoonPhim!",
                    Type = "PaymentSuccess",
                    Url = "/user/profile", // Hoặc "/nang-cap"
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Notifications.Add(notification);
                await _context.SaveChangesAsync();

                _logger.LogInformation("✅ Created PaymentSuccess notification for User {UserId}", userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error creating PaymentSuccess notification for User {UserId}", userId);
            }
        }
        public async Task CreateCancelSubscriptionNotificationAsync(
            int userId,
            string planType,
            DateTime endDate,
            int daysRemaining)
        {
            try
            {
                var planDisplay = planType.ToLower() switch
                {
                    "premium" => "Premium",
                    "student" => "Student",
                    _ => planType
                };

                var notification = new Notification
                {
                    UserId = userId,
                    Title = "⚠️ Gói đăng ký đã được hủy",
                    Content = $"Gói {planDisplay} của bạn đã được hủy thành công. " +
                             $"Bạn vẫn có thể sử dụng đầy đủ tính năng đến hết ngày {endDate:dd/MM/yyyy} " +
                             $"(còn {daysRemaining} ngày). " +
                             $"Sau đó tài khoản sẽ tự động chuyển về gói Free.",
                    Type = "CancelSubscription",
                    Url = "/user/profile",
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Notifications.Add(notification);
                await _context.SaveChangesAsync();

                _logger.LogInformation(
                    "✅ Created cancel subscription notification for User {UserId}, Plan: {PlanType}",
                    userId, planType
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "❌ Error creating cancel subscription notification for User {UserId}",
                    userId
                );
            }
        }
        /// <summary>
        /// Tạo thông báo khi user hủy gói
        /// </summary>
        public async Task CreateSubscriptionCancelledNotificationAsync(
            int userId,
            string planName,
            DateTime endDate,
            string? reason = null)
        {
            try
            {
                var notification = new Notification
                {
                    UserId = userId,
                    Title = "⚠️ Đã hủy gói đăng ký",
                    Content = string.IsNullOrEmpty(reason)
                        ? $"Bạn đã hủy gói {planName}. Gói vẫn có hiệu lực đến {endDate:dd/MM/yyyy}. Bạn có thể mua lại bất cứ lúc nào!"
                        : $"Bạn đã hủy gói {planName} với lý do: {reason}. Gói vẫn có hiệu lực đến {endDate:dd/MM/yyyy}.",
                    Type = "SubscriptionCancelled",
                    Url = "/nang-cap",
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Notifications.Add(notification);
                await _context.SaveChangesAsync();

                _logger.LogInformation("✅ Created SubscriptionCancelled notification for User {UserId}", userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error creating SubscriptionCancelled notification for User {UserId}", userId);
            }
        }
        public async Task<List<Notification>> CreateNotificationsAsync(List<int> userIds, string title,
            string content, string type, string? url = null)
        {
            try
            {
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

                _logger.LogInformation("✅ Created {Count} notifications", notifications.Count);
                return notifications;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error in CreateNotificationsAsync");
                throw;
            }
        }
    }
}
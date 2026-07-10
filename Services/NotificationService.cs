// Services/NotificationService.cs - FIXED TIMEZONE VERSION
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
        private readonly IFcmNotificationService _fcmService;

        public NotificationService(
            MovieWebDbContext context, 
            ILogger<NotificationService> logger,
            IFcmNotificationService fcmService)
        {
            _context = context;
            _logger = logger;
            _fcmService = fcmService;
        }

        // ========== CREATE NOTIFICATION FOR SINGLE USER ==========
        public async Task<Notification> CreateNotificationAsync(
            int userId, 
            string title, 
            string content,
            string type, 
            string? url = null)
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
                    CreatedAt = DateTime.Now // ✅ ĐỔI: UTC -> Local Time (VN +7)
                };

                _context.Notifications.Add(notification);
                await _context.SaveChangesAsync();

                _logger.LogInformation("✅ Created notification {NotificationId} for User {UserId}", 
                    notification.NotificationId, userId);

                // Send FCM push notification
                try
                {
                    var user = await _context.Users.FindAsync(userId);
                    if (user != null && !string.IsNullOrEmpty(user.FcmToken))
                    {
                        bool isEnabled = true;
                        if (type == "PaymentReminder" || type == "PaymentSuccess" || type == "SubscriptionCancelled" || type == "CancelSubscription")
                        {
                            isEnabled = user.NotifyPayment;
                        }
                        else if (type == "MovieRequestSuccess" || type == "MovieUpdate" || type == "movie_request_completed" || type == "CommentReply")
                        {
                            isEnabled = user.NotifyMovie;
                        }
                        else
                        {
                            isEnabled = user.NotifySystem;
                        }

                        if (isEnabled)
                        {
                            await _fcmService.SendPushNotificationAsync(user.FcmToken, title, content, type, url);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error sending push notification in CreateNotificationAsync for User {UserId}", userId);
                }

                return notification;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error in CreateNotificationAsync for User {UserId}", userId);
                throw;
            }
        }

        // ========== CREATE NOTIFICATIONS FOR MULTIPLE USERS ==========
        public async Task<List<Notification>> CreateNotificationsAsync(
            List<int> userIds, 
            string title,
            string content, 
            string type, 
            string? url = null)
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
                    CreatedAt = DateTime.Now // ✅ ĐỔI: UTC -> Local Time
                }).ToList();

                _context.Notifications.AddRange(notifications);
                await _context.SaveChangesAsync();

                _logger.LogInformation("✅ Created {Count} notifications", notifications.Count);

                // Send FCM push notifications for all users
                foreach (var userId in userIds)
                {
                    try
                    {
                        var user = await _context.Users.FindAsync(userId);
                        if (user != null && !string.IsNullOrEmpty(user.FcmToken))
                        {
                            bool isEnabled = true;
                            if (type == "PaymentReminder" || type == "PaymentSuccess" || type == "SubscriptionCancelled" || type == "CancelSubscription")
                            {
                                isEnabled = user.NotifyPayment;
                            }
                            else if (type == "MovieRequestSuccess" || type == "MovieUpdate" || type == "movie_request_completed" || type == "CommentReply")
                            {
                                isEnabled = user.NotifyMovie;
                            }
                            else
                            {
                                isEnabled = user.NotifySystem;
                            }

                            if (isEnabled)
                            {
                                await _fcmService.SendPushNotificationAsync(user.FcmToken, title, content, type, url);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error sending push notification in CreateNotificationsAsync for User {UserId}", userId);
                    }
                }

                return notifications;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error in CreateNotificationsAsync");
                throw;
            }
        }

        // ========== GET NOTIFICATIONS ==========
        public async Task<List<NotificationDto>> GetNotificationsAsync(int userId, string type = "all", int limit = 20)
        {
            try
            {
                _logger.LogInformation("GetNotificationsAsync: UserId={UserId}, Type={Type}", userId, type);

                IQueryable<Notification> query = _context.Notifications
                    .AsNoTracking()
                    .Where(n => n.UserId == userId);

                if (type == "payment")
                {
                    query = query.Where(n =>
                        n.Type == "PaymentReminder" ||
                        n.Type == "PaymentSuccess" ||
                        n.Type == "SubscriptionCancelled" ||
                        n.Type == "CancelSubscription");
                }
                else if (type == "movie")
                {
                    query = query.Where(n =>
                        n.Type == "MovieRequestSuccess" ||
                        n.Type == "MovieUpdate" ||
                        n.Type == "movie_request_completed" ||
                        n.Type == "CommentReply");
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
                        CreatedAt = n.CreatedAt ?? DateTime.Now // ✅ ĐỔI: UTC -> Now
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

        // ========== GET UNREAD COUNT ==========
        public async Task<UnreadCountDto> GetUnreadCountAsync(int userId)
        {
            try
            {
                var paymentCount = await _context.Notifications
                    .Where(n => n.UserId == userId &&
                               (n.IsRead == false || n.IsRead == null) &&
                               (n.Type == "PaymentReminder" ||
                                n.Type == "PaymentSuccess" ||
                                n.Type == "SubscriptionCancelled" ||
                                n.Type == "CancelSubscription"))
                    .CountAsync();

                var movieCount = await _context.Notifications
                    .Where(n => n.UserId == userId &&
                               (n.IsRead == false || n.IsRead == null) &&
                               (n.Type == "MovieRequestSuccess" ||
                                n.Type == "MovieUpdate" ||
                                n.Type == "movie_request_completed" ||
                                n.Type == "CommentReply"))
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

        // ========== MARK AS READ ==========
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

        // ========== MARK ALL AS READ ==========
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
                        n.Type == "SubscriptionCancelled" ||
                        n.Type == "CancelSubscription");
                }
                else if (type == "movie")
                {
                    query = query.Where(n =>
                        n.Type == "MovieRequestSuccess" ||
                        n.Type == "MovieUpdate" ||
                        n.Type == "movie_request_completed" ||
                        n.Type == "CommentReply");
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

        // ========== DELETE NOTIFICATION ==========
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

        // ========== DELETE ALL NOTIFICATIONS ==========
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
                        n.Type == "SubscriptionCancelled" ||
                        n.Type == "CancelSubscription");
                }
                else if (type == "movie")
                {
                    query = query.Where(n =>
                        n.Type == "MovieRequestSuccess" ||
                        n.Type == "MovieUpdate" ||
                        n.Type == "movie_request_completed" ||
                        n.Type == "CommentReply");
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

        // ========== SPECIALIZED NOTIFICATION CREATORS ==========

        /// <summary>
        /// Tạo notification khi phim user yêu cầu đã có
        /// </summary>
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

                await CreateNotificationAsync(
                    userId,
                    "Phim yêu cầu đã có sẵn! 🎬",
                    $"Phim '{movieName}' bạn yêu cầu đã được thêm vào hệ thống. Xem ngay!",
                    "MovieRequestSuccess",
                    $"/phim/{movieSlug}"
                );

                _logger.LogInformation("✅ Created MovieRequestSuccess notification for User {UserId}", userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error creating MovieRequestSuccess notification for User {UserId}", userId);
            }
        }

        /// <summary>
        /// Tạo notification nhắc nhở gia hạn
        /// </summary>
        public async Task CreatePaymentReminderAsync(int userId)
        {
            try
            {
                var today = DateTime.Now.Date; // ✅ ĐỔI: UTC -> Local Time

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

                await CreateNotificationAsync(
                    userId,
                    "⏰ Thông báo gia hạn gói",
                    $"Gói {user.SubscriptionType} của bạn sẽ hết hạn vào {user.SubscriptionEndDate.Value:dd/MM/yyyy}. Gia hạn ngay!",
                    "PaymentReminder",
                    "/nang-cap"
                );

                _logger.LogInformation("✅ Created PaymentReminder for User {UserId}", userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error creating PaymentReminder for User {UserId}", userId);
                throw;
            }
        }

        /// <summary>
        /// Tạo notification thanh toán thành công
        /// </summary>
        public async Task CreatePaymentSuccessNotificationAsync(
            int userId,
            string subscriptionType,
            DateTime subscriptionEndDate,
            decimal amountVND)
        {
            try
            {
                await CreateNotificationAsync(
                    userId,
                    "✅ Thanh toán thành công!",
                    $"Bạn đã nâng cấp lên gói {subscriptionType} thành công. Có hiệu lực đến {subscriptionEndDate:dd/MM/yyyy}. Cảm ơn bạn đã ủng hộ MoonPhim!",
                    "PaymentSuccess",
                    "/user/payment-history"
                );

                _logger.LogInformation("✅ Created PaymentSuccess notification for User {UserId}", userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error creating PaymentSuccess notification for User {UserId}", userId);
            }
        }

        /// <summary>
        /// Tạo notification khi user hủy gói
        /// </summary>
        public async Task CreateSubscriptionCancelledNotificationAsync(
            int userId,
            string planName,
            DateTime endDate,
            string? reason = null)
        {
            try
            {
                var content = string.IsNullOrEmpty(reason)
                    ? $"Bạn đã hủy gói {planName}. Gói vẫn có hiệu lực đến {endDate:dd/MM/yyyy}. Bạn có thể mua lại bất cứ lúc nào!"
                    : $"Bạn đã hủy gói {planName} với lý do: {reason}. Gói vẫn có hiệu lực đến {endDate:dd/MM/yyyy}.";

                await CreateNotificationAsync(
                    userId,
                    "⚠️ Đã hủy gói đăng ký",
                    content,
                    "CancelSubscription",
                    "/nang-cap"
                );

                _logger.LogInformation("✅ Created CancelSubscription notification for User {UserId}", userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error creating CancelSubscription notification for User {UserId}", userId);
            }
        }
    }
}
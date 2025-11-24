// Services/Interfaces/INotificationService.cs - FIXED VERSION
using MovieWeb.Models.Entities;

namespace MovieWeb.Services.Interfaces
{
    public interface INotificationService
    {
        /// <summary>
        /// Lấy danh sách notifications của user
        /// </summary>
        Task<List<NotificationDto>> GetNotificationsAsync(int userId, string type = "all", int limit = 20);

        /// <summary>
        /// Đếm số notifications chưa đọc
        /// </summary>
        Task<UnreadCountDto> GetUnreadCountAsync(int userId);

        /// <summary>
        /// Đánh dấu 1 notification đã đọc
        /// </summary>
        Task<bool> MarkAsReadAsync(int notificationId, int userId);

        /// <summary>
        /// Đánh dấu tất cả notifications đã đọc
        /// </summary>
        Task<int> MarkAllAsReadAsync(int userId, string type = "all");

        /// <summary>
        /// Xóa 1 notification
        /// </summary>
        Task<bool> DeleteNotificationAsync(int notificationId, int userId);

        /// <summary>
        /// Xóa tất cả notifications
        /// </summary>
        Task<int> DeleteAllNotificationsAsync(int userId, string type = "all");

        // ✅ THÊM: Single user notification
        /// <summary>
        /// Tạo notification cho 1 user (CommentReply, MovieRequest, etc.)
        /// </summary>
        Task<Notification> CreateNotificationAsync(
            int userId, 
            string title, 
            string content, 
            string type, 
            string? url = null);

        /// <summary>
        /// Tạo multiple notifications (cho nhiều users cùng lúc)
        /// </summary>
        Task<List<Notification>> CreateNotificationsAsync(
            List<int> userIds, 
            string title,
            string content, 
            string type, 
            string? url = null);

        /// <summary>
        /// (Hangfire) Tạo notification nhắc nhở gia hạn cho 1 user
        /// </summary>
        Task CreatePaymentReminderAsync(int userId);

        /// <summary>
        /// (Hangfire) Tạo notification khi phim user yêu cầu đã có
        /// </summary>
        Task CreateMovieRequestCompletedAsync(int userId, int movieId, string movieName);

        /// <summary>
        /// Tạo notification thanh toán thành công
        /// </summary>
        Task CreatePaymentSuccessNotificationAsync(
            int userId, 
            string subscriptionType, 
            DateTime subscriptionEndDate, 
            decimal amountVND);

        /// <summary>
        /// Tạo notification khi hủy gói
        /// </summary>
        Task CreateSubscriptionCancelledNotificationAsync(
            int userId, 
            string planName, 
            DateTime endDate, 
            string? reason = null);
    }

    // ===== DTOs =====
    public class NotificationDto
    {
        public int NotificationId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string? Url { get; set; }
        public bool? IsRead { get; set; }
        public DateTime? CreatedAt { get; set; }
    }

    public class UnreadCountDto
    {
        public int Payment { get; set; }
        public int Movie { get; set; }
        public int Total { get; set; }
    }
}
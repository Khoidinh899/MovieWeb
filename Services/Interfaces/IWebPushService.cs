using System.Collections.Generic;
using System.Threading.Tasks;

namespace MovieWeb.Services.Interfaces
{
    /// <summary>Nội dung một thông báo đẩy trên trình duyệt/PWA (Web Push, VAPID).</summary>
    public class WebPushMessage
    {
        public string Title { get; set; } = "MoonPhim";
        public string Body { get; set; } = "";
        public string? Url { get; set; }
        public string? Tag { get; set; }
    }

    public interface IWebPushService
    {
        /// <summary>Khóa công khai VAPID (base64url) cho pushManager.subscribe.</summary>
        Task<string> GetPublicKeyAsync();

        Task SaveSubscriptionAsync(int userId, string endpoint, string p256dh, string auth, string? userAgent);

        Task RemoveSubscriptionAsync(int userId, string endpoint);

        /// <summary>User nào trong danh sách có ít nhất một thiết bị đã bật thông báo trình duyệt.</summary>
        Task<List<int>> FilterUsersWithSubscriptionsAsync(IEnumerable<int> userIds);

        /// <summary>Gửi tới mọi thiết bị của các user (chạy trong job Hangfire). Trả về số lượt gửi thành công.</summary>
        Task<int> SendToUsersAsync(List<int> userIds, WebPushMessage message);
    }
}

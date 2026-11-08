using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MovieWeb.Data;
using MovieWeb.Models.Entities;
using MovieWeb.Services.Interfaces;
using WebPush;
using DbWebPushSubscription = MovieWeb.Models.Entities.WebPushSubscription;

namespace MovieWeb.Services
{
    /// <summary>
    /// Thông báo đẩy trên trình duyệt/PWA theo chuẩn Web Push (VAPID). Khóa VAPID lấy từ cấu hình
    /// (WebPush:PublicKey/PrivateKey) nếu có, không thì tự tạo một lần và lưu ở SiteSettings — sống qua các lần deploy.
    /// </summary>
    public class WebPushService : IWebPushService
    {
        private const string VapidSettingKey = "webpush.vapid";
        private const int MaxBodyLength = 240;
        private static readonly SemaphoreSlim KeyLock = new(1, 1);

        private readonly MovieWebDbContext _context;
        private readonly ISiteSettingsService _siteSettings;
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;
        private readonly ILogger<WebPushService> _logger;

        public WebPushService(
            MovieWebDbContext context,
            ISiteSettingsService siteSettings,
            IConfiguration configuration,
            HttpClient httpClient,
            ILogger<WebPushService> logger)
        {
            _context = context;
            _siteSettings = siteSettings;
            _configuration = configuration;
            _httpClient = httpClient;
            _logger = logger;
        }

        private sealed class VapidKeys
        {
            public string PublicKey { get; set; } = "";
            public string PrivateKey { get; set; } = "";
        }

        private async Task<VapidKeys> GetKeysAsync()
        {
            var configPublic = _configuration["WebPush:PublicKey"];
            var configPrivate = _configuration["WebPush:PrivateKey"];
            if (!string.IsNullOrWhiteSpace(configPublic) && !string.IsNullOrWhiteSpace(configPrivate))
                return new VapidKeys { PublicKey = configPublic, PrivateKey = configPrivate };

            var stored = await _siteSettings.GetAsync<VapidKeys>(VapidSettingKey);
            if (!string.IsNullOrEmpty(stored?.PublicKey) && !string.IsNullOrEmpty(stored.PrivateKey)) return stored;

            await KeyLock.WaitAsync();
            try
            {
                stored = await _siteSettings.GetAsync<VapidKeys>(VapidSettingKey);
                if (!string.IsNullOrEmpty(stored?.PublicKey) && !string.IsNullOrEmpty(stored.PrivateKey)) return stored;

                var generated = VapidHelper.GenerateVapidKeys();
                var keys = new VapidKeys { PublicKey = generated.PublicKey, PrivateKey = generated.PrivateKey };
                await _siteSettings.SetAsync(VapidSettingKey, keys);
                _logger.LogInformation("Generated new VAPID key pair for Web Push.");
                return keys;
            }
            finally
            {
                KeyLock.Release();
            }
        }

        public async Task<string> GetPublicKeyAsync() => (await GetKeysAsync()).PublicKey;

        public async Task SaveSubscriptionAsync(int userId, string endpoint, string p256dh, string auth, string? userAgent)
        {
            var existing = await _context.WebPushSubscriptions.FirstOrDefaultAsync(s => s.Endpoint == endpoint);
            if (existing == null)
            {
                _context.WebPushSubscriptions.Add(new DbWebPushSubscription
                {
                    UserId = userId,
                    Endpoint = endpoint,
                    P256dh = p256dh,
                    Auth = auth,
                    UserAgent = userAgent,
                    CreatedAt = DateTime.UtcNow
                });
            }
            else
            {
                // Cùng trình duyệt nhưng đổi tài khoản hoặc khóa mới: gán lại cho user hiện tại.
                existing.UserId = userId;
                existing.P256dh = p256dh;
                existing.Auth = auth;
                existing.UserAgent = userAgent;
            }
            await _context.SaveChangesAsync();
        }

        public async Task RemoveSubscriptionAsync(int userId, string endpoint)
        {
            var existing = await _context.WebPushSubscriptions
                .FirstOrDefaultAsync(s => s.Endpoint == endpoint && s.UserId == userId);
            if (existing == null) return;
            _context.WebPushSubscriptions.Remove(existing);
            await _context.SaveChangesAsync();
        }

        public async Task<List<int>> FilterUsersWithSubscriptionsAsync(IEnumerable<int> userIds)
        {
            var result = new List<int>();
            foreach (var chunk in userIds.Distinct().Chunk(1000))
            {
                result.AddRange(await _context.WebPushSubscriptions.AsNoTracking()
                    .Where(s => chunk.Contains(s.UserId))
                    .Select(s => s.UserId)
                    .Distinct()
                    .ToListAsync());
            }
            return result;
        }

        public async Task<int> SendToUsersAsync(List<int> userIds, WebPushMessage message)
        {
            if (userIds == null || userIds.Count == 0) return 0;

            var keys = await GetKeysAsync();
            var vapid = new VapidDetails(_configuration["WebPush:Subject"] ?? "https://moonphim.me", keys.PublicKey, keys.PrivateKey);
            var payload = JsonSerializer.Serialize(new
            {
                title = Truncate(message.Title, 120),
                body = Truncate(message.Body, MaxBodyLength),
                url = SafeUrl(message.Url),
                tag = message.Tag,
                icon = "/apple-touch-icon.png",
                badge = "/favicon-32x32.png"
            });
            var options = new Dictionary<string, object> { ["vapidDetails"] = vapid, ["TTL"] = 86400 };

            var client = new WebPushClient(_httpClient);
            var sent = 0;
            var expired = new List<int>();

            foreach (var chunk in userIds.Distinct().Chunk(1000))
            {
                var subscriptions = await _context.WebPushSubscriptions
                    .Where(s => chunk.Contains(s.UserId))
                    .ToListAsync();

                foreach (var sub in subscriptions)
                {
                    try
                    {
                        await client.SendNotificationAsync(new PushSubscription(sub.Endpoint, sub.P256dh, sub.Auth), payload, options);
                        sub.LastSuccessAt = DateTime.UtcNow;
                        sent++;
                    }
                    catch (WebPushException ex) when (ex.StatusCode is HttpStatusCode.Gone or HttpStatusCode.NotFound)
                    {
                        expired.Add(sub.Id); // Người dùng đã gỡ quyền/xóa trình duyệt: dọn bản ghi.
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Web push failed for subscription {Id}", sub.Id);
                    }
                }

                if (expired.Count > 0)
                    _context.WebPushSubscriptions.RemoveRange(subscriptions.Where(s => expired.Contains(s.Id)));
                await _context.SaveChangesAsync();
                expired.Clear();
            }

            _logger.LogInformation("Web push sent {Sent} for {Users} users", sent, userIds.Count);
            return sent;
        }

        private static string Truncate(string? text, int max)
        {
            text = (text ?? "").Trim();
            return text.Length <= max ? text : text[..(max - 1)] + "…";
        }

        /// <summary>Chỉ cho đường dẫn nội bộ "/..." hoặc https://moonphim.me; còn lại về trang chủ.</summary>
        private static string SafeUrl(string? url)
        {
            if (string.IsNullOrWhiteSpace(url)) return "/trang-chu";
            if (url.StartsWith('/') && !url.StartsWith("//")) return url;
            if (Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps
                && uri.Host.Equals("moonphim.me", StringComparison.OrdinalIgnoreCase))
                return uri.PathAndQuery;
            return "/trang-chu";
        }
    }
}

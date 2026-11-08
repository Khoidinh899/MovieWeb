using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MovieWeb.Services.Interfaces;

namespace MovieWeb.Controllers
{
    /// <summary>Đăng ký/hủy thông báo đẩy trên trình duyệt/PWA (Web Push). Dùng cookie đăng nhập của web, không dùng cho app mobile.</summary>
    [Route("api/web-push")]
    public class WebPushController : Controller
    {
        private readonly IWebPushService _webPush;

        public WebPushController(IWebPushService webPush)
        {
            _webPush = webPush;
        }

        public class SubscriptionKeysDto
        {
            public string? P256dh { get; set; }
            public string? Auth { get; set; }
        }

        public class SubscriptionDto
        {
            public string? Endpoint { get; set; }
            public SubscriptionKeysDto? Keys { get; set; }
        }

        [HttpGet("public-key")]
        [AllowAnonymous]
        public async Task<IActionResult> PublicKey() => Json(new { publicKey = await _webPush.GetPublicKeyAsync() });

        [HttpPost("subscribe")]
        [Authorize(AuthenticationSchemes = "Identity.Application")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Subscribe([FromBody] SubscriptionDto dto)
        {
            if (!TryGetUserId(out var userId)) return Unauthorized();
            if (!IsValidEndpoint(dto?.Endpoint)
                || string.IsNullOrWhiteSpace(dto!.Keys?.P256dh) || dto.Keys.P256dh.Length > 200
                || string.IsNullOrWhiteSpace(dto.Keys.Auth) || dto.Keys.Auth.Length > 100)
                return BadRequest(new { success = false, message = "Dữ liệu đăng ký không hợp lệ." });

            var userAgent = Request.Headers.UserAgent.ToString();
            if (userAgent.Length > 300) userAgent = userAgent[..300];

            await _webPush.SaveSubscriptionAsync(userId, dto.Endpoint!, dto.Keys.P256dh, dto.Keys.Auth, userAgent);
            return Json(new { success = true });
        }

        [HttpPost("unsubscribe")]
        [Authorize(AuthenticationSchemes = "Identity.Application")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Unsubscribe([FromBody] SubscriptionDto dto)
        {
            if (!TryGetUserId(out var userId)) return Unauthorized();
            if (!IsValidEndpoint(dto?.Endpoint)) return BadRequest(new { success = false });
            await _webPush.RemoveSubscriptionAsync(userId, dto!.Endpoint!);
            return Json(new { success = true });
        }

        /// <summary>Gửi thử một thông báo tới các thiết bị của chính mình.</summary>
        [HttpPost("test")]
        [Authorize(AuthenticationSchemes = "Identity.Application")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Test()
        {
            if (!TryGetUserId(out var userId)) return Unauthorized();
            var sent = await _webPush.SendToUsersAsync(new() { userId }, new WebPushMessage
            {
                Title = "MoonPhim",
                Body = "Thông báo trên trình duyệt đã hoạt động 🎉",
                Url = "/user/notifications",
                Tag = "webpush-test"
            });
            return Json(new { success = sent > 0, sent });
        }

        private bool TryGetUserId(out int userId) =>
            int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);

        private static bool IsValidEndpoint(string? endpoint) =>
            !string.IsNullOrWhiteSpace(endpoint) && endpoint.Length <= 1000
            && Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps;
    }
}

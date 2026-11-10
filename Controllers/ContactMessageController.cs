using System;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MovieWeb.Services;

namespace MovieWeb.Controllers
{
    /// <summary>Form liên hệ (/contact): gửi tin nhắn về hộp thư Contact:ToEmail (mặc định contact@moonphim.me).</summary>
    public class ContactMessageController : Controller
    {
        private const int MaxPerWindow = 3;
        private static readonly TimeSpan Window = TimeSpan.FromMinutes(10);

        private readonly IEmailService _email;
        private readonly IMemoryCache _cache;
        private readonly IConfiguration _configuration;
        private readonly ILogger<ContactMessageController> _logger;

        public ContactMessageController(IEmailService email, IMemoryCache cache, IConfiguration configuration, ILogger<ContactMessageController> logger)
        {
            _email = email;
            _cache = cache;
            _configuration = configuration;
            _logger = logger;
        }

        public class ContactMessageDto
        {
            public string? Name { get; set; }
            public string? Email { get; set; }
            public string? Message { get; set; }
            /// <summary>Bẫy bot: ô ẩn, người thật để trống.</summary>
            public string? Website { get; set; }
        }

        [HttpPost("contact/send")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Send([FromForm] ContactMessageDto dto)
        {
            if (!string.IsNullOrEmpty(dto.Website)) return Json(new { success = true }); // bot: giả như đã gửi

            var name = (dto.Name ?? "").Trim();
            var email = (dto.Email ?? "").Trim();
            var message = (dto.Message ?? "").Trim();
            if (name.Length is < 2 or > 100 || message.Length is < 5 or > 3000
                || email.Length > 200 || !Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
            {
                return Json(new { success = false, message = "Vui lòng nhập họ tên, email hợp lệ và nội dung (tối thiểu 5 ký tự)." });
            }

            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var key = "contact_rate_" + ip;
            var count = _cache.GetOrCreate(key, e => { e.AbsoluteExpirationRelativeToNow = Window; return 0; });
            if (count >= MaxPerWindow)
            {
                return Json(new { success = false, message = "Bạn gửi hơi nhanh, vui lòng thử lại sau ít phút." });
            }
            _cache.Set(key, count + 1, Window);

            var to = _configuration["Contact:ToEmail"] ?? "contact@moonphim.me";
            Func<string, string> enc = s => WebUtility.HtmlEncode(s);
            var body = $@"<div style=""font-family:Arial,sans-serif;font-size:14px;line-height:1.6"">
<h2 style=""margin:0 0 12px"">Tin nhắn mới từ trang Liên hệ MoonPhim</h2>
<p><strong>Họ tên:</strong> {enc(name)}<br/>
<strong>Email:</strong> <a href=""mailto:{enc(email)}"">{enc(email)}</a><br/>
<strong>Thời gian:</strong> {DateTime.UtcNow.AddHours(7):dd/MM/yyyy HH:mm} (GMT+7)</p>
<div style=""white-space:pre-wrap;border-left:4px solid #0477eb;padding:8px 12px;background:#f5f7fb"">{enc(message)}</div>
<p style=""color:#888;font-size:12px"">Trả lời trực tiếp tới email người gửi ở trên.</p></div>";

            try
            {
                await _email.SendEmailAsync(to, $"[MoonPhim Liên hệ] {name}", body);
                _logger.LogInformation("Contact message sent to inbox from {Email}", email);
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send contact message");
                return Json(new { success = false, message = "Chưa gửi được tin nhắn, vui lòng thử lại sau hoặc email trực tiếp tới " + to });
            }
        }
    }
}

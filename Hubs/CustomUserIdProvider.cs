using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace MovieWeb.Hubs // <-- Kiểm tra lại namespace này cho đúng
{
    public class CustomUserIdProvider : IUserIdProvider
    {
        private readonly ILogger<CustomUserIdProvider> _logger;

        public CustomUserIdProvider(ILogger<CustomUserIdProvider> logger)
        {
            _logger = logger;
        }

        public virtual string GetUserId(HubConnectionContext connection)
        {
            // Thử tìm ID theo chuẩn (NameIdentifier)
            var userId = connection.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            string foundBy = "NameIdentifier";

            // Nếu không thấy, thử tìm theo tên (Name)
            if (string.IsNullOrEmpty(userId))
            {
                userId = connection.User?.FindFirst(ClaimTypes.Name)?.Value;
                foundBy = "Name";
            }

            // GHI LOG RA TERMINAL
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("!!! [SignalR] VẪN KHÔNG TÌM THẤY User ID (đã thử NameIdentifier và Name). Kết nối ẩn danh.");
            }
            else
            {
                // Đây là User ID thật sự (có thể là số "1", "2" hoặc một chuỗi GUID dài)
                _logger.LogInformation(">>> [SignalR] Đã dán nhãn User ID '{UserId}' (tìm thấy bằng '{FoundBy}') cho kết nối.", userId, foundBy);
            }

            return userId;
        }
    }
}
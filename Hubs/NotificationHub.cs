// Hubs/NotificationHub.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace MovieWeb.Hubs
{
    [Authorize]
    public class NotificationHub : Hub
    {
        private readonly ILogger<NotificationHub> _logger;

        public NotificationHub(ILogger<NotificationHub> logger)
        {
            _logger = logger;
        }

        // ========== KHI USER KẾT NỐI ==========
        public override async Task OnConnectedAsync()
        {
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            
            if (!string.IsNullOrEmpty(userId))
            {
                // Thêm user vào group theo UserId để dễ gửi notification
                await Groups.AddToGroupAsync(Context.ConnectionId, $"User_{userId}");
                _logger.LogInformation("✅ User {UserId} connected with ConnectionId: {ConnectionId}", 
                    userId, Context.ConnectionId);
            }
            else
            {
                _logger.LogWarning("⚠️ User connected without UserId. ConnectionId: {ConnectionId}", 
                    Context.ConnectionId);
            }

            await base.OnConnectedAsync();
        }

        // ========== KHI USER NGẮT KẾT NỐI ==========
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            
            if (!string.IsNullOrEmpty(userId))
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"User_{userId}");
                _logger.LogInformation("❌ User {UserId} disconnected. ConnectionId: {ConnectionId}", 
                    userId, Context.ConnectionId);
            }

            if (exception != null)
            {
                _logger.LogError(exception, "SignalR disconnected with error");
            }

            await base.OnDisconnectedAsync(exception);
        }

        // ========== METHOD ĐỂ CLIENT TEST KẾT NỐI ==========
        public async Task Ping()
        {
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            await Clients.Caller.SendAsync("Pong", $"Hello User {userId}!");
        }
    }
}
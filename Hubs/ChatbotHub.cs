using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using MovieWeb.Services;
using MovieWeb.Models.DTOs;

namespace MovieWeb.Hubs
{
    [Authorize] // User phải login mới chat được
    public class ChatbotHub : Hub
    {
        private readonly IGeminiService _geminiService;
        private readonly IMovieRequestService _movieRequestService;
        private readonly ILogger<ChatbotHub> _logger;

        // Lưu lịch sử chat tạm thời trong memory (mỗi user có 1 session)
        private static readonly ConcurrentDictionary<string, ChatSession> _chatSessions = new();

        public ChatbotHub(
            IGeminiService geminiService,
            IMovieRequestService movieRequestService,
            ILogger<ChatbotHub> logger)
        {
            _geminiService = geminiService;
            _movieRequestService = movieRequestService;
            _logger = logger;
        }

        public override async Task OnConnectedAsync()
        {
            var userId = GetUserId();
            _logger.LogInformation($"User {userId} connected to chatbot");

            // Khởi tạo session mới hoặc lấy session cũ
            var session = _chatSessions.GetOrAdd(userId, new ChatSession());

            // Gửi lời chào ban đầu nếu là lần đầu
            if (session.MessageCount == 0)
            {
                await Clients.Caller.SendAsync("ReceiveMessage", new ChatMessageResponse
                {
                    Role = "mooner",
                    Message = "Xin chào! Tôi là Mooner 🌙, trợ lý AI chuyên nghiệp đến từ MoonPhim. Hôm nay bạn muốn yêu cầu phim nào? Tôi sẽ rất vui nếu giúp được bạn! ✨",
                    Timestamp = DateTime.Now
                });
            }

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var userId = GetUserId();
            _logger.LogInformation($"User {userId} disconnected from chatbot");

            // Giữ session trong 30 phút sau khi disconnect
            // (user có thể quay lại và tiếp tục chat)

            await base.OnDisconnectedAsync(exception);
        }

        // ===== MAIN METHOD: XỬ LÝ TIN NHẮN TỪ USER =====
        public async Task SendMessage(string message)
        {
            var userId = GetUserId();
            var userIdInt = GetUserIdInt();

            _logger.LogInformation($"User {userId} sent message: {message}");

            try
            {
                // Lấy session
                var session = _chatSessions.GetOrAdd(userId, new ChatSession());

                // Lưu tin nhắn user vào conversation log
                session.AddMessage("user", message);

                // Echo tin nhắn user về client (hiển thị ngay)
                await Clients.Caller.SendAsync("ReceiveMessage", new ChatMessageResponse
                {
                    Role = "user",
                    Message = message,
                    Timestamp = DateTime.Now
                });

                // Hiển thị typing indicator
                await Clients.Caller.SendAsync("BotTyping", true);

                // Gọi Gemini AI để phân tích
                var aiResponse = await _geminiService.AnalyzeMovieRequestAsync(
                    message,
                    session.GetConversationHistory()
                );

                // Tắt typing indicator
                await Clients.Caller.SendAsync("BotTyping", false);

                if (!aiResponse.Success)
                {
                    await Clients.Caller.SendAsync("ReceiveMessage", new ChatMessageResponse
                    {
                        Role = "mooner",
                        Message = "Xin lỗi, tôi đang gặp chút vấn đề kỹ thuật. Bạn thử lại sau nhé! 😔",
                        Timestamp = DateTime.Now
                    });
                    return;
                }

                // Lưu response của AI vào session
                session.AddMessage("mooner", aiResponse.AiMessage);

                // Gửi tin nhắn AI về client
                await Clients.Caller.SendAsync("ReceiveMessage", new ChatMessageResponse
                {
                    Role = "mooner",
                    Message = aiResponse.AiMessage,
                    Timestamp = DateTime.Now
                });

                // ===== XỬ LÝ LOGIC DỰA TRÊN KẾT QUẢ AI =====

                // Nếu AI cần hỏi thêm thông tin → Chờ user trả lời
                if (aiResponse.NeedMoreInfo)
                {
                    session.IncrementAttempt();

                    // Nếu hỏi quá 2 lần → Chuyển sang Kịch bản 4
                    if (session.AttemptCount > 2)
                    {
                        await HandleScenario4(userIdInt, session);
                    }
                    // Còn không thì chờ user trả lời tiếp
                    return;
                }

                // Nếu AI đã tìm ra tên phim → Xử lý request
                if (!string.IsNullOrEmpty(aiResponse.MovieTitle))
                {
                    await ProcessMovieRequest(userIdInt, aiResponse, session);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error processing message from user {userId}");
                
                await Clients.Caller.SendAsync("ReceiveMessage", new ChatMessageResponse
                {
                    Role = "mooner",
                    Message = "Ối! Có gì đó không ổn. Bạn thử lại sau nhé! 😔",
                    Timestamp = DateTime.Now
                });
            }
        }

        // ===== XỬ LÝ CÁC KỊCH BẢN =====

        private async Task ProcessMovieRequest(int userId, GeminiResponse aiResponse, ChatSession session)
        {
            var result = await _movieRequestService.ProcessMovieRequestAsync(
                userId,
                aiResponse.MovieTitle!,
                aiResponse.MovieYear,
                session.GetConversationLog()
            );

            if (!result.Success)
            {
                await Clients.Caller.SendAsync("ReceiveMessage", new ChatMessageResponse
                {
                    Role = "mooner",
                    Message = "Xin lỗi, đã có lỗi khi xử lý yêu cầu của bạn. Bạn thử lại nhé! 😔",
                    Timestamp = DateTime.Now
                });
                return;
            }

            // Gửi kết quả dựa trên scenario
            var responseMessage = result.Scenario switch
            {
                RequestScenario.AlreadyExists => new ChatMessageResponse
                {
                    Role = "mooner",
                    Message = result.Message,
                    MovieUrl = result.MovieUrl,
                    HasMovie = true,
                    Timestamp = DateTime.Now
                },
                RequestScenario.PendingSync => new ChatMessageResponse
                {
                    Role = "mooner",
                    Message = result.Message,
                    RequestId = result.RequestId,
                    HasMovie = false,
                    Timestamp = DateTime.Now
                },
                RequestScenario.ManualVerification => new ChatMessageResponse
                {
                    Role = "mooner",
                    Message = result.Message,
                    RequestId = result.RequestId,
                    HasMovie = false,
                    Timestamp = DateTime.Now
                },
                _ => new ChatMessageResponse
                {
                    Role = "mooner",
                    Message = "Có gì đó không đúng. Thử lại nhé! 😔",
                    HasMovie = false,
                    Timestamp = DateTime.Now
                }
            };

            await Clients.Caller.SendAsync("ReceiveMessage", responseMessage);

            // Reset session sau khi hoàn tất
            session.Reset();
        }

        private async Task HandleScenario4(int userId, ChatSession session)
        {
            // Kịch bản 4: AI chịu thua sau 2 lần hỏi
            var message = "Xin lỗi vì tôi chưa hiểu rõ yêu cầu của bạn sau 2 lần hỏi 😔. Nhưng đừng lo! " +
                          "Tôi đã ghi nhận toàn bộ cuộc trò chuyện và sẽ chuyển cho admin tìm kiếm thủ công. " +
                          "Chúng tôi sẽ thông báo ngay khi có kết quả nhé! 💫";

            await Clients.Caller.SendAsync("ReceiveMessage", new ChatMessageResponse
            {
                Role = "mooner",
                Message = message,
                Timestamp = DateTime.Now
            });

            // Tạo request với movieTitle = null
            var result = await _movieRequestService.ProcessMovieRequestAsync(
                userId,
                "Yêu cầu không xác định được tên phim",
                null,
                session.GetConversationLog()
            );

            session.Reset();
        }

        // ===== HELPER METHODS =====

        private string GetUserId()
        {
            return Context.UserIdentifier ?? Context.ConnectionId;
        }

        private int GetUserIdInt()
        {
            var userIdClaim = Context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
            return int.TryParse(userIdClaim?.Value, out var userId) ? userId : 0;
        }
    }

    // ===== CHAT SESSION MODEL =====
    public class ChatSession
    {
        private readonly System.Collections.Generic.List<ChatMessage> _messages = new();
        public int AttemptCount { get; private set; } = 0;
        public int MessageCount => _messages.Count;
        public DateTime CreatedAt { get; } = DateTime.Now;

        public void AddMessage(string role, string message)
        {
            _messages.Add(new ChatMessage
            {
                Role = role,
                Message = message,
                Timestamp = DateTime.Now
            });
        }

        public void IncrementAttempt()
        {
            AttemptCount++;
        }

        public string GetConversationHistory()
        {
            return string.Join("\n", _messages.Select(m => $"[{m.Role}]: {m.Message}"));
        }

        public string GetConversationLog()
        {
            return string.Join("\n---\n", _messages.Select(m => 
                $"[{m.Timestamp:HH:mm:ss}] {m.Role.ToUpper()}: {m.Message}"
            ));
        }

        public void Reset()
        {
            _messages.Clear();
            AttemptCount = 0;
        }
    }

    public class ChatMessage
    {
        public string Role { get; set; } = ""; // "user" hoặc "mooner"
        public string Message { get; set; } = "";
        public DateTime Timestamp { get; set; }
    }
}
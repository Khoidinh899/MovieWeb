using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MovieWeb.Data;
using MovieWeb.Models.DTOs; 
using MovieWeb.Models.Entities; 
using MovieWeb.Services; 
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;
using System.Text;

namespace MovieWeb.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class ChatbotController : ControllerBase
    {
        private readonly MovieWebDbContext _context;
        private readonly IGeminiService _geminiService; 
        private readonly IMovieRequestService _movieRequestService;
        private readonly IRecommendationService _recommendationService; // THÊM MỚI
        private readonly ILogger<ChatbotController> _logger; 
        private readonly UserManager<User> _userManager;

        public ChatbotController(
            MovieWebDbContext context, 
            IGeminiService geminiService, 
            IMovieRequestService movieRequestService,
            IRecommendationService recommendationService, // THÊM MỚI
            ILogger<ChatbotController> logger, 
            UserManager<User> userManager)
        {
            _context = context;
            _geminiService = geminiService;
            _movieRequestService = movieRequestService;
            _recommendationService = recommendationService; // THÊM MỚI
            _logger = logger;
            _userManager = userManager;
        }

        // ===== CẬP NHẬT MODEL REQUEST =====
        public class ChatRequest
        {
            public string Message { get; set; }
            public string Mode { get; set; } = "by_name"; // "by_name", "by_description", "recommendation"
            public string History { get; set; } = ""; // Lưu lịch sử hội thoại cho recommendation
        }

        [HttpPost("SendMessage")]
        public async Task<IActionResult> SendMessage([FromBody] ChatRequest request)
        {
            var userId = GetUserId();

            // ===== KIỂM TRA QUYỀN PREMIUM =====
            if (userId == 0)
            {
                return Unauthorized(new { 
                    success = false, 
                    aiMessage = "Phiên đăng nhập không hợp lệ. Vui lòng tải lại trang." 
                });
            }

            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null)
            {
                return NotFound(new { 
                    success = false, 
                    aiMessage = "Không tìm thấy người dùng của bạn." 
                });
            }

            if (!user.IsPremium)
            {
                _logger.LogWarning("User {UserId} (Free) tried to use AI Chatbot.", userId);
                return StatusCode(403, new {
                    success = false, 
                    needMoreInfo = false,
                    aiMessage = "Tính năng này chỉ dành cho thành viên MoonPro/MoonStu. Vui lòng nâng cấp tài khoản nhé! 🚀"
                });
            }

            // ===== KIỂM TRA MESSAGE RỖNG =====
            if (string.IsNullOrEmpty(request.Message))
            {
                return BadRequest(new { success = false, message = "Message is required." });
            }

            // ===== PHÂN LUỒNG THEO MODE =====
            _logger.LogInformation("User {UserId} sent message with mode: {Mode}", userId, request.Mode);

            try
            {
                switch (request.Mode?.ToLower())
                {
                    case "by_name":
                        return await HandleRequestByName(userId, request);
                    
                    case "by_description":
                        return await HandleRequestByDescription(userId, request);
                    
                    case "recommendation":
                        return await HandleRecommendation(userId, request);
                    
                    default:
                        return BadRequest(new { 
                            success = false, 
                            aiMessage = "Mode không hợp lệ. Vui lòng chọn lại chế độ." 
                        });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing chatbot request");
                return StatusCode(500, new { 
                    success = false, 
                    aiMessage = "Đã có lỗi xảy ra. Vui lòng thử lại sau! 😔" 
                });
            }
        }

        // ===== HANDLER 1: YÊU CẦU THEO TÊN =====
        private async Task<IActionResult> HandleRequestByName(int userId, ChatRequest request)
        {
            _logger.LogInformation("Mode: Request by name");

            // Gọi AI để phân tích tên phim
            var aiResponse = await _geminiService.AnalyzeMovieRequestAsync(
                request.Message,
                request.History ?? "",
                "by_name" // Truyền mode xuống AI
            );

            _logger.LogInformation("AI Response: NeedMoreInfo={NeedInfo}, MovieTitle={Title}", 
                aiResponse.NeedMoreInfo, aiResponse.MovieTitle);

            // Nếu AI cần hỏi thêm thông tin
            if (!aiResponse.Success || aiResponse.NeedMoreInfo)
            {
                return Ok(aiResponse);
            }

            // Nếu AI chưa trích xuất được tên phim
            if (string.IsNullOrEmpty(aiResponse.MovieTitle))
            {
                return Ok(aiResponse);
            }

            // AI đã có tên phim -> Gọi service tìm kiếm thật
            var movieRequestResult = await _movieRequestService.ProcessMovieRequestAsync(
                userId,
                aiResponse.MovieTitle,
                aiResponse.MovieYear,
                $"AI Log: {aiResponse.RawAiResponse}"
            );

            // Cập nhật message từ MovieRequestService
            aiResponse.AiMessage = movieRequestResult.Message;

            if (movieRequestResult.Scenario == RequestScenario.AlreadyExists)
            {
                aiResponse.MovieUrl = movieRequestResult.MovieUrl;
            }

            return Ok(aiResponse);
        }

        // ===== HANDLER 2: YÊU CẦU THEO MÔ TẢ =====
        private async Task<IActionResult> HandleRequestByDescription(int userId, ChatRequest request)
        {
            _logger.LogInformation("Mode: Request by description");

            // Gọi AI để suy luận tên phim từ mô tả
            var aiResponse = await _geminiService.AnalyzeMovieRequestAsync(
                request.Message,
                request.History ?? "",
                "by_description" // Truyền mode xuống AI
            );

            _logger.LogInformation("AI Response: NeedMoreInfo={NeedInfo}, MovieTitle={Title}", 
                aiResponse.NeedMoreInfo, aiResponse.MovieTitle);

            // Nếu AI cần hỏi thêm
            if (!aiResponse.Success || aiResponse.NeedMoreInfo)
            {
                return Ok(aiResponse);
            }

            // Nếu AI chưa đoán được tên phim
            if (string.IsNullOrEmpty(aiResponse.MovieTitle))
            {
                return Ok(aiResponse);
            }

            // AI đã đoán được tên -> Xử lý giống mode by_name
            var movieRequestResult = await _movieRequestService.ProcessMovieRequestAsync(
                userId,
                aiResponse.MovieTitle,
                aiResponse.MovieYear,
                $"AI Log (Description): {aiResponse.RawAiResponse}"
            );

            aiResponse.AiMessage = movieRequestResult.Message;

            if (movieRequestResult.Scenario == RequestScenario.AlreadyExists)
            {
                aiResponse.MovieUrl = movieRequestResult.MovieUrl;
            }

            return Ok(aiResponse);
        }

        // ===== HANDLER 3: GỢI Ý PHIM (LOGIC MỚI) =====
        private async Task<IActionResult> HandleRecommendation(int userId, ChatRequest request)
        {
            _logger.LogInformation("Mode: Recommendation");

            // Gọi AI để phân tích yêu cầu gợi ý
            var aiResponse = await _geminiService.AnalyzeRecommendationRequestAsync(
                request.Message,
                request.History ?? ""
            );

            _logger.LogInformation("AI Recommendation Response: NeedMoreInfo={NeedInfo}", aiResponse.NeedMoreInfo);

            // Nếu AI cần hỏi thêm thông tin (thể loại, quốc gia, năm...)
            if (aiResponse.NeedMoreInfo)
            {
                return Ok(aiResponse);
            }

            // AI đã đủ thông tin -> Gọi RecommendationService để lọc phim
            var recommendations = await _recommendationService.GetRecommendationsAsync(
                aiResponse.Genre,
                aiResponse.Country,
                aiResponse.Type,
                aiResponse.Year
            );

            if (recommendations == null || recommendations.Count == 0)
            {
                return Ok(new
                {
                    success = true,
                    needMoreInfo = false,
                    aiMessage = "Rất tiếc, tôi không tìm thấy phim nào phù hợp với yêu cầu của bạn 😔. Bạn có muốn thử tiêu chí khác không?"
                });
            }

            // Format danh sách phim thành message
            var movieListMessage = new StringBuilder(); // Dùng StringBuilder
            movieListMessage.Append("Đây là những bộ phim tôi gợi ý cho bạn:<br><br>");
            
            foreach (var movie in recommendations)
            {
                // Thêm CSS inline cho đẹp
                movieListMessage.Append($"<div style='margin-bottom: 15px; border-bottom: 1px solid #f0f0f0; padding-bottom: 10px;'>");
                
                // Tên phim (Icon 🎬)
                movieListMessage.Append($"<strong style='font-size: 1.05em;'>🎬 {movie.Name} ({movie.Year})</strong><br>");
                
                // Thông tin (Icon ⭐ và Loại phim đã được dịch ở Service)
                movieListMessage.Append($"<span style='font-size: 0.9em; color: #6c757d;'>"); // Màu xám
                movieListMessage.Append($"⭐ {movie.Rating}/10 | ");
                movieListMessage.Append($"🎞️ {movie.Type}"); // Dùng movie.Type đã được map
                movieListMessage.Append($"</span><br>");
                
                // Nút "Xem ngay" (Icon 🔗) - Dùng style gradient của bạn
                movieListMessage.Append($"<a href='/phim/{movie.Slug}' target='_blank' style='display: inline-block; margin-top: 8px; padding: 6px 14px; background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); color: white; text-decoration: none; border-radius: 20px; font-size: 0.85em; font-weight: bold;'>");
                movieListMessage.Append($"🔗 Xem ngay");
                movieListMessage.Append($"</a>");
                
                movieListMessage.Append($"</div>");
            }
            movieListMessage.Append("Bạn có cần gợi ý thêm phim nào khác không? 😊");

            return Ok(new
            {
                success = true,
                needMoreInfo = false,
                
                // Đây là message HTML
                aiMessage = movieListMessage.ToString(), 
                
                // Đánh dấu đây là HTML để FE biết
                isHtmlMessage = true, 

                // Giữ nguyên data này nếu bạn muốn dùng ở FE
                recommendations = recommendations.Select(m => new {
                    url = $"/phim/{m.Slug}"
                    // ... các thuộc tính khác
                }).ToList()
            });
            // ===== KẾT THÚC SỬA Ở ĐÂY =====
        }

        // ===== CÁC ENDPOINT KHÁC (GIỮ NGUYÊN) =====
        
        [HttpGet("requests")]
        public async Task<IActionResult> GetMyRequests([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            var userId = GetUserId();

            var query = _context.UserRequestMovies
                .Include(ur => ur.Request)
                    .ThenInclude(r => r.Movie) 
                .Where(ur => ur.UserId == userId)
                .OrderByDescending(ur => ur.CreatedAt);

            var totalItems = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

            var requests = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(ur => new
                {
                    requestId = ur.RequestId,
                    movieTitle = ur.Request.MovieTitle,
                    movieYear = ur.Request.MovieYear,
                    status = ur.Request.Status,
                    requestCount = ur.Request.RequestCount,
                    createdAt = ur.CreatedAt, 
                    updatedAt = ur.Request.CompletedAt,
                    movieUrl = ur.Request.MovieId != null ? $"/phim/{ur.Request.Movie!.Slug}" : null
                })
                .ToListAsync();

            return Ok(new
            {
                success = true,
                data = requests,
                pagination = new
                {
                    currentPage = page,
                    totalPages = totalPages,
                    totalItems = totalItems,
                    pageSize = pageSize
                }
            });
        }

        [HttpGet("requests/{id}")]
        public async Task<IActionResult> GetRequestDetail(int id)
        {
            var userId = GetUserId();

            var userRequest = await _context.UserRequestMovies
                .Include(ur => ur.Request)
                    .ThenInclude(r => r.Movie)
                .FirstOrDefaultAsync(ur => ur.RequestId == id && ur.UserId == userId);

            if (userRequest == null)
            {
                return NotFound(new { success = false, message = "Không tìm thấy yêu cầu" });
            }

            var request = userRequest.Request;

            return Ok(new
            {
                success = true,
                data = new
                {
                    requestId = request.Id,
                    movieTitle = request.MovieTitle,
                    movieYear = request.MovieYear,
                    status = request.Status,
                    requestCount = request.RequestCount,
                    conversationLog = request.ConversationLog,
                    createdAt = userRequest.CreatedAt, 
                    updatedAt = request.CompletedAt,
                    completedAt = request.CompletedAt, 
                    movieUrl = request.MovieId != null ? $"/phim/{request.Movie!.Slug}" : null,
                    movieInfo = request.Movie != null ? new
                    {
                        name = request.Movie.Name,
                        originalName = request.Movie.OriginalName,
                        posterUrl = request.Movie.PosterUrl,
                        year = request.Movie.Year
                    } : null
                }
            });
        }

        [HttpDelete("requests/{id}")]
        public async Task<IActionResult> CancelRequest(int id)
        {
            var userId = GetUserId();

            var userRequest = await _context.UserRequestMovies
                .Include(ur => ur.Request)
                .FirstOrDefaultAsync(ur => ur.RequestId == id && ur.UserId == userId);

            if (userRequest == null)
            {
                return NotFound(new { success = false, message = "Không tìm thấy yêu cầu" });
            }

            var request = userRequest.Request;

            if (request.Status == RequestStatus.Completed)
            {
                return BadRequest(new { success = false, message = "Không thể hủy yêu cầu đã hoàn thành" });
            }

            _context.UserRequestMovies.Remove(userRequest);

            var otherUsers = await _context.UserRequestMovies
                .Where(ur => ur.RequestId == id && ur.UserId != userId)
                .CountAsync();

            if (otherUsers == 0)
            {
                _context.RequestsMovies.Remove(request);
            }
            else
            {
                request.RequestCount--;
            }

            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Đã hủy yêu cầu thành công" });
        }

        [HttpGet("stats")]
        public async Task<IActionResult> GetStats()
        {
            var userId = GetUserId();

            var totalRequests = await _context.UserRequestMovies
                .Where(ur => ur.UserId == userId)
                .CountAsync();

            var completedRequests = await _context.UserRequestMovies
                .Include(ur => ur.Request)
                .Where(ur => ur.UserId == userId && ur.Request.Status == RequestStatus.Completed)
                .CountAsync();

            var pendingRequests = await _context.UserRequestMovies
                .Include(ur => ur.Request)
                .Where(ur => ur.UserId == userId && ur.Request.Status != RequestStatus.Completed)
                .CountAsync();

            return Ok(new
            {
                success = true,
                data = new
                {
                    totalRequests = totalRequests,
                    completedRequests = completedRequests,
                    pendingRequests = pendingRequests
                }
            });
        }

        private int GetUserId()
        {
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
            return int.TryParse(userIdClaim?.Value, out var userId) ? userId : 0;
        }
    }
}
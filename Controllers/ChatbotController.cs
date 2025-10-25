using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MovieWeb.Data;
using MovieWeb.Models.DTOs; // B cần DTOs.cs cho ChatMessageResponse, nhưng controller này ko dùng
using MovieWeb.Models.Entities; // B cần RequestsMovie.cs, UserRequestMovie.cs
using MovieWeb.Services; // <-- *** THÊM DÒNG NÀY ***

namespace MovieWeb.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class ChatbotController : ControllerBase
    {
        private readonly MovieWebDbContext _context;
        private readonly IGeminiService _geminiService; // <-- *** THÊM DÒNG NÀY ***
        private readonly IMovieRequestService _movieRequestService; // <-- *** THÊM DÒNG NÀY ***
        private readonly ILogger<ChatbotController> _logger; // <-- THÊM DÒNG NÀY

        public ChatbotController(MovieWebDbContext context, IGeminiService geminiService, IMovieRequestService movieRequestService, ILogger<ChatbotController> logger)
        {
            _context = context;
            _geminiService = geminiService; // <-- *** THÊM DÒNG NÀY ***
            _movieRequestService = movieRequestService;
            _logger = logger; // <-- THÊM DÒNG NÀY
        }
        public class ChatRequest
        {
            public string Message { get; set; }
            public string History { get; set; } // Tương lai sẽ dùng
        }


        [HttpPost("SendMessage")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendMessage([FromBody] ChatRequest request)
        {
            if (string.IsNullOrEmpty(request.Message))
            {
                return BadRequest(new { success = false, message = "Message is required." });
            }

            var userId = GetUserId(); // Lấy userId

            // === BƯỚC 1: HỎI AI ĐỂ LẤY TÊN PHIM ===
            var aiResponse = await _geminiService.AnalyzeMovieRequestAsync(
                request.Message,
                request.History ?? ""
            );
_logger.LogWarning("AI Response: NeedMoreInfo={NeedInfo}, MovieTitle={Title}", aiResponse.NeedMoreInfo, aiResponse.MovieTitle);
            // Nếu AI báo lỗi hoặc AI cần hỏi thêm (needMoreInfo)
            if (!aiResponse.Success || aiResponse.NeedMoreInfo)
            {
                // Thì cứ trả về câu hỏi của AI
                return Ok(aiResponse);
            }

            // === BƯỚC 2: AI ĐÃ TÌM RA TÊN PHIM (ví dụ: "Bậc thầy giải hoà") ===
            // Bây giờ ta dùng tên đó để TÌM THẬT trong DB

            if (string.IsNullOrEmpty(aiResponse.MovieTitle))
            {
                // Nếu AI tự tin (NeedMoreInfo=false) nhưng lại không trả về tên phim?
                // Trả về tin nhắn AI cho an toàn
                return Ok(aiResponse);
            }

            // GỌI SERVICE TÌM KIẾM THẬT
            var movieRequestResult = await _movieRequestService.ProcessMovieRequestAsync(
                userId,
                aiResponse.MovieTitle,
                aiResponse.MovieYear,
                $"AI Log: {aiResponse.RawAiResponse}" // Lưu lại log AI
            );

            // === BƯỚC 3: TRẢ VỀ KẾT QUẢ TÌM THẬT ===
            // Lấy cái message xịn từ service (ví dụ: "Phim đã có sẵn!")
            // Ghi đè lên cái message demo của AI
            aiResponse.AiMessage = movieRequestResult.Message;

            // Nếu phim đã có, trả về URL
            if (movieRequestResult.Scenario == RequestScenario.AlreadyExists)
            {
                // Bạn có thể thêm 1 trường "movieUrl" vào GeminiResponse để JS xử lý
                // aiResponse.MovieUrl = movieRequestResult.MovieUrl; (Nếu bạn tự thêm)
            }

            return Ok(aiResponse); // Trả về kết quả cuối cùng
        }
    
        /// <summary>
        /// Lấy lịch sử yêu cầu phim của user
        /// GET: api/chatbot/requests
        /// </summary>
        [HttpGet("requests")]
        public async Task<IActionResult> GetMyRequests([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            var userId = GetUserId();

            var query = _context.UserRequestMovies
                .Include(ur => ur.Request)
                    .ThenInclude(r => r.Movie) // Giờ đã có thể Include Movie
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
                    createdAt = ur.CreatedAt, // Ngày user này yêu cầu

                    // FIX 1: 'RequestsMovie' không có 'UpdatedAt'.
                    // Ta dùng 'CompletedAt' để thay thế.
                    updatedAt = ur.Request.CompletedAt,

                    // FIX 2: Logic này giờ đã chạy đúng vì ta đã thêm MovieId và Movie
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

        /// <summary>
        /// Lấy chi tiết một request
        /// GET: api/chatbot/requests/{id}
        /// </summary>
        [HttpGet("requests/{id}")]
        public async Task<IActionResult> GetRequestDetail(int id)
        {
            var userId = GetUserId();

            var userRequest = await _context.UserRequestMovies
                .Include(ur => ur.Request)
                    // FIX 3: Logic này giờ đã chạy đúng
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
                    createdAt = userRequest.CreatedAt, // Ngày user này tạo request

                    // FIX 4: 'request' (RequestsMovie) không có 'UpdatedAt'.
                    // Ta dùng 'CompletedAt' (vì 'CreatedAt' của request đã có ở dưới)
                    updatedAt = request.CompletedAt,

                    completedAt = request.CompletedAt, // Ngày request được hoàn thành

                    // FIX 5: Logic movieUrl và movieInfo giờ đã chạy đúng
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

        /// <summary>
        /// Hủy một request đang pending
        /// DELETE: api/chatbot/requests/{id}
        /// </summary>
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

            // Chỉ cho phép hủy nếu chưa hoàn thành (Dùng hằng số)
            if (request.Status == RequestStatus.Completed)
            {
                return BadRequest(new { success = false, message = "Không thể hủy yêu cầu đã hoàn thành" });
            }

            // Xóa UserRequestMovie
            _context.UserRequestMovies.Remove(userRequest);

            // Nếu không còn user nào request phim này, xóa luôn RequestsMovie
            var otherUsers = await _context.UserRequestMovies
                .Where(ur => ur.RequestId == id && ur.UserId != userId)
                .CountAsync();

            if (otherUsers == 0)
            {
                _context.RequestsMovies.Remove(request);
            }
            else
            {
                // Giảm request count
                request.RequestCount--;
            }

            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Đã hủy yêu cầu thành công" });
        }

        /// <summary>
        /// Lấy thống kê yêu cầu của user
        /// GET: api/chatbot/stats
        /// </summary>
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

        // Helper method
        private int GetUserId()
        {
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
            return int.TryParse(userIdClaim?.Value, out var userId) ? userId : 0;
        }
    }
}
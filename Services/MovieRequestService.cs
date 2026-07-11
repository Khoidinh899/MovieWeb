using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MovieWeb.Data;
using MovieWeb.Models.Entities;

namespace MovieWeb.Services
{
    public interface IMovieRequestService
    {
        Task<MovieRequestResult> ProcessMovieRequestAsync(int userId, string movieTitle, int? movieYear, string conversationLog);
        Task<bool> NotifyUserAsync(int userId, string title, string content, string? url = null);
    }

    public class MovieRequestService : IMovieRequestService
    {
        private readonly MovieWebDbContext _context;
        private readonly IOPhimService _ophimService;
        private readonly ILogger<MovieRequestService> _logger;

        public MovieRequestService(
            MovieWebDbContext context,
            IOPhimService ophimService,
            ILogger<MovieRequestService> logger)
        {
            _context = context;
            _ophimService = ophimService;
            _logger = logger;
        }

        public async Task<MovieRequestResult> ProcessMovieRequestAsync(
            int userId,
            string movieTitle,
            int? movieYear,
            string conversationLog)
        {
            try
            {
                // ===== KỊCH BẢN 1: PHIM ĐÃ CÓ TRONG DB =====
                var existingMovie = await CheckMovieInDatabaseAsync(movieTitle, movieYear);
                if (existingMovie != null)
                {
                    _logger.LogInformation($"Movie '{movieTitle}' already exists in DB (ID: {existingMovie.MovieId})");

                    // Tùy chọn: Ghi log vào Notifications
                    await NotifyUserAsync(
                        userId,
                        "Phim đã có sẵn! 🎉",
                        $"Phim '{existingMovie.Name}' bạn yêu cầu đã có trên MoonPhim rồi nhé!",
                        $"/phim/{existingMovie.Slug}"
                    );

                    return new MovieRequestResult
                    {
                        Success = true,
                        Scenario = RequestScenario.AlreadyExists,
                        Message = $"Phim '{existingMovie.Name}' đã có trên hệ thống! 🎬",
                        MovieUrl = $"/phim/{existingMovie.Slug}",
                        ExistingMovie = existingMovie
                    };
                }

                // ===== KỊCH BẢN 2: CHECK API OPHIM =====
                var ophimResult = await CheckMovieOnOPhimAsync(movieTitle);

                if (ophimResult.Found)
                {
                    _logger.LogInformation($"Movie '{movieTitle}' found on OPhim (slug: {ophimResult.Slug})");

                    // Tạo hoặc cập nhật request
                    var request = await CreateOrUpdateRequestAsync(
                        userId,
                        movieTitle,
                        movieYear,
                        ophimResult.Slug,
                        RequestStatus.Pending, // "Chờ đồng bộ"
                        conversationLog
                    );

                    await NotifyUserAsync(
                        userId,
                        "Yêu cầu đã tiếp nhận! ⏳",
                        $"Phim '{movieTitle}' sẽ được đồng bộ sớm. Chúng tôi sẽ báo bạn ngay khi có nhé! 🌙",
                        null
                    );

                    return new MovieRequestResult
                    {
                        Success = true,
                        Scenario = RequestScenario.PendingSync,
                        Message = "Phim này sẽ được đồng bộ sớm, chúng mình sẽ báo bạn ngay khi có nhé! ⏳",
                        RequestId = request.Id
                    };
                }

                // ===== KỊCH BẢN 3 & 4: CẦN XÁC MINH THỦ CÔNG =====
                _logger.LogInformation($"Movie '{movieTitle}' not found anywhere. Creating manual verification request.");

                var manualRequest = await CreateOrUpdateRequestAsync(
                    userId,
                    movieTitle,
                    movieYear,
                    null,
                    RequestStatus.NeedsVerification, // "Cần xác minh thủ công"
                    conversationLog
                );

                await NotifyUserAsync(
                    userId,
                    "Yêu cầu đã ghi nhận! 📝",
                    $"Phim '{movieTitle}' sẽ được admin tìm kiếm thủ công. Chúng tôi sẽ thông báo ngay khi có kết quả! 💫",
                    null
                );

                return new MovieRequestResult
                {
                    Success = true,
                    Scenario = RequestScenario.ManualVerification,
                    Message = "Tôi đã ghi nhận yêu cầu và sẽ chuyển cho admin tìm kiếm thủ công. Chúng tôi sẽ thông báo ngay khi có phim nhé! 📝",
                    RequestId = manualRequest.Id
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error processing movie request: {movieTitle}");
                return new MovieRequestResult
                {
                    Success = false,
                    Message = "Đã có lỗi xảy ra khi xử lý yêu cầu. Vui lòng thử lại sau! 😔"
                };
            }
        }

        // ===== PRIVATE HELPERS =====

        private async Task<Movie?> CheckMovieInDatabaseAsync(string movieTitle, int? movieYear)
        {
            // "%" là ký tự đại diện (wildcard) cho SQL Like
            var searchTerm = $"%{movieTitle.Trim()}%";

            var query = _context.Movies
                .Where(m => m.IsActive == true)
                .Where(m =>
                    EF.Functions.ILike(m.Name, searchTerm) ||
                    (m.OriginalName != null && EF.Functions.ILike(m.OriginalName, searchTerm))
                );

            if (movieYear.HasValue)
            {
                query = query.Where(m => m.Year == movieYear.Value);
            }

            return await query.OrderByDescending(m => m.Year == movieYear).FirstOrDefaultAsync();
        }

        private async Task<OPhimSearchResult> CheckMovieOnOPhimAsync(string movieTitle)
        {
            try
            {
                var searchResult = await _ophimService.SearchMoviesAsync(movieTitle, page: 1);

                if (searchResult?.Data?.Items != null && searchResult.Data.Items.Count > 0)
                {
                    var firstMovie = searchResult.Data.Items.First();
                    return new OPhimSearchResult
                    {
                        Found = true,
                        Slug = firstMovie.Slug,
                        Name = firstMovie.Name
                    };
                }

                return new OPhimSearchResult { Found = false };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error searching movie on OPhim: {movieTitle}");
                return new OPhimSearchResult { Found = false };
            }
        }

        private async Task<RequestsMovie> CreateOrUpdateRequestAsync(int userId, string movieTitle, int? movieYear, string? ophimSlug, string status, string conversationLog)
        {
            // Kiểm tra xem đã có request tương tự chưa
            var existingRequest = await _context.RequestsMovies
                .Include(r => r.UserRequests)
                .FirstOrDefaultAsync(r =>
                    r.MovieTitle == movieTitle &&
                    r.Status != RequestStatus.Completed
                );

            if (existingRequest != null)
            {
                _logger.LogInformation($"Request for '{movieTitle}' already exists (ID: {existingRequest.Id}). Updating...");

                // Cập nhật request hiện có
                existingRequest.RequestCount++;
                existingRequest.Status = status;
                existingRequest.OphimSlug = ophimSlug ?? existingRequest.OphimSlug;
                existingRequest.ConversationLog = conversationLog;

                // Kiểm tra user đã request chưa
                var userAlreadyRequested = existingRequest.UserRequests
                    .Any(ur => ur.UserId == userId);

                if (!userAlreadyRequested)
                {
                    // === LOGIC NÀY ĐÃ ĐÚNG ===
                    existingRequest.UserRequests.Add(new UserRequestMovie
                    {
                        UserId = userId,
                        CreatedAt = DateTime.Now
                    });
                }

                await _context.SaveChangesAsync();
                return existingRequest;
            }

            // ==================================================
            // === SỬA LỖI NẰM Ở ĐÂY (TẠO REQUEST MỚI) ===
            // ==================================================
            _logger.LogInformation($"Creating new request for '{movieTitle}'.");

            // Tạo request mới
            var newRequest = new RequestsMovie
            {
                MovieTitle = movieTitle,
                MovieYear = movieYear,
                OphimSlug = ophimSlug,
                RequestCount = 1,
                Status = status,
                ConversationLog = conversationLog,
                CreatedAt = DateTime.Now,

                // SỬA 1: Phải khởi tạo collection để EF Core nhận diện
                UserRequests = new List<UserRequestMovie>()
            };

            // SỬA 2: Thêm UserRequestMovie vào collection của cha
            // EF Core sẽ tự động hiểu và gán RequestId
            newRequest.UserRequests.Add(new UserRequestMovie
            {
                UserId = userId,
                CreatedAt = DateTime.Now
                // Không cần gán RequestId, EF Core tự làm
            });

            _context.RequestsMovies.Add(newRequest);

            // SỬA 3: Chỉ cần LƯU 1 LẦN
            // EF Core sẽ tự động lưu cả newRequest và userRequestLink
            await _context.SaveChangesAsync();

            // (Toàn bộ khối "Thêm UserRequestMovie" cũ ở dưới đã được xóa)

            return newRequest;
        }

        public async Task<bool> NotifyUserAsync(int userId, string title, string content, string? url = null)
        {
            try
            {
                var notification = new Notification
                {
                    UserId = userId,
                    Title = title,
                    Content = content,
                    Type = "movie_request",
                    Url = url,
                    IsRead = false,
                    CreatedAt = DateTime.Now
                };

                _context.Notifications.Add(notification);
                await _context.SaveChangesAsync();

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error creating notification for user {userId}");
                return false;
            }
        }
    }

    // ===== RESULT MODELS =====

    public class MovieRequestResult
    {
        public bool Success { get; set; }
        public RequestScenario Scenario { get; set; }
        public string Message { get; set; } = "";
        public string? MovieUrl { get; set; }
        public int? RequestId { get; set; }
        public Movie? ExistingMovie { get; set; }
    }

    public enum RequestScenario
    {
        AlreadyExists,      // Kịch bản 1: Phim đã có
        PendingSync,        // Kịch bản 2: Chờ đồng bộ
        ManualVerification  // Kịch bản 3 & 4: Cần xác minh
    }

    public class OPhimSearchResult
    {
        public bool Found { get; set; }
        public string? Slug { get; set; }
        public string? Name { get; set; }
    }
}
// Jobs/BackgroundSyncMovieJob.cs
using Hangfire;
using Microsoft.EntityFrameworkCore;
using MovieWeb.Data;
using MovieWeb.Models.Entities;
using MovieWeb.Services;
using MovieWeb.Services.Interfaces;

namespace MovieWeb.Jobs
{
    public class BackgroundSyncMovieJob
    {
        private readonly MovieWebDbContext _context;
        private readonly IMovieSyncService _movieSyncService; // ✅ Dùng service có sẵn của b
        private readonly ILogger<BackgroundSyncMovieJob> _logger;
        private readonly IBackgroundJobClient _backgroundJobClient;
        private readonly INotificationService _notificationService;

        // ===== LỖI LÀ Ở ĐÂY =====
        // T đã xóa chữ "Bỏ vào đây" và sửa lại hàm constructor cho đúng
        public BackgroundSyncMovieJob(
            MovieWebDbContext context,
            IMovieSyncService movieSyncService, 
            ILogger<BackgroundSyncMovieJob> logger,
            IBackgroundJobClient backgroundJobClient,
            INotificationService notificationService)
        { // <-- Sửa lỗi: Thêm {
            _context = context;
            _movieSyncService = movieSyncService;
            _logger = logger;
            _backgroundJobClient = backgroundJobClient;
            _notificationService = notificationService;
        } // <-- Sửa lỗi: Thêm }

        /// <summary>
        /// Job này chạy định kỳ:
        /// 1. Tìm request "Chờ đồng bộ" (Pending)
        /// 2. "Khóa" chúng bằng cách set Status = "Đang xử lý" (Processing)
        /// 3. Dùng MovieSyncService để thêm phim vào DB
        /// 4. Cập nhật request: Status = "Hoàn tất" + movie_id = [ID phim vừa thêm]
        /// </summary>
        [AutomaticRetry(Attempts = 0)] // Không retry job này, để nó chạy ở lần kế tiếp
        public async Task Execute()
        {
            _logger.LogInformation("╔══════════════════════════════════════╗");
            _logger.LogInformation("║  MOVIE REQUEST PROCESSOR JOB - BẮT ĐẦU ║");
            _logger.LogInformation("╚══════════════════════════════════════╝");

            // 1. Tìm tất cả request "Chờ đồng bộ" VÀ có ophim_slug
            var requestsToProcess = await _context.RequestsMovies
                .Include(r => r.UserRequests) // Lấy danh sách user đã yêu cầu
                .Where(r => r.Status == RequestStatus.Pending && r.OphimSlug != null)
                .ToListAsync();

            if (!requestsToProcess.Any())
            {
                _logger.LogInformation("✅ Không có request 'Chờ đồng bộ' nào. Kết thúc.");
                _logger.LogInformation("═══════════════════════════════════════════");
                return;
            }

            _logger.LogInformation($"🔍 Tìm thấy {requestsToProcess.Count} request(s). Bắt đầu 'Khóa' (set 'Processing')...");

            // 2. "Khóa" các request này lại để admin không xử lý trùng
            foreach (var req in requestsToProcess)
            {
                req.Status = RequestStatus.Processing; // <-- DÙNG STATUS MỚI
                req.AdminNote = "Job tự động đang xử lý...";
            }
            // Dùng try-catch để nếu lỗi thì set lại status
            try
            {
                await _context.SaveChangesAsync();
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi cập nhật status 'Processing'. Hủy bỏ job lần này.");
                return; // Dừng lại, lần sau chạy tiếp
            }
            
            _logger.LogInformation("✅ Đã 'khóa' {Count} request. Bắt đầu gọi MovieSyncService...", requestsToProcess.Count);

            try
            {
                // 3. Dùng MovieSyncService của b để đồng bộ phim
                // Tạo một List<ApiMovie> "giả" từ RequestsMovie
                var apiMoviesToSync = requestsToProcess
                    .Where(r => !string.IsNullOrEmpty(r.OphimSlug)) // Double check
                    .Select(r => new MovieWeb.Models.API.Movie
                    {
                        Slug = r.OphimSlug!,
                        Name = r.MovieTitle ?? "N/A",
                        Year = r.MovieYear ?? 0
                    }).ToList();

                // Gọi hàm sync chính (hàm này sẽ tự check phim tồn tại, tự gọi OPhim, tự save)
                await _movieSyncService.SyncMoviesFromApiToDbAsync(apiMoviesToSync, 0);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Lỗi nghiêm trọng khi đang chạy MovieSyncService. Các request sẽ được retry ở lần sau.");
                // Set lại status về Pending để lần sau chạy lại
                foreach (var req in requestsToProcess)
                {
                    req.Status = RequestStatus.Pending; 
                    req.AdminNote = "Lỗi job, chờ retry.";
                }
                await _context.SaveChangesAsync();
                throw; // Ném lỗi để Hangfire biết là job fail
            }


            _logger.LogInformation("✅ MovieSyncService đã chạy xong. Bắt đầu liên kết kết quả (cập nhật 'movie_id')...");

            // 4. CẬP NHẬT REQUEST (THỰC HIỆN "LƯU Ý")
            // Vòng lặp này để kiểm tra kết quả sau khi MovieSyncService đã chạy
            foreach (var request in requestsToProcess)
            {
                // Tìm lại phim trong DB (vì MovieSyncService vừa thêm nó vào)
                var movieInDb = await _context.Movies
                    .AsNoTracking()
                    .FirstOrDefaultAsync(m => m.Slug == request.OphimSlug);

                if (movieInDb != null)
                {
                    // 4a. THÀNH CÔNG: Phim đã có trong DB
                    request.Status = RequestStatus.Completed;
                    request.MovieId = movieInDb.MovieId; // <-- LIÊN KẾT ID PHIM ĐÂY
                    request.CompletedAt = DateTime.Now;
                    request.AdminNote = "Đồng bộ tự động hoàn tất.";

                    _logger.LogInformation("✅ Hoàn tất: Request (ID: {ReqId}) -> Movie (ID: {MovieId})", request.Id, movieInDb.MovieId);

                    // 5. Gửi thông báo cho TẤT CẢ user đã yêu cầu phim này
                    foreach (var userRequest in request.UserRequests)
                    {
                        _backgroundJobClient.Enqueue<INotificationService>(
                            service => service.CreateMovieRequestCompletedAsync(
                                userRequest.UserId,
                                movieInDb.MovieId,
                                movieInDb.Name
                            )
                        );
                    }
                }
                else
                {
                    // 4b. THẤT BẠI: MovieSyncService (vì lý do nào đó) không thêm được phim này
                    _logger.LogWarning("❌ Thất bại: MovieSyncService đã chạy nhưng không tìm thấy phim (Slug: {Slug}). Chuyển sang 'Cần xác minh'.", request.OphimSlug);
                    request.Status = RequestStatus.NeedsVerification;
                    request.AdminNote = "Job tự động: MovieSyncService không thể đồng bộ phim này.";
                }
            } // Hết vòng lặp foreach

            await _context.SaveChangesAsync(); // Lưu lại tất cả thay đổi (Completed / NeedsVerification)

            _logger.LogInformation("╔════════════════════════════════════════╗");
            _logger.LogInformation("║  MOVIE REQUEST PROCESSOR JOB - KẾT THÚC ║");
            _logger.LogInformation("╚════════════════════════════════════════╝");
        }
    }
}
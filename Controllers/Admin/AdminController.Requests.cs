using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MovieWeb.Models.Entities;
using MovieWeb.Services.Interfaces;
using Hangfire; // Phải thêm để dùng IBackgroundJobClient
using Microsoft.AspNetCore.SignalR; // <-- THÊM DÒNG NÀY
using MovieWeb.Hubs;              // <-- THÊM DÒNG NÀY

// Phải cùng namespace
namespace MovieWeb.Controllers
{
    // Phải là "partial class"
    public partial class AdminController : Controller
    {
        // GET: /Admin/MovieRequests
        [HttpGet]
        public async Task<IActionResult> MovieRequests(string status = "all", int page = 1, int pageSize = 20)
        {
            if (!await IsAdminAsync())
            {
                return Forbid();
            }

            try
            {
                // === ADDED: Tính toán số lượng cho từng status ===
                var allRequestsQuery = _context.RequestsMovies.AsQueryable();
                var counts = new
                {
                    Pending = await allRequestsQuery.CountAsync(r => r.Status == RequestStatus.Pending),
                    Processing = await allRequestsQuery.CountAsync(r => r.Status == RequestStatus.Processing),
                    NeedsVerification = await allRequestsQuery.CountAsync(r => r.Status == RequestStatus.NeedsVerification),
                    Completed = await allRequestsQuery.CountAsync(r => r.Status == RequestStatus.Completed),
                    Total = await allRequestsQuery.CountAsync()
                };
                ViewBag.RequestCounts = counts; // Gửi qua ViewBag
                                                // =================================================

                var query = _context.RequestsMovies
                                  .Include(r => r.Movie)
                                  .AsQueryable();

                // Filter by status (Giữ nguyên)
                if (!string.IsNullOrEmpty(status) && status != "all")
                {
                    query = query.Where(r => r.Status == status);
                    ViewBag.Status = status;
                }

                var totalRequests = await query.CountAsync(); // Total này là total *sau khi lọc*
                var requests = await query
                    .OrderByDescending(r => r.CreatedAt)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                ViewBag.TotalPages = (int)Math.Ceiling((double)totalRequests / pageSize);
                ViewBag.CurrentPage = page;
                ViewBag.PageSize = pageSize;
                ViewBag.TotalRequests = totalRequests; // Giữ lại để hiển thị số lượng trong bảng

                ViewBag.StatusOptions = new List<string> {
            RequestStatus.Pending,
            RequestStatus.Processing,
            RequestStatus.NeedsVerification,
            RequestStatus.Completed
        };

                return View("MovieRequests", requests);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading movie requests page");
                TempData["ErrorMessage"] = "Có lỗi xảy ra khi tải trang yêu cầu phim";
                return RedirectToAction("Dashboard");
            }
        }
        // POST: /Admin/SetRequestStatus
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetRequestStatus(int id, string status, string? adminNote)
        {
            if (!await IsAdminAsync())
            {
                return Json(new { success = false, message = "Không có quyền truy cập" });
            }

            try
            {
                var request = await _context.RequestsMovies.FindAsync(id);
                if (request == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy yêu cầu" });
                }

                // Chỉ cho phép set các status thủ công
                var validStatuses = new[] {
                    RequestStatus.Processing,       // "Nhận xử lý"
                    RequestStatus.NeedsVerification, // "Cần xác minh"
                    RequestStatus.Pending           // "Trả lại" (nếu lỡ bấm)
                };

                if (!validStatuses.Contains(status))
                {
                    return Json(new { success = false, message = "Trạng thái không hợp lệ" });
                }

                request.Status = status;
                if (!string.IsNullOrEmpty(adminNote))
                {
                    request.AdminNote = adminNote;
                }
                else
                {
                    var currentUser = await _authService.GetCurrentUserAsync();
                    request.AdminNote = $"Admin (ID: {currentUser?.Id}) cập nhật status: {status}";
                }

                await _context.SaveChangesAsync();

                await LogAdminActionAsync("SET_REQUEST_STATUS", $"Changed request {request.MovieTitle} (ID: {id}) status to {status}", id.ToString());

                return Json(new { success = true, message = $"Đã cập nhật trạng thái thành '{status}'" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting request status for ID: {Id}", id);
                return Json(new { success = false, message = "Có lỗi xảy ra" });
            }
        }

        /// <summary>
        /// Action (AJAX) để admin "Hoàn tất" (Thực hiện "Lưu ý")
        /// </summary>
        // POST: /Admin/LinkMovieToRequest
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> LinkMovieToRequest(int requestId, int movieId)
        {
            if (!await IsAdminAsync())
            {
                return Json(new { success = false, message = "Không có quyền truy cập" });
            }

            try
            {
                var request = await _context.RequestsMovies
                                            .Include(r => r.UserRequests)
                                            .FirstOrDefaultAsync(r => r.Id == requestId);
                if (request == null)
                {
                    return Json(new { success = false, message = $"Không tìm thấy yêu cầu (ID: {requestId})" });
                }

                var movie = await _context.Movies.FirstOrDefaultAsync(m => m.MovieId == movieId);
                if (movie == null)
                {
                    return Json(new { success = false, message = $"Không tìm thấy phim (ID: {movieId})" });
                }

                // Cập nhật request
                request.Status = RequestStatus.Completed;
                request.MovieId = movie.MovieId;
                request.CompletedAt = DateTime.Now;
                var currentUser = await _authService.GetCurrentUserAsync();
                request.AdminNote = $"Hoàn tất thủ công bởi Admin (ID: {currentUser?.Id}). Đã liên kết với Phim ID: {movieId}.";

                await _context.SaveChangesAsync();

                _logger.LogInformation("✅ Admin {AdminId} completed request {RequestId} -> Movie {MovieId}",
                    currentUser?.Id, requestId, movieId);
                await LogAdminActionAsync("COMPLETE_MOVIE_REQUEST",
                    $"Linked request {request.MovieTitle} (ID: {requestId}) to Movie {movie.Name} (ID: {movieId})",
                    requestId.ToString());

                // ========== GỬI THÔNG BÁO CHO TỪNG USER ==========
                foreach (var userRequest in request.UserRequests)
                {
                    // 1. Lưu vào DB qua Hangfire
                    _backgroundJobClient.Enqueue<INotificationService>(
                        service => service.CreateMovieRequestCompletedAsync(
                            userRequest.UserId,
                            movie.MovieId,
                            movie.Name
                        )
                    );

                    // ========== 2. GỬI SIGNALR - FIXED FORMAT ==========
                    try
                    {
                        // ✅ TẠO DTO ĐÚNG FORMAT (PascalCase - C# convention)
                        var notificationDto = new
                        {
                            NotificationId = 0, // ID tạm - chưa lưu DB
                            Title = "Phim yêu cầu đã có sẵn! 🎬",
                            Content = $"Phim '{movie.Name}' bạn yêu cầu đã được thêm vào hệ thống. Xem ngay!",
                            Type = "MovieRequestSuccess", // ✅ FIX: Type mới
                            Url = !string.IsNullOrEmpty(movie.Slug)
                                  ? $"/phim/{movie.Slug}"
                                  : $"/Movie/Details/{movie.MovieId}",
                            IsRead = false,
                            CreatedAt = DateTime.UtcNow
                        };

                        string userIdAsString = userRequest.UserId.ToString();

                        // ✅ GỬI DTO ĐÃ FORMAT
                        await _notificationHubContext.Clients
                            .User(userIdAsString)
                            .SendAsync("ReceiveNotification", notificationDto);

                        _logger.LogInformation("✅ [SignalR] Sent notification to User {UserId}", userRequest.UserId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "❌ [SignalR] Error sending to User {UserId}", userRequest.UserId);
                    }
                }

                return Json(new { success = true, message = "Đã liên kết phim và gửi thông báo!" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error linking Movie {MovieId} to Request {RequestId}", movieId, requestId);
                return Json(new { success = false, message = "Có lỗi xảy ra khi liên kết phim" });
            }
        }
        [HttpGet("/api/movies/search")]
        public async Task<IActionResult> SearchMoviesApi(string term)
        {
            if (!await IsAdminAsync())
            {
                return Json(new { success = false, message = "Không có quyền truy cập" });
            }

            try
            {
                if (string.IsNullOrWhiteSpace(term) || term.Length < 2)
                {
                    return Json(new { success = false, message = "Vui lòng nhập ít nhất 2 ký tự" });
                }

                // Tìm kiếm phim không phân biệt dấu và hoa thường
                var searchTerm = $"%{term.Trim()}%";
                const string collation = "SQL_Latin1_General_CP1_CI_AI";

                var movies = await _context.Movies
                    .Where(m => m.IsActive == true)
                    .Where(m =>
                        EF.Functions.Like(EF.Functions.Collate(m.Name, collation), searchTerm) ||
                        EF.Functions.Like(EF.Functions.Collate(m.OriginalName, collation), searchTerm)
                    )
                    .OrderByDescending(m => m.Year)
                    .Take(20) // Giới hạn 20 kết quả
                    .Select(m => new
                    {
                        movieId = m.MovieId,
                        name = m.Name,
                        originalName = m.OriginalName,
                        year = m.Year,
                        posterUrl = m.PosterUrl,
                        slug = m.Slug
                    })
                    .ToListAsync();

                return Json(new { success = true, data = movies });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching movies with term: {Term}", term);
                return Json(new { success = false, message = "Có lỗi xảy ra khi tìm kiếm phim" });
            }
        }
        // ===================================================================
        // THÊM VÀO FILE AdminController.cs (partial class)
        // Paste vào cuối file, trước dấu }
        // ===================================================================

        /// <summary>
        /// Action mới: Tự động tìm và sync phim từ OPhim API
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AutoSyncMovieRequest(int requestId)
        {
            if (!await IsAdminAsync())
            {
                return Json(new { success = false, message = "Không có quyền truy cập" });
            }

            try
            {
                // 1. Lấy request
                var request = await _context.RequestsMovies.FindAsync(requestId);
                if (request == null)
                {
                    return Json(new { success = false, message = $"Không tìm thấy yêu cầu (ID: {requestId})" });
                }

                if (string.IsNullOrEmpty(request.MovieTitle))
                {
                    return Json(new { success = false, message = "Yêu cầu không có tên phim" });
                }

                _logger.LogInformation("🤖 [AUTO SYNC] Bắt đầu tìm phim: {Title} ({Year})", request.MovieTitle, request.MovieYear);

                // 2. Tìm kiếm trên OPhim API
                var searchResults = await _oPhimService.SearchMoviesAsync(request.MovieTitle, 1);

                if (searchResults?.Data?.Items == null || !searchResults.Data.Items.Any())
                {
                    _logger.LogWarning("❌ [AUTO SYNC] Không tìm thấy kết quả nào");
                    return Json(new
                    {
                        success = false,
                        message = "Không tìm thấy phim nào trên OPhim"
                    });
                }

                // 3. Lọc theo năm (nếu có)
                var matchedMovies = searchResults.Data.Items.AsEnumerable();

                if (request.MovieYear.HasValue && request.MovieYear > 0)
                {
                    matchedMovies = matchedMovies.Where(m => m.Year == request.MovieYear);
                }

                var movieList = matchedMovies.ToList();

                if (!movieList.Any())
                {
                    _logger.LogWarning("❌ [AUTO SYNC] Không có phim nào khớp năm {Year}", request.MovieYear);
                    return Json(new
                    {
                        success = false,
                        message = $"Không tìm thấy phim '{request.MovieTitle}' năm {request.MovieYear}"
                    });
                }

                // 4. Nếu có nhiều kết quả → trả về danh sách để admin chọn
                if (movieList.Count > 1)
                {
                    _logger.LogInformation("🔍 [AUTO SYNC] Tìm thấy {Count} kết quả, cần admin chọn", movieList.Count);

                    var options = movieList.Select(m => new
                    {
                        slug = m.Slug,
                        name = m.Name,
                        originalName = m.OriginName,
                        year = m.Year,
                        posterUrl = m.PosterUrl,
                        type = m.Type
                    }).ToList();

                    return Json(new
                    {
                        success = true,
                        needsSelection = true,
                        options = options
                    });
                }

                // 5. Nếu chỉ có 1 kết quả → tự động sync luôn
                var selectedMovie = movieList.First();
                return await ExecuteAutoSyncAsync(requestId, selectedMovie.Slug);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ [AUTO SYNC] Lỗi khi tự động sync Request {RequestId}", requestId);
                return Json(new
                {
                    success = false,
                    message = $"Có lỗi xảy ra: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Action mới: Thực hiện sync sau khi admin đã chọn phim
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmAutoSync(int requestId, string slug)
        {
            if (!await IsAdminAsync())
            {
                return Json(new { success = false, message = "Không có quyền truy cập" });
            }

            if (string.IsNullOrEmpty(slug))
            {
                return Json(new { success = false, message = "Slug không hợp lệ" });
            }

            return await ExecuteAutoSyncAsync(requestId, slug);
        }

        /// <summary>
        /// Helper: Thực hiện sync phim từ OPhim vào DB
        /// </summary>
        private async Task<IActionResult> ExecuteAutoSyncAsync(int requestId, string slug)
        {
            try
            {
                var request = await _context.RequestsMovies.FindAsync(requestId);
                if (request == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy yêu cầu" });
                }

                _logger.LogInformation("⚙️ [AUTO SYNC] Bắt đầu đồng bộ slug: {Slug}", slug);

                // 1. Kiểm tra phim đã tồn tại chưa
                var existingMovie = await _context.Movies
                    .AsNoTracking()
                    .FirstOrDefaultAsync(m => m.Slug == slug);

                if (existingMovie != null)
                {
                    _logger.LogInformation("✅ [AUTO SYNC] Phim đã tồn tại (ID: {MovieId}), chỉ cập nhật request", existingMovie.MovieId);

                    // Phim đã có → chỉ cập nhật request
                    request.OphimSlug = slug;
                    request.MovieId = existingMovie.MovieId;
                    request.Status = RequestStatus.NeedsVerification;
                    request.AdminNote = $"Tự động tìm thấy phim đã có: {existingMovie.Name}";

                    await _context.SaveChangesAsync();

                    return Json(new
                    {
                        success = true,
                        message = "Phim đã tồn tại, đã liên kết với request",
                        movieId = existingMovie.MovieId,
                        movieName = existingMovie.Name
                    });
                }

                // 2. Lấy chi tiết phim từ OPhim
                var movieDetail = await _oPhimService.GetMovieDetailAsync(slug);

                if (movieDetail?.Item == null)
                {
                    _logger.LogWarning("❌ [AUTO SYNC] Không lấy được chi tiết phim từ slug: {Slug}", slug);
                    return Json(new
                    {
                        success = false,
                        message = "Không thể lấy thông tin chi tiết phim từ OPhim"
                    });
                }

                _logger.LogInformation("📦 [AUTO SYNC] Đã lấy chi tiết: {Name}", movieDetail.Item.Name);

                // 3. Cập nhật OphimSlug vào request trước khi sync
                request.OphimSlug = slug;
                request.AdminNote = $"Đang tự động đồng bộ: {movieDetail.Item.Name}...";
                await _context.SaveChangesAsync();

                // 4. Sync phim vào DB qua MovieSyncService
                var apiMovies = new List<MovieWeb.Models.API.Movie> { movieDetail.Item };
                await _movieSyncService.SyncMoviesFromApiToDbAsync(apiMovies, 0);

                _logger.LogInformation("💾 [AUTO SYNC] Đã sync vào DB");

                // 5. Lấy lại phim từ DB (vì MovieSyncService vừa thêm)
                var syncedMovie = await _context.Movies
                    .AsNoTracking()
                    .FirstOrDefaultAsync(m => m.Slug == slug);

                if (syncedMovie == null)
                {
                    _logger.LogError("❌ [AUTO SYNC] Lỗi nghiêm trọng: Phim không tìm thấy sau khi sync");
                    return Json(new
                    {
                        success = false,
                        message = "Lỗi: Không thể tìm thấy phim sau khi đồng bộ"
                    });
                }

                // 6. Cập nhật request với trạng thái "Cần xác minh"
                request.Status = RequestStatus.NeedsVerification;
                request.MovieId = syncedMovie.MovieId;
                request.AdminNote = $"✅ Tự động đồng bộ thành công: {syncedMovie.Name} (ID: {syncedMovie.MovieId})";

                await _context.SaveChangesAsync();

                await LogAdminActionAsync(
                    "AUTO_SYNC_MOVIE_REQUEST",
                    $"Auto synced: {syncedMovie.Name} (Slug: {slug}) -> Request ID: {requestId}",
                    requestId.ToString()
                );

                _logger.LogInformation("✅ [AUTO SYNC] Hoàn tất: Request {RequestId} -> Movie {MovieId}",
                    requestId, syncedMovie.MovieId);

                return Json(new
                {
                    success = true,
                    message = $"Đã đồng bộ phim thành công: {syncedMovie.Name}",
                    movieId = syncedMovie.MovieId,
                    movieName = syncedMovie.Name
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ [AUTO SYNC] Lỗi trong ExecuteAutoSyncAsync");
                return Json(new
                {
                    success = false,
                    message = $"Có lỗi xảy ra: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Action mới: Xác nhận request (chuyển từ NeedsVerification -> Completed)
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmMovieRequest(int requestId)
        {
            if (!await IsAdminAsync())
            {
                return Json(new { success = false, message = "Không có quyền truy cập" });
            }

            try
            {
                var request = await _context.RequestsMovies
                    .Include(r => r.Movie)
                    .FirstOrDefaultAsync(r => r.Id == requestId);

                if (request == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy yêu cầu" });
                }

                if (request.Status != RequestStatus.NeedsVerification)
                {
                    return Json(new
                    {
                        success = false,
                        message = $"Request phải ở trạng thái 'Cần xác minh' (hiện tại: {request.Status})"
                    });
                }

                if (!request.MovieId.HasValue)
                {
                    return Json(new
                    {
                        success = false,
                        message = "Request chưa được liên kết với phim nào"
                    });
                }

                // Cập nhật status
                request.Status = RequestStatus.Completed;
                request.CompletedAt = DateTime.Now;

                var currentUser = await _authService.GetCurrentUserAsync();
                request.AdminNote = $"✅ Admin (ID: {currentUser?.Id}) đã xác nhận phim hợp lệ. Chờ gửi thông báo.";

                await _context.SaveChangesAsync();

                await LogAdminActionAsync(
                    "CONFIRM_MOVIE_REQUEST",
                    $"Confirmed request {request.MovieTitle} (ID: {requestId}) as completed",
                    requestId.ToString()
                );

                _logger.LogInformation("✅ Admin confirmed Request {RequestId} -> Status: Completed", requestId);

                return Json(new
                {
                    success = true,
                    message = "Đã xác nhận phim hợp lệ. Bạn có thể gửi thông báo cho users."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error confirming request {RequestId}", requestId);
                return Json(new
                {
                    success = false,
                    message = $"Có lỗi xảy ra: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Action mới: Gửi thông báo cho users (tách riêng khỏi việc hoàn tất)
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendMovieNotification(int requestId)
        {
            if (!await IsAdminAsync())
            {
                return Json(new { success = false, message = "Không có quyền truy cập" });
            }

            try
            {
                var request = await _context.RequestsMovies
                    .Include(r => r.Movie)
                    .Include(r => r.UserRequests)
                    .FirstOrDefaultAsync(r => r.Id == requestId);

                if (request == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy yêu cầu" });
                }

                if (request.Status != RequestStatus.Completed)
                {
                    return Json(new
                    {
                        success = false,
                        message = "Chỉ có thể gửi thông báo cho request đã 'Hoàn tất'"
                    });
                }

                if (!request.MovieId.HasValue || request.Movie == null)
                {
                    return Json(new
                    {
                        success = false,
                        message = "Request chưa được liên kết với phim"
                    });
                }

                if (!request.UserRequests.Any())
                {
                    return Json(new
                    {
                        success = false,
                        message = "Không có user nào yêu cầu phim này"
                    });
                }

                _logger.LogInformation("📧 [NOTIFICATION] Bắt đầu gửi thông báo cho {Count} users",
                    request.UserRequests.Count);

                var movie = request.Movie;
                int successCount = 0;

                // Gửi thông báo cho từng user
                foreach (var userRequest in request.UserRequests)
                {
                    try
                    {
                        // 1. Lưu vào DB qua Hangfire
                        _backgroundJobClient.Enqueue<INotificationService>(
                            service => service.CreateMovieRequestCompletedAsync(
                                userRequest.UserId,
                                movie.MovieId,
                                movie.Name
                            )
                        );

                        // 2. Gửi SignalR real-time
                        var notificationDto = new
                        {
                            NotificationId = 0,
                            Title = "Phim yêu cầu đã có sẵn! 🎬",
                            Content = $"Phim '{movie.Name}' bạn yêu cầu đã được thêm vào hệ thống. Xem ngay!",
                            Type = "MovieRequestSuccess",
                            Url = !string.IsNullOrEmpty(movie.Slug)
                                  ? $"/phim/{movie.Slug}"
                                  : $"/Movie/Details/{movie.MovieId}",
                            IsRead = false,
                            CreatedAt = DateTime.UtcNow
                        };

                        string userIdAsString = userRequest.UserId.ToString();

                        await _notificationHubContext.Clients
                            .User(userIdAsString)
                            .SendAsync("ReceiveNotification", notificationDto);

                        successCount++;
                        _logger.LogInformation("✅ [NOTIFICATION] Sent to User {UserId}", userRequest.UserId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "❌ [NOTIFICATION] Error sending to User {UserId}", userRequest.UserId);
                    }
                }

                // Cập nhật note
                var currentUser = await _authService.GetCurrentUserAsync();
                request.AdminNote = $"✅ Admin (ID: {currentUser?.Id}) đã gửi thông báo cho {successCount}/{request.UserRequests.Count} users.";
                await _context.SaveChangesAsync();

                await LogAdminActionAsync(
                    "SEND_MOVIE_NOTIFICATION",
                    $"Sent notifications for request {request.MovieTitle} (ID: {requestId}) to {successCount} users",
                    requestId.ToString()
                );

                return Json(new
                {
                    success = true,
                    message = $"Đã gửi thông báo cho {successCount}/{request.UserRequests.Count} users"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error sending notifications for request {RequestId}", requestId);
                return Json(new
                {
                    success = false,
                    message = $"Có lỗi xảy ra: {ex.Message}"
                });
            }
        }
    }
}
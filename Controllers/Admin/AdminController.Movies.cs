using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MovieWeb.Models.DTOs;
using MovieWeb.Models.Entities;
using MovieWeb.Helpers; // Cần cho MovieHelper
using MovieWeb.Services;
using Hangfire; // Cần cho BackgroundJob

// Phải cùng namespace
namespace MovieWeb.Controllers
{
    // Phải là "partial class"
    public partial class AdminController : Controller
    {
        // GET: /Admin/Movies
        public async Task<IActionResult> Movies(string? search, string? type, bool? isActive, bool? isManual, bool? isBanner, int page = 1, int pageSize = 20)
        {
            if (!await IsAdminAsync())
            {
                return Forbid();
            }

            try
            {
                var query = _context.Movies
                    .Include(m => m.Categories)
                    .Include(m => m.Countries)
                    .AsQueryable();

                // Search
                if (!string.IsNullOrWhiteSpace(search))
                {
                    query = query.Where(m =>
                        m.Name.Contains(search) ||
                        (m.OriginalName != null && m.OriginalName.Contains(search)) ||
                        m.Slug.Contains(search));
                    ViewBag.Search = search;
                }

                // Filter by type
                if (!string.IsNullOrWhiteSpace(type))
                {
                    query = query.Where(m => m.Type == type);
                    ViewBag.Type = type;
                }

                // Filter by active status
                if (isActive.HasValue)
                {
                    query = query.Where(m => m.IsActive == isActive.Value);
                    ViewBag.IsActive = isActive.Value;
                }

                // Filter by manual/API source
                if (isManual.HasValue)
                {
                    if (isManual.Value)
                        query = query.Where(m => m.ApiId == null);
                    else
                        query = query.Where(m => m.ApiId != null);
                    ViewBag.IsManual = isManual.Value;
                }

                // Filter banner
                if (isBanner.HasValue)
                {
                    query = query.Where(m => m.IsBanner == isBanner.Value);
                    ViewBag.IsBanner = isBanner.Value;
                }

                var totalMovies = await query.CountAsync();
                var movieList = await query
                    .OrderByDescending(m => m.CreatedAt)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                var movies = new List<AdminMovieListDto>();
                foreach (var movie in movieList)
                {
                    var movieDto = new AdminMovieListDto
                    {
                        MovieId = movie.MovieId,
                        Slug = movie.Slug,
                        Name = movie.Name,
                        OriginalName = movie.OriginalName,
                        PosterUrl = movie.PosterUrl,
                        Type = movie.Type,
                        Status = movie.Status,
                        Quality = movie.Quality,
                        Year = movie.Year,
                        ViewCount = movie.ViewCount ?? 0,
                        Rating = movie.Rating ?? 0,
                        RatingCount = movie.RatingCount ?? 0,
                        IsRecommended = movie.IsRecommended ?? false,
                        IsBanner = movie.IsBanner ?? false,
                        EpisodeCurrent = movie.EpisodeCurrent,
                        EpisodeTotal = movie.EpisodeTotal,
                        IsActive = movie.IsActive ?? true,
                        IsManual = string.IsNullOrEmpty(movie.ApiId),
                        CreatedAt = movie.CreatedAt ?? DateTime.MinValue,
                        UpdatedAt = movie.UpdatedAt ?? DateTime.MinValue,
                        Categories = movie.Categories.Select(c => c.Name).ToList(),
                        Countries = movie.Countries.Select(c => c.Name).ToList()
                    };
                    movies.Add(movieDto);
                }

                ViewBag.TotalPages = (int)Math.Ceiling((double)totalMovies / pageSize);
                ViewBag.CurrentPage = page;
                ViewBag.PageSize = pageSize;
                ViewBag.TotalMovies = await _context.Movies.CountAsync(); // Lấy tổng số để hiển thị card
                ViewBag.ActiveMovies = await _context.Movies.CountAsync(m => m.IsActive == true);
                ViewBag.ManualMovies = await _context.Movies.CountAsync(m => m.ApiId == null);
                ViewBag.ApiMovies = await _context.Movies.CountAsync(m => m.ApiId != null);

                return View("Movies", movies);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading movies list");
                TempData["ErrorMessage"] = "Có lỗi xảy ra khi tải danh sách phim";
                return RedirectToAction("Dashboard");
            }
        }

        // GET: /Admin/GetMovieData/{id}
        [HttpGet]
        public async Task<IActionResult> GetMovieData(int id)
        {
            if (!await IsAdminAsync())
            {
                return Json(new { success = false, message = "Không có quyền truy cập" });
            }

            try
            {
                var movie = await _context.Movies
                    .Include(m => m.Categories)
                    .Include(m => m.Countries)
                    .Include(m => m.Actors)
                    .Include(m => m.Directors)
                    .FirstOrDefaultAsync(m => m.MovieId == id);

                if (movie == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy phim" });
                }

                return Json(new
                {
                    success = true,
                    data = new
                    {
                        movieId = movie.MovieId,
                        slug = movie.Slug,
                        name = movie.Name,
                        originalName = movie.OriginalName,
                        description = movie.Description,
                        type = movie.Type,
                        status = movie.Status,
                        posterUrl = movie.PosterUrl,
                        thumbUrl = movie.ThumbUrl,
                        trailerUrl = movie.TrailerUrl,
                        time = movie.Time,
                        episodeCurrent = movie.EpisodeCurrent,
                        episodeTotal = movie.EpisodeTotal,
                        quality = movie.Quality,
                        language = movie.Language,
                        year = movie.Year,
                        isRecommended = movie.IsRecommended,
                        isActive = movie.IsActive,
                        categoryNames = string.Join(", ", movie.Categories.Select(c => c.Name)),
                        countryNames = string.Join(", ", movie.Countries.Select(c => c.Name)),
                        actorNames = string.Join(",", movie.Actors.Select(a => a.Name)),
                        directorNames = string.Join(",", movie.Directors.Select(d => d.Name)),
                        apiSlug = movie.ApiId
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting movie data for ID: {MovieId}", id);
                return Json(new { success = false, message = "Có lỗi xảy ra khi tải dữ liệu" });
            }
        }

        // POST: /Admin/CreateMovie
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateMovie(CreateMovieDto model)
        {
            if (!await IsAdminAsync())
            {
                return Json(new { success = false, message = "Không có quyền truy cập" });
            }

            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = string.Join(", ", ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage));
                    return Json(new { success = false, message = errors });
                }

                if (await _context.Movies.AnyAsync(m => m.Slug == model.Slug))
                {
                    return Json(new { success = false, message = "Slug đã được sử dụng" });
                }

                var movie = new MovieWeb.Models.Entities.Movie
                {
                    // ApiId = null, // Sẽ được set bên dưới
                    Slug = model.Slug,
                    Name = model.Name,
                    OriginalName = model.OriginalName,
                    Description = model.Description,
                    Content = MovieHelper.GetBannerDescription(model.Description ?? ""),
                    Type = model.Type,
                    Status = model.Status,
                    PosterUrl = string.IsNullOrWhiteSpace(model.PosterUrl)
                        ? null
                        : Path.GetFileName(model.PosterUrl),
                    ThumbUrl = string.IsNullOrWhiteSpace(model.ThumbUrl)
                        ? null
                        : Path.GetFileName(model.ThumbUrl),
                    // TrailerUrl = model.TrailerUrl, // Sẽ được set bên dưới
                    Time = model.Time,
                    EpisodeCurrent = model.EpisodeCurrent,
                    EpisodeTotal = model.EpisodeTotal,
                    Quality = model.Quality,
                    Language = model.Language,
                    Year = model.Year,
                    ViewCount = 0,
                    Rating = 0,
                    RatingCount = 0,
                    IsRecommended = model.IsRecommended,
                    IsBanner = model.IsBanner ?? false,
                    IsActive = model.IsActive,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };

                // ==== LOGIC XỬ LÝ APISLUG ====
                string successMessage = "Tạo phim thành công!";
                bool shouldSyncEpisodes = false;

                if (model.Type == "series" && !string.IsNullOrWhiteSpace(model.ApiSlug))
                {
                    // Đây là phim bộ, muốn sync tự động
                    movie.ApiId = model.ApiSlug; // Lưu ApiSlug vào ApiId
                    movie.TrailerUrl = null; // Phim bộ không có trailerUrl chung
                    successMessage = "Tạo phim thành công! Đã lên lịch đồng bộ các tập phim.";
                    shouldSyncEpisodes = true;
                }
                else
                {
                    // Phim lẻ hoặc phim thủ công
                    movie.ApiId = null;
                    movie.TrailerUrl = model.TrailerUrl;
                }
                // ==== KẾT THÚC LOGIC ====

                _context.Movies.Add(movie);
                await _context.SaveChangesAsync(); // 👈 Lưu để lấy movie.MovieId

                // ==== GỌI HANGFIRE JOB ====
                // Job này sẽ chạy ngầm và lưu vào bảng "Episodes"
                if (shouldSyncEpisodes)
                {
                    _backgroundJobClient.Enqueue<IMovieSyncService>(
                        service => service.SyncMovieFromApiBySlug(movie.ApiId, movie.MovieId));
                    
                    _logger.LogInformation("Enqueued episode sync job for movie: {MovieName} (ID: {MovieId}) with ApiSlug: {ApiSlug}", 
                        movie.Name, movie.MovieId, movie.ApiId);
                }
                // ==== KẾT THÚC ====

                if (!string.IsNullOrWhiteSpace(model.CategoryNames))
                {
                    var categoryNames = model.CategoryNames.Split(',')
                        .Select(n => n.Trim())
                        .Where(n => !string.IsNullOrWhiteSpace(n))
                        .ToList();

                    foreach (var categoryName in categoryNames)
                    {
                        var category = await _context.Categories.FirstOrDefaultAsync(c => c.Name == categoryName);

                        if (category == null)
                        {
                            category = new MovieWeb.Models.Entities.Category
                            {
                                Name = categoryName,
                                Slug = GenerateSlug(categoryName), // Dùng hàm helper GenerateSlug
                                IsActive = true,
                                CreatedAt = DateTime.Now
                            };
                            _context.Categories.Add(category);
                        }
                        movie.Categories.Add(category);
                    }
                }

                // Thêm countries
                if (!string.IsNullOrWhiteSpace(model.CountryNames))
                {
                    var countryNames = model.CountryNames.Split(',')
                        .Select(n => n.Trim())
                        .Where(n => !string.IsNullOrWhiteSpace(n))
                        .ToList();

                    foreach (var countryName in countryNames)
                    {
                        var country = await _context.Countries.FirstOrDefaultAsync(c => c.Name == countryName);

                        if (country == null)
                        {
                            country = new MovieWeb.Models.Entities.Country
                            {
                                Name = countryName,
                                Slug = GenerateSlug(countryName), // Dùng hàm helper GenerateSlug
                                IsActive = true,
                                CreatedAt = DateTime.Now
                            };
                            _context.Countries.Add(country);
                        }
                        movie.Countries.Add(country);
                    }
                }

                // Thêm actors
                if (!string.IsNullOrWhiteSpace(model.ActorNames))
                {
                    var actorNames = model.ActorNames.Split(',')
                        .Select(n => n.Trim())
                        .Where(n => !string.IsNullOrWhiteSpace(n))
                        .ToList();

                    foreach (var actorName in actorNames)
                    {
                        var slug = GenerateSlug(actorName); // Dùng hàm helper
                        var actor = await _context.Actors.FirstOrDefaultAsync(a => a.Slug == slug);

                        if (actor == null)
                        {
                            actor = new MovieWeb.Models.Entities.Actor
                            {
                                Name = actorName,
                                Slug = slug
                            };
                            _context.Actors.Add(actor);
                        }
                        movie.Actors.Add(actor);
                    }
                }

                // Thêm directors
                if (!string.IsNullOrWhiteSpace(model.DirectorNames))
                {
                    var directorNames = model.DirectorNames.Split(',')
                        .Select(n => n.Trim())
                        .Where(n => !string.IsNullOrWhiteSpace(n))
                        .ToList();

                    foreach (var directorName in directorNames)
                    {
                        var slug = GenerateSlug(directorName); // Dùng hàm helper
                        var director = await _context.Directors.FirstOrDefaultAsync(d => d.Slug == slug);

                        if (director == null)
                        {
                            director = new MovieWeb.Models.Entities.Director
                            {
                                Name = directorName,
                                Slug = slug
                            };
                            _context.Directors.Add(director);
                        }
                        movie.Directors.Add(director);
                    }
                }


                await _context.SaveChangesAsync(); // Lưu các quan hệ

                await LogAdminActionAsync("CREATE_MOVIE", $"Created manual movie: {movie.Name}", movie.MovieId.ToString());
                _logger.LogInformation("Admin created new movie: {MovieName}", movie.Name);

                return Json(new { success = true, message = successMessage }); // 👈 Dùng successMessage
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating movie");
                return Json(new { success = false, message = ex.InnerException?.Message ?? ex.Message });
            }
        }

        // POST: /Admin/UpdateMovie
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateMovie(UpdateMovieDto model)
        {
            if (!await IsAdminAsync())
            {
                return Json(new { success = false, message = "Không có quyền truy cập" });
            }

            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = string.Join(", ", ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage));
                    return Json(new { success = false, message = errors });
                }

                var movie = await _context.Movies
                    .Include(m => m.Categories)
                    .Include(m => m.Countries)
                    .Include(m => m.Actors)
                    .Include(m => m.Directors)
                    .FirstOrDefaultAsync(m => m.MovieId == model.MovieId);

                if (movie == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy phim" });
                }

                if (await _context.Movies.AnyAsync(m => m.Slug == model.Slug && m.MovieId != model.MovieId))
                {
                    return Json(new { success = false, message = "Slug đã được sử dụng bởi phim khác" });
                }

                // ==== LƯU LẠI APIID CŨ ====
                var oldApiId = movie.ApiId;
                string successMessage = "Cập nhật phim thành công!";
                bool shouldSyncEpisodes = false;
                // ==== KẾT THÚC ====

                // Cập nhật thông tin cơ bản
                movie.Slug = model.Slug;
                movie.Name = model.Name;
                movie.OriginalName = model.OriginalName;
                movie.Description = model.Description;
                movie.Content = MovieHelper.GetBannerDescription(model.Description ?? "");
                movie.Type = model.Type;
                movie.Status = model.Status;
                movie.PosterUrl = model.PosterUrl;
                movie.ThumbUrl = model.ThumbUrl;
                // movie.TrailerUrl = model.TrailerUrl; // Sẽ set bên dưới
                movie.Time = model.Time;
                movie.EpisodeCurrent = model.EpisodeCurrent;
                movie.EpisodeTotal = model.EpisodeTotal;
                movie.Quality = model.Quality;
                movie.Language = model.Language;
                movie.Year = model.Year;
                movie.IsRecommended = model.IsRecommended;
                movie.IsActive = model.IsActive;
                movie.UpdatedAt = DateTime.Now;

                // ==== LOGIC XỬ LÝ APISLUG KHI UPDATE ====
                if (model.Type == "series" && !string.IsNullOrWhiteSpace(model.ApiSlug))
                {
                    // Đây là phim bộ, có ApiSlug
                    movie.ApiId = model.ApiSlug;
                    movie.TrailerUrl = null; // Phim bộ không có trailerUrl chung

                    // Chỉ sync khi ApiSlug thay đổi (hoặc mới được thêm vào)
                    if (movie.ApiId != oldApiId)
                    {
                        shouldSyncEpisodes = true;
                        successMessage = "Cập nhật phim thành công! Đã lên lịch đồng bộ lại các tập phim.";
                        _logger.LogInformation("ApiSlug changed for movie ID {MovieId}. Old: '{OldSlug}', New: '{NewSlug}'. Enqueuing sync.",
                            movie.MovieId, oldApiId, movie.ApiId);
                    }
                }
                else
                {
                    // Phim lẻ hoặc phim thủ công (hoặc admin xóa ApiSlug)
                    movie.ApiId = null;
                    movie.TrailerUrl = model.TrailerUrl;
                }
                // ==== KẾT THÚC LOGIC ====

                // Cập nhật categories
                movie.Categories.Clear();
                if (!string.IsNullOrWhiteSpace(model.CategoryNames))
                {
                    var categoryNames = model.CategoryNames.Split(',')
                        .Select(n => n.Trim())
                        .Where(n => !string.IsNullOrWhiteSpace(n))
                        .ToList();
                    
                    foreach (var categoryName in categoryNames)
                    {
                        var category = await _context.Categories.FirstOrDefaultAsync(c => c.Name == categoryName);
                        if (category == null)
                        {
                            category = new MovieWeb.Models.Entities.Category
                            {
                                Name = categoryName,
                                Slug = GenerateSlug(categoryName),
                                IsActive = true,
                                CreatedAt = DateTime.Now
                            };
                            _context.Categories.Add(category);
                        }
                        movie.Categories.Add(category);
                    }
                }

                // Cập nhật countries
                movie.Countries.Clear();
                if (!string.IsNullOrWhiteSpace(model.CountryNames))
                {
                    var countryNames = model.CountryNames.Split(',')
                        .Select(n => n.Trim())
                        .Where(n => !string.IsNullOrWhiteSpace(n))
                        .ToList();

                    foreach (var countryName in countryNames)
                    {
                        var country = await _context.Countries.FirstOrDefaultAsync(c => c.Name == countryName);
                        if (country == null)
                        {
                            country = new MovieWeb.Models.Entities.Country
                            {
                                Name = countryName,
                                Slug = GenerateSlug(countryName),
                                IsActive = true,
                                CreatedAt = DateTime.Now
                            };
                            _context.Countries.Add(country);
                        }
                        movie.Countries.Add(country);
                    }
                }

                // Cập nhật actors
                movie.Actors.Clear();
                if (!string.IsNullOrWhiteSpace(model.ActorNames))
                {
                    var actorNames = model.ActorNames.Split(',')
                        .Select(n => n.Trim())
                        .Where(n => !string.IsNullOrWhiteSpace(n))
                        .ToList();
                    foreach (var actorName in actorNames)
                    {
                        var slug = GenerateSlug(actorName);
                        var actor = await _context.Actors.FirstOrDefaultAsync(a => a.Slug == slug);
                        if (actor == null)
                        {
                            actor = new MovieWeb.Models.Entities.Actor { Name = actorName, Slug = slug };
                            _context.Actors.Add(actor);
                        }
                        movie.Actors.Add(actor);
                    }
                }

                // Cập nhật directors
                movie.Directors.Clear();
                if (!string.IsNullOrWhiteSpace(model.DirectorNames))
                {
                    var directorNames = model.DirectorNames.Split(',')
                        .Select(n => n.Trim())
                        .Where(n => !string.IsNullOrWhiteSpace(n))
                        .ToList();
                    foreach (var directorName in directorNames)
                    {
                        var slug = GenerateSlug(directorName);
                        var director = await _context.Directors.FirstOrDefaultAsync(d => d.Slug == slug);
                        if (director == null)
                        {
                            director = new MovieWeb.Models.Entities.Director { Name = directorName, Slug = slug };
                            _context.Directors.Add(director);
                        }
                        movie.Directors.Add(director);
                    }
                }


                await _context.SaveChangesAsync(); // 👈 Lưu thay đổi phim VÀ quan hệ

                // ==== GỌI HANGFIRE JOB (NẾU CẦN) ====
                // Job này sẽ chạy ngầm và lưu vào bảng "Episodes"
                if (shouldSyncEpisodes)
                {
                    // 🚨 TÙY CHỌN: Ông có thể muốn xóa sạch tập cũ trước khi sync lại
                    // var oldEpisodes = _context.Episodes.Where(e => e.MovieId == movie.MovieId);
                    // if (oldEpisodes.Any())
                    // {
                    //    _context.Episodes.RemoveRange(oldEpisodes);
                    //    await _context.SaveChangesAsync(); 
                    //    _logger.LogInformation("Removed old episodes for movie ID {MovieId} before re-syncing.", movie.MovieId);
                    // }
                    // 👆 Bỏ comment phần trên nếu ông muốn xóa sạch tập cũ khi sync lại

                    _backgroundJobClient.Enqueue<IMovieSyncService>(
                        service => service.SyncMovieFromApiBySlug(movie.ApiId, movie.MovieId));
                }
                // ==== KẾT THÚC ====

                await LogAdminActionAsync("UPDATE_MOVIE", $"Updated movie: {movie.Name}", movie.MovieId.ToString());
                _logger.LogInformation("Admin updated movie: {MovieName}", movie.Name);

                return Json(new { success = true, message = successMessage }); // 👈 Dùng successMessage
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating movie");
                return Json(new { success = false, message = "Có lỗi xảy ra khi cập nhật phim" });
            }
        }

        // POST: /Admin/ToggleMovieStatus
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleMovieStatus(int movieId, bool isActive)
        {
            if (!await IsAdminAsync())
            {
                return Json(new { success = false, message = "Không có quyền truy cập" });
            }

            try
            {
                var movie = await _context.Movies.FindAsync(movieId);
                if (movie == null)
                {
                    return Json(new { success = false, message = "Phim không tồn tại" });
                }

                movie.IsActive = isActive;
                movie.UpdatedAt = DateTime.Now;
                await _context.SaveChangesAsync();

                await LogAdminActionAsync("TOGGLE_MOVIE_STATUS",
                    $"Changed movie {movie.Name} status to {(isActive ? "Active" : "Inactive")}",
                    movieId.ToString());

                return Json(new { success = true, message = $"Đã {(isActive ? "kích hoạt" : "ẩn")} phim thành công" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling movie status for ID: {MovieId}", movieId);
                return Json(new { success = false, message = "Có lỗi xảy ra" });
            }
        }

        // POST: /Admin/ToggleMovieRecommended
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleMovieRecommended(int movieId, bool isRecommended)
        {
            if (!await IsAdminAsync())
            {
                return Json(new { success = false, message = "Không có quyền truy cập" });
            }

            try
            {
                var movie = await _context.Movies.FindAsync(movieId);
                if (movie == null)
                {
                    return Json(new { success = false, message = "Phim không tồn tại" });
                }

                movie.IsRecommended = isRecommended;
                movie.UpdatedAt = DateTime.Now;
                await _context.SaveChangesAsync();

                await LogAdminActionAsync("TOGGLE_MOVIE_RECOMMENDED",
                    $"Changed movie {movie.Name} recommended status to {isRecommended}",
                    movieId.ToString());

                return Json(new { success = true, message = $"Đã {(isRecommended ? "đánh dấu" : "bỏ đánh dấu")} phim đề xuất" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling movie recommended for ID: {MovieId}", movieId);
                return Json(new { success = false, message = "Có lỗi xảy ra" });
            }
        }

        // POST: /Admin/DeleteMovie
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteMovie(int movieId)
        {
            if (!await IsAdminAsync())
            {
                return Json(new { success = false, message = "Không có quyền truy cập" });
            }

            try
            {
                var movie = await _context.Movies
                    .Include(m => m.Categories)
                    .Include(m => m.Countries)
                    .Include(m => m.Actors)
                    .Include(m => m.Directors)
                    .FirstOrDefaultAsync(m => m.MovieId == movieId);

                if (movie == null)
                {
                    return Json(new { success = false, message = "Phim không tồn tại" });
                }

                // Kiểm tra dữ liệu liên quan
                var hasRelatedData = await _context.Comments.AnyAsync(c => c.MovieId == movieId) ||
                                     await _context.Favorites.AnyAsync(f => f.MovieId == movieId) ||
                                     await _context.Ratings.AnyAsync(r => r.MovieId == movieId) ||
                                     await _context.WatchHistories.AnyAsync(w => w.MovieId == movieId);

                if (hasRelatedData)
                {
                    return Json(new { success = false, message = "Không thể xóa phim có dữ liệu liên quan. Hãy ẩn phim thay thế." });
                }

                // Xóa relationships
                movie.Categories.Clear();
                movie.Countries.Clear();
                movie.Actors.Clear();
                movie.Directors.Clear();

                _context.Movies.Remove(movie);
                await _context.SaveChangesAsync();

                await LogAdminActionAsync("DELETE_MOVIE", $"Deleted movie: {movie.Name} (ID: {movieId})", movieId.ToString());
                _logger.LogInformation("Admin deleted movie: {MovieName}", movie.Name);

                return Json(new { success = true, message = "Đã xóa phim thành công" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting movie ID: {MovieId}", movieId);
                return Json(new { success = false, message = "Có lỗi xảy ra khi xóa phim" });
            }
        }

        // POST: /Admin/ToggleMovieBanner
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleMovieBanner(int movieId, bool isBanner)
        {
            // 🔒 Kiểm tra quyền admin
            if (!await IsAdminAsync())
            {
                return Json(new
                {
                    success = false,
                    message = "Không có quyền truy cập"
                });
            }

            try
            {
                // 🔍 Tìm phim theo ID
                var movie = await _context.Movies.FindAsync(movieId);
                if (movie == null)
                {
                    return Json(new
                    {
                        success = false,
                        message = "Phim không tồn tại"
                    });
                }

                // ⚠️ Giới hạn tối đa 5 phim được gắn banner
                if (isBanner)
                {
                    var currentBannerCount = await _context.Movies
                        .CountAsync(m => m.IsBanner == true && m.IsActive == true);

                    if (currentBannerCount >= 5)
                    {
                        return Json(new
                        {
                            success = false,
                            message = "Đã đạt giới hạn 5 phim banner. Hãy bỏ banner của phim khác trước."
                        });
                    }
                }

                // ✅ Cập nhật trạng thái banner
                movie.IsBanner = isBanner;

                await _context.SaveChangesAsync();

                // 🧹 Xóa cache banner để tải lại dữ liệu mới
                _cache.Remove("banner_movies_entities");

                // 🧾 Ghi log hành động admin
                await LogAdminActionAsync(
                    "TOGGLE_MOVIE_BANNER",
                    $"Changed movie {movie.Name} banner status to {isBanner}",
                    movieId.ToString()
                );

                // 🎉 Phản hồi thành công
                return Json(new
                {
                    success = true,
                    message = $"Đã {(isBanner ? "thêm vào" : "bỏ khỏi")} banner thành công"
                });
            }
            catch (Exception ex)
            {
                // ❌ Ghi log lỗi
                _logger.LogError(ex, "Error toggling movie banner for ID: {MovieId}", movieId);

                return Json(new
                {
                    success = false,
                    message = "Có lỗi xảy ra khi cập nhật banner"
                });
            }
        }
    }
}
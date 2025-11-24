// NỘI DUNG ĐẦY ĐỦ CỦA: Controllers/MovieController.cs

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MovieWeb.Data;
using MovieWeb.Models.Entities;
using MovieWeb.Repositories; // Giữ nguyên (nếu bạn dùng)
using MovieWeb.Services; // Giữ nguyên
using System.Text.RegularExpressions; // Giữ nguyên
using System.Security.Claims; // Giữ nguyên
using MovieWeb.Models.ViewModels;
using MovieWeb.Services.Interfaces; // Thêm dòng này

namespace MovieWeb.Controllers
{
    public class MovieController : Controller
    {
        private readonly MovieWebDbContext _context;
        private readonly IMovieRepository _movieRepository; // Giữ nguyên
        private readonly IAuthService _authService; // Giữ nguyên

        public MovieController(MovieWebDbContext context, IMovieRepository movieRepository, IAuthService authService)
        {
            _context = context;
            _movieRepository = movieRepository;
            _authService = authService;
        }

        // ============================================================
        // 🎬 TRANG CHI TIẾT PHIM (PHỤC HỒI CODE GỐC)
        // ============================================================
        [Route("phim/{slug}")]
        public async Task<IActionResult> Detail(string slug)
        {
            if (string.IsNullOrEmpty(slug))
                return NotFound();

            var movie = await _context.Movies
                .Include(m => m.Actors)
                .Include(m => m.Categories)
                .Include(m => m.Directors)
                .Include(m => m.Countries)
                .Include(m => m.Episodes)
                .FirstOrDefaultAsync(m => m.Slug == slug && (m.IsActive ?? false));

            if (movie == null)
                return NotFound();

            // 🎯 Gợi ý phim liên quan
            var categoryIds = movie.Categories.Select(c => c.CategoryId).ToList();

            var relatedMovies = await _context.Movies
                .Include(m => m.Categories)
                .Where(m => (m.IsActive ?? false)
                                && m.MovieId != movie.MovieId
                                && m.Categories.Any(c => categoryIds.Contains(c.CategoryId)))
                .OrderByDescending(m => m.ViewCount ?? 0)
                .Take(8)
                .ToListAsync();

            // 👁️ Tăng lượt xem
            movie.ViewCount = (movie.ViewCount ?? 0) + 1;
            await _context.SaveChangesAsync();

            // 🎞️ Lấy danh sách tập phim
            var allEpisodes = await _context.Episodes
                .Where(e => e.MovieId == movie.MovieId)
                .OrderBy(e => e.EpisodeName)
                .ToListAsync();

            // 🧩 Chuẩn hóa server key
            string NormalizeKey(string? s)
            {
                s ??= "Khác";
                s = s.Trim();
                s = Regex.Replace(s, @"\s+", " ");
                return s.ToLowerInvariant();
            }

            string DisplayName(string? s)
            {
                s ??= "Khác";
                s = s.Trim();
                s = Regex.Replace(s, @"\s+", " ");
                return s;
            }

            var serverDisplayNames = new Dictionary<string, string>();
            var groupedByNormalized = new Dictionary<string, List<Episode>>();

            foreach (var ep in allEpisodes)
            {
                var key = NormalizeKey(ep.ServerName);
                var display = DisplayName(ep.ServerName);

                if (!serverDisplayNames.ContainsKey(key))
                    serverDisplayNames[key] = display;

                if (!groupedByNormalized.ContainsKey(key))
                    groupedByNormalized[key] = new List<Episode>();

                groupedByNormalized[key].Add(ep);
            }

            // ⚙️ Server mặc định
            string defaultServerKey = "";
            if (serverDisplayNames.Keys.Any(k => serverDisplayNames[k].Contains("Vietsub", StringComparison.OrdinalIgnoreCase)))
            {
                defaultServerKey = serverDisplayNames.First(x => x.Value.Contains("Vietsub", StringComparison.OrdinalIgnoreCase)).Key;
            }
            else if (serverDisplayNames.Keys.Any(k => serverDisplayNames[k].Contains("Thuyết Minh", StringComparison.OrdinalIgnoreCase)))
            {
                defaultServerKey = serverDisplayNames.First(x => x.Value.Contains("Thuyết Minh", StringComparison.OrdinalIgnoreCase)).Key;
            }
            else
            {
                defaultServerKey = serverDisplayNames.Keys.FirstOrDefault() ?? "";
            }

            ViewBag.GroupedEpisodes = groupedByNormalized;
            ViewBag.ServerDisplayNames = serverDisplayNames;
            ViewBag.Servers = serverDisplayNames.Keys.ToList();
            ViewBag.DefaultServer = defaultServerKey;
            ViewBag.RelatedMovies = relatedMovies;
            ViewData["Title"] = movie.Name;
            ViewData["PageType"] = "Detail";

            // 💎 Kiểm tra gói tài khoản (ẩn quảng cáo nếu Premium / Student)
            var currentUser = await _authService.GetCurrentUserAsync();
            bool shouldShowAds = true;

            if (currentUser != null)
            {
                var subscriptionType = currentUser.SubscriptionType?.ToLower() ?? "free";
                if (subscriptionType == "premium" || subscriptionType == "student")
                    shouldShowAds = false;
            }

            // 🧠 Phân loại phim
            bool isSeriesType = movie.Type?.ToLower() == "series" || movie.Type?.ToLower() == "hoathinh";

            // 🎞️ Lấy tập 1
            string? episode1Url = null;
            if (isSeriesType)
            {
                if (!string.IsNullOrEmpty(movie.TrailerUrl))
                {
                    episode1Url = movie.TrailerUrl;
                }
                else
                {
                    var firstEpisode = allEpisodes
                        .Where(e => e.EpisodeName == "1" || e.Slug == "1")
                        .OrderBy(e => e.EpisodeName)
                        .FirstOrDefault();

                    if (firstEpisode != null)
                        episode1Url = firstEpisode.LinkM3u8;
                }
            }

            // 📢 Quảng cáo
            var advertisements = await _context.Advertisements
                .Where(a => a.IsActive)
                .OrderBy(a => a.DisplayOrder)
                .ToListAsync();

            ViewBag.Advertisements = advertisements;
            ViewBag.ShouldShowAds = shouldShowAds;
            ViewBag.IsSeriesType = isSeriesType;
            ViewBag.Episode1Url = episode1Url;

            return View(movie);
        }

        // ============================================================
        // 🔍 TÌM KIẾM PHIM (ĐÃ SỬA CHO CHỌN NHIỀU)
        // ============================================================
        [HttpGet("tim-kiem")]
        public async Task<IActionResult> Search([FromQuery] string? keyword, [FromQuery] MovieFilterViewModel filters)
        {
            // 🔸 Query cơ bản
            var query = _context.Movies
                .Include(m => m.Categories)
                .Include(m => m.Countries)
                .Include(m => m.Actors)
                .Include(m => m.Directors)
                .Where(m => (m.IsActive ?? false));

            // 1️⃣ Lọc theo Keyword (nếu có)
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                string searchKeyword = keyword.Trim().ToLower();
                query = query.Where(m =>
                    m.Name.ToLower().Contains(searchKeyword) ||
                    (m.OriginalName != null && m.OriginalName.ToLower().Contains(searchKeyword)) ||
                    m.Slug.ToLower().Contains(searchKeyword) ||
                    (m.Description != null && m.Description.ToLower().Contains(searchKeyword)) ||
                    m.Categories.Any(c => c.Name.ToLower().Contains(searchKeyword) || c.Slug.ToLower().Contains(searchKeyword)) ||
                    m.Countries.Any(c => c.Name.ToLower().Contains(searchKeyword) || c.Slug.ToLower().Contains(searchKeyword)) ||
                    m.Actors.Any(a => a.Name.ToLower().Contains(searchKeyword)) ||
                    m.Directors.Any(d => d.Name.ToLower().Contains(searchKeyword))
                );
            }

            // 💡 ===== BẮT ĐẦU LOGIC LỌC MỚI (CHỌN NHIỀU) ===== 💡

            // 2️⃣ Lọc theo Type (Chọn 1)
            if (!string.IsNullOrWhiteSpace(filters.Type))
            {
                query = query.Where(m => m.Type == filters.Type);
            }

            // 3️⃣ Lọc theo Countries (CHỌN NHIỀU - OR)
            if (!string.IsNullOrWhiteSpace(filters.Countries))
            {
                var countryList = filters.Countries.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
                if (countryList.Any())
                {
                    query = query.Where(m => m.Countries.Any(c => countryList.Contains(c.Slug)));
                }
            }

            // 4️⃣ Lọc theo Categories (CHỌN NHIỀU - AND)
            if (!string.IsNullOrWhiteSpace(filters.Categories))
            {
                var categoryList = filters.Categories.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
                if (categoryList.Any())
                {
                    // Phim phải có TẤT CẢ các thể loại được chọn
                    query = query.Where(m => categoryList.All(catSlug => m.Categories.Any(c => c.Slug == catSlug)));
                }
            }

            // 5️⃣ Lọc theo Language (Chọn 1)
            if (!string.IsNullOrWhiteSpace(filters.Language))
            {
                query = query.Where(m => m.Language == filters.Language);
            }

            // 6️⃣ Lọc theo Years (CHỌN NHIỀU - OR)
            if (!string.IsNullOrWhiteSpace(filters.Years))
            {
                // Chuyển chuỗi "2024,2023" thành List<int> { 2024, 2023 }
                var yearList = new List<int>();
                foreach (var yearStr in filters.Years.Split(',', StringSplitOptions.RemoveEmptyEntries))
                {
                    if (int.TryParse(yearStr, out int year))
                    {
                        yearList.Add(year);
                    }
                }

                if (yearList.Any())
                {
                    query = query.Where(m => m.Year.HasValue && yearList.Contains(m.Year.Value));
                }
            }
            // 💡 ===== KẾT THÚC LOGIC LỌC MỚI ===== 💡

            // ===== SẮP XẾP =====
            query = (filters.SortBy?.ToLower()) switch
            {
                "newest" => query.OrderByDescending(m => m.Year).ThenByDescending(m => m.CreatedAt),
                "rating" => query.OrderByDescending(m => m.Rating ?? 0),
                "views" => query.OrderByDescending(m => m.ViewCount ?? 0),
                _ => query.OrderByDescending(m => m.UpdatedAt ?? m.CreatedAt) // Sắp xếp mặc định
            };

            // ===== PHÂN TRANG (Thay thế Take(40)) =====
            int pageSize = filters.PageSize > 0 ? filters.PageSize : 20;
            int page = filters.Page > 0 ? filters.Page : 1;

            int totalMovies = await query.CountAsync();
            int totalPages = (int)Math.Ceiling(totalMovies / (double)pageSize);

            var movies = await query
                .Select(m => new Movie
                {
                    MovieId = m.MovieId,
                    Name = m.Name,
                    OriginalName = m.OriginalName,
                    Slug = m.Slug,
                    ThumbUrl = m.ThumbUrl,
                    PosterUrl = m.PosterUrl,
                    Categories = m.Categories.Select(c => new Category { Name = c.Name }).Take(1).ToList()
                })
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // ===== Lấy data cho Bộ lọc =====
            var countries = await _context.Countries
                .Where(c => c.IsActive == true)
                .OrderBy(c => c.Name)
                .ToListAsync();

            var categories = await _context.Categories
                .Where(c => c.IsActive == true)
                .OrderBy(c => c.Name)
                .ToListAsync();

            // ===== Truyền dữ liệu cho View =====
            ViewBag.Keyword = keyword; // Truyền keyword
            ViewBag.Filters = filters; // Truyền filters
            ViewBag.Countries = countries; // Truyền data
            ViewBag.Categories = categories; // Truyền data
            ViewBag.CurrentPage = page; // Pagination
            ViewBag.TotalPages = totalPages; // Pagination

            ViewData["Title"] = string.IsNullOrWhiteSpace(keyword) ? "Tìm kiếm & Lọc phim" : $"Tìm kiếm: {keyword}";
            ViewData["PageType"] = "Search";

            var advertisements = await _context.Advertisements
                .Where(a => a.IsActive)
                .OrderBy(a => a.DisplayOrder)
                .ToListAsync();

            ViewBag.Advertisements = advertisements;
            return View("Search", movies);
        }

        // ============================================================
        // 💬 LẤY COMMENT + ĐÁNH GIÁ (Không thay đổi)
        // ============================================================
        [HttpGet("api/comments/{movieId}")]
        public async Task<IActionResult> GetComments(int movieId)
        {
            // ... (LOGIC GIỮ NGUYÊN) ...
            var comments = await _context.Comments
               .Include(c => c.User)
               .Where(c => c.MovieId == movieId && (c.IsActive ?? true))
               .OrderByDescending(c => c.CreatedAt)
               .Select(c => new
               {
                   c.CommentId,
                   c.Content,
                   c.CreatedAt,
                   c.Rating,
                   UserName = c.User.UserName
               })
               .ToListAsync();

            double average = comments.Any(c => c.Rating.HasValue)
                ? comments.Where(c => c.Rating.HasValue).Average(c => c.Rating.Value)
                : 0;

            return Json(new { average, comments });
        }

        // ============================================================
        // 🎞️ API LẤY DANH SÁCH TẬP PHIM (Không thay đổi)
        // ============================================================
        [HttpGet("api/episodes/{movieId}")]
        public async Task<IActionResult> GetEpisodes(int movieId, [FromQuery] string? server = null)
        {
            try
            {
                // 1️⃣ Lấy tất cả tập phim của movieId
                var allEpisodes = await _context.Episodes
                    .Where(e => e.MovieId == movieId)
                    .OrderBy(e => e.EpisodeName)
                    .ToListAsync();

                if (!allEpisodes.Any())
                {
                    return Json(new { success = false, message = "Không tìm thấy tập phim nào." });
                }

                // 2️⃣ Chuẩn hóa tên server (loại bỏ khoảng trắng thừa, chuyển thành lowercase)
                string NormalizeServerName(string? serverName)
                {
                    if (string.IsNullOrWhiteSpace(serverName))
                        return "khac";

                    return Regex.Replace(serverName.Trim(), @"\s+", " ").ToLowerInvariant();
                }

                // 3️⃣ Nhóm tập phim theo server
                var groupedByServer = allEpisodes
                    .GroupBy(e => NormalizeServerName(e.ServerName))
                    .ToDictionary(
                        g => g.Key,
                        g => g.OrderBy(e => e.EpisodeName).ToList()
                    );

                // 4️⃣ Nếu không truyền server, trả về tất cả
                if (string.IsNullOrWhiteSpace(server))
                {
                    var result = groupedByServer.Select(kvp => new
                    {
                        serverName = kvp.Key,
                        displayName = kvp.Value.FirstOrDefault()?.ServerName ?? kvp.Key,
                        episodes = kvp.Value.Select(e => new
                        {
                            e.EpisodeId,
                            e.EpisodeName,
                            e.Slug,
                            e.LinkM3u8,
                        }).ToList()
                    }).ToList();

                    return Json(new { success = true, data = result });
                }

                // 5️⃣ Nếu có truyền server, chỉ trả về tập của server đó
                var normalizedServer = NormalizeServerName(server);

                if (!groupedByServer.ContainsKey(normalizedServer))
                {
                    return Json(new { success = false, message = $"Không tìm thấy server: {server}" });
                }

                var episodes = groupedByServer[normalizedServer].Select(e => new
                {
                    e.EpisodeId,
                    e.EpisodeName,
                    e.Slug,
                    e.LinkM3u8,
                    serverName = e.ServerName
                }).ToList();

                return Json(new { success = true, data = episodes });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi khi tải danh sách tập: " + ex.Message });
            }
        }
        [Route("the-loai/phim-moi-cap-nhat")]
        public async Task<IActionResult> PhimMoiCapNhat(
            [FromQuery] string? Countries,
            [FromQuery] string? Categories,
            [FromQuery] string? Years,
            [FromQuery] string? Language,
            [FromQuery] string? SortBy,
            [FromQuery] int Page = 1)
        {
            const int pageSize = 24;

            var query = _context.Movies
                .Include(m => m.Countries)  // ✅ Thêm Include
                .Include(m => m.Categories) // ✅ Thêm Include
                .Where(m => m.IsActive == true);

            // ✅ SỬA: Lọc theo Collection<Country>
            if (!string.IsNullOrEmpty(Countries))
            {
                var countryList = Countries.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
                if (countryList.Any())
                {
                    query = query.Where(m => m.Countries.Any(c => countryList.Contains(c.Slug)));
                }
            }

            // ✅ SỬA: Lọc theo Collection<Category>
            if (!string.IsNullOrEmpty(Categories))
            {
                var categoryList = Categories.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
                if (categoryList.Any())
                {
                    query = query.Where(m => categoryList.All(catSlug => m.Categories.Any(c => c.Slug == catSlug)));
                }
            }

            // ✅ SỬA: Year là int?, không phải string
            if (!string.IsNullOrEmpty(Years))
            {
                var yearList = new List<int>();
                foreach (var yearStr in Years.Split(',', StringSplitOptions.RemoveEmptyEntries))
                {
                    if (int.TryParse(yearStr, out int year))
                    {
                        yearList.Add(year);
                    }
                }
                if (yearList.Any())
                {
                    query = query.Where(m => m.Year.HasValue && yearList.Contains(m.Year.Value));
                }
            }

            if (!string.IsNullOrEmpty(Language))
            {
                query = query.Where(m => m.Language == Language);
            }

            // Sắp xếp theo UpdatedAt (mới nhất lên đầu)
            query = SortBy switch
            {
                "view" => query.OrderByDescending(m => m.ViewCount),
                "year" => query.OrderByDescending(m => m.Year),
                _ => query.OrderByDescending(m => m.UpdatedAt)
            };

            var totalMovies = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalMovies / (double)pageSize);

            var movies = await query
                .Skip((Page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.CategoryName = "Phim Mới Cập Nhật";
            ViewBag.CurrentPage = Page;
            ViewBag.TotalPages = totalPages;
            ViewBag.SeoDescription = "Phim mới cập nhật liên tục, xem phim online miễn phí chất lượng cao.";

            ViewBag.Filters = new MovieFilterViewModel
            {
                Countries = Countries,
                Categories = Categories,
                Years = Years,
                Language = Language,
                SortBy = SortBy
            };

            return View(movies);
        }

        [Route("the-loai/hoat-hinh")]
        public async Task<IActionResult> HoatHinh(
            [FromQuery] string? Countries,
            [FromQuery] string? Categories,
            [FromQuery] string? Years,
            [FromQuery] string? Language,
            [FromQuery] string? SortBy,
            [FromQuery] int Page = 1)
        {
            const int pageSize = 24;

            var query = _context.Movies
                .Include(m => m.Countries)  // ✅ Thêm Include
                .Include(m => m.Categories) // ✅ Thêm Include
                .Where(m => m.IsActive == true && m.Type == "hoathinh");

            // ✅ SỬA: Lọc theo Collection<Country>
            if (!string.IsNullOrEmpty(Countries))
            {
                var countryList = Countries.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
                if (countryList.Any())
                {
                    query = query.Where(m => m.Countries.Any(c => countryList.Contains(c.Slug)));
                }
            }

            // ✅ SỬA: Lọc theo Collection<Category>
            if (!string.IsNullOrEmpty(Categories))
            {
                var categoryList = Categories.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
                if (categoryList.Any())
                {
                    query = query.Where(m => categoryList.All(catSlug => m.Categories.Any(c => c.Slug == catSlug)));
                }
            }

            // ✅ SỬA: Year là int?, không phải string
            if (!string.IsNullOrEmpty(Years))
            {
                var yearList = new List<int>();
                foreach (var yearStr in Years.Split(',', StringSplitOptions.RemoveEmptyEntries))
                {
                    if (int.TryParse(yearStr, out int year))
                    {
                        yearList.Add(year);
                    }
                }
                if (yearList.Any())
                {
                    query = query.Where(m => m.Year.HasValue && yearList.Contains(m.Year.Value));
                }
            }

            if (!string.IsNullOrEmpty(Language))
            {
                query = query.Where(m => m.Language == Language);
            }

            // Sắp xếp
            query = SortBy switch
            {
                "view" => query.OrderByDescending(m => m.ViewCount),
                "year" => query.OrderByDescending(m => m.Year),
                _ => query.OrderByDescending(m => m.UpdatedAt)
            };

            var totalMovies = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalMovies / (double)pageSize);

            var movies = await query
                .Skip((Page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.CategoryName = "Phim Hoạt Hình";
            ViewBag.CurrentPage = Page;
            ViewBag.TotalPages = totalPages;
            ViewBag.SeoDescription = "Xem phim hoạt hình, anime, cartoon online miễn phí.";

            ViewBag.Filters = new MovieFilterViewModel
            {
                Countries = Countries,
                Categories = Categories,
                Years = Years,
                Language = Language,
                SortBy = SortBy
            };

            return View(movies);
        }
        // ============================================================
        // 🎬 DANH SÁCH PHIM LẺ (ĐÃ SỬA CHO CHỌN NHIỀU)
        // ============================================================
        [Route("the-loai/phim-le")]
        public async Task<IActionResult> PhimLe([FromQuery] MovieFilterViewModel filters)
        {
            int pageSize = filters.PageSize > 0 ? filters.PageSize : 20;
            int page = filters.Page > 0 ? filters.Page : 1;

            // 🔸 Query cơ bản
            var query = _context.Movies
                .Include(m => m.Countries)
                .Include(m => m.Categories) // 💡 Thêm Include
                .Where(m => (m.IsActive ?? false) &&
                               (m.Type != null && (
                                   m.Type.ToLower() == "single" ||
                                   m.Type.ToLower() == "phimle" ||
                                   m.Type.ToLower() == "phim-le" ||
                                   m.Type.ToLower() == "phim lẻ")));

            // 💡 ===== BẮT ĐẦU LOGIC LỌC MỚI (CHỌN NHIỀU) ===== 💡

            // 1️⃣ Lọc theo Type (nếu người dùng muốn chọn loại khác)
            if (!string.IsNullOrWhiteSpace(filters.Type) && filters.Type != "single")
            {
                query = query.Where(m => m.Type == filters.Type);
            }

            // 2️⃣ Lọc theo Countries (CHỌN NHIỀU - OR)
            if (!string.IsNullOrWhiteSpace(filters.Countries))
            {
                var countryList = filters.Countries.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
                if (countryList.Any())
                {
                    query = query.Where(m => m.Countries.Any(c => countryList.Contains(c.Slug)));
                }
            }

            // 3️⃣ Lọc theo Categories (CHỌN NHIỀU - AND)
            if (!string.IsNullOrWhiteSpace(filters.Categories))
            {
                var categoryList = filters.Categories.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
                if (categoryList.Any())
                {
                    query = query.Where(m => categoryList.All(catSlug => m.Categories.Any(c => c.Slug == catSlug)));
                }
            }

            // 4️⃣ Lọc theo Language (Chọn 1)
            if (!string.IsNullOrWhiteSpace(filters.Language))
            {
                query = query.Where(m => m.Language == filters.Language);
            }

            // 5️⃣ Lọc theo Years (CHỌN NHIỀU - OR)
            if (!string.IsNullOrWhiteSpace(filters.Years))
            {
                var yearList = new List<int>();
                foreach (var yearStr in filters.Years.Split(',', StringSplitOptions.RemoveEmptyEntries))
                {
                    if (int.TryParse(yearStr, out int year))
                    {
                        yearList.Add(year);
                    }
                }
                if (yearList.Any())
                {
                    query = query.Where(m => m.Year.HasValue && yearList.Contains(m.Year.Value));
                }
            }
            // 💡 ===== KẾT THÚC LOGIC LỌC MỚI ===== 💡

            // ===== SẮP XẾP =====
            query = (filters.SortBy?.ToLower()) switch
            {
                "newest" => query.OrderByDescending(m => m.Year).ThenByDescending(m => m.CreatedAt),
                "rating" => query.OrderByDescending(m => m.Rating ?? 0),
                "views" => query.OrderByDescending(m => m.ViewCount ?? 0),
                _ => query.OrderByDescending(m => m.UpdatedAt ?? m.CreatedAt)
            };

            // ===== PHÂN TRANG =====
            int totalMovies = await query.CountAsync();
            int totalPages = (int)Math.Ceiling(totalMovies / (double)pageSize);

            var movies = await query
                .Select(m => new Movie
                {
                    MovieId = m.MovieId,
                    Name = m.Name,
                    Slug = m.Slug,
                    ThumbUrl = m.ThumbUrl,
                    PosterUrl = m.PosterUrl
                })
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // ✅ Lấy danh sách Countries
            var countries = await _context.Countries
                .Where(c => c.IsActive == true)
                .OrderBy(c => c.Name)
                .ToListAsync();

            // ✅ Lấy danh sách Categories
            var categories = await _context.Categories
                .Where(c => c.IsActive == true)
                .OrderBy(c => c.Name)
                .ToListAsync();

            // 💡 Set giá trị mặc định cho bộ lọc (vì đây là trang Phim Lẻ)
            filters.Type = "single";

            // 🔹 Truyền dữ liệu cho View
            ViewBag.CategoryName = "Phim lẻ";
            ViewBag.Countries = countries;
            ViewBag.Categories = categories; // 💡 Thêm
            ViewBag.CategorySlug = "phim-le";
            ViewBag.Filters = filters; // 💡 filters giờ chứa Models mới
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewData["Title"] = "Phim Lẻ Hay | Phim Lẻ Mới Nhất Chọn Lọc";
            ViewBag.SeoDescription = "Tổng hợp Phim Lẻ hay chọn lọc, cập nhật mới nhất. Xem Phim Lẻ vietsub, thuyết minh nhanh nhất tại MoonPhim.";

            return View("PhimLe", movies);
        }

        // ============================================================
        // 🎥 DANH SÁCH PHIM BỘ (ĐÃ SỬA CHO CHỌN NHIỀU)
        // ============================================================
        [Route("the-loai/phim-bo")]
        public async Task<IActionResult> PhimBo([FromQuery] MovieFilterViewModel filters)
        {
            int pageSize = filters.PageSize > 0 ? filters.PageSize : 20;
            int page = filters.Page > 0 ? filters.Page : 1;

            var query = _context.Movies
                .Include(m => m.Countries)
                .Include(m => m.Categories) // 💡 Thêm
                .Where(m => (m.IsActive ?? false) &&
                               (m.Type != null && (
                                   m.Type.ToLower() == "series" ||
                                   m.Type.ToLower() == "phimbo" ||
                                   m.Type.ToLower() == "phim-bo" ||
                                   m.Type.ToLower() == "phim bộ")));

            // 💡 ===== BẮT ĐẦU LOGIC LỌC MỚI (CHỌN NHIỀU) ===== 💡

            // 1️⃣ Lọc theo Type
            if (!string.IsNullOrWhiteSpace(filters.Type) && filters.Type != "series")
            {
                query = query.Where(m => m.Type == filters.Type);
            }

            // 2️⃣ Lọc theo Countries (CHỌN NHIỀU - OR)
            if (!string.IsNullOrWhiteSpace(filters.Countries))
            {
                var countryList = filters.Countries.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
                if (countryList.Any())
                {
                    query = query.Where(m => m.Countries.Any(c => countryList.Contains(c.Slug)));
                }
            }

            // 3️⃣ Lọc theo Categories (CHỌN NHIỀU - AND)
            if (!string.IsNullOrWhiteSpace(filters.Categories))
            {
                var categoryList = filters.Categories.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
                if (categoryList.Any())
                {
                    query = query.Where(m => categoryList.All(catSlug => m.Categories.Any(c => c.Slug == catSlug)));
                }
            }

            // 4️⃣ Lọc theo Language (Chọn 1)
            if (!string.IsNullOrWhiteSpace(filters.Language))
            {
                query = query.Where(m => m.Language == filters.Language);
            }

            // 5️⃣ Lọc theo Years (CHỌN NHIỀU - OR)
            if (!string.IsNullOrWhiteSpace(filters.Years))
            {
                var yearList = new List<int>();
                foreach (var yearStr in filters.Years.Split(',', StringSplitOptions.RemoveEmptyEntries))
                {
                    if (int.TryParse(yearStr, out int year))
                    {
                        yearList.Add(year);
                    }
                }
                if (yearList.Any())
                {
                    query = query.Where(m => m.Year.HasValue && yearList.Contains(m.Year.Value));
                }
            }
            // 💡 ===== KẾT THÚC LOGIC LỌC MỚI ===== 💡

            // ===== SẮP XẾP =====
            query = (filters.SortBy?.ToLower()) switch
            {
                "newest" => query.OrderByDescending(m => m.Year).ThenByDescending(m => m.CreatedAt),
                "rating" => query.OrderByDescending(m => m.Rating ?? 0),
                "views" => query.OrderByDescending(m => m.ViewCount ?? 0),
                _ => query.OrderByDescending(m => m.UpdatedAt ?? m.CreatedAt)
            };

            // ===== PHÂN TRANG =====
            int totalMovies = await query.CountAsync();
            int totalPages = (int)Math.Ceiling(totalMovies / (double)pageSize);

            var movies = await query
                .Select(m => new Movie
                {
                    MovieId = m.MovieId,
                    Name = m.Name,
                    Slug = m.Slug,
                    ThumbUrl = m.ThumbUrl,
                    PosterUrl = m.PosterUrl
                })
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // ✅ Lấy danh sách Countries
            var countries = await _context.Countries
                .Where(c => c.IsActive == true)
                .OrderBy(c => c.Name)
                .ToListAsync();

            // ✅ Lấy danh sách Categories
            var categories = await _context.Categories
                .Where(c => c.IsActive == true)
                .OrderBy(c => c.Name)
                .ToListAsync();

            // 💡 Set giá trị mặc định cho bộ lọc (vì đây là trang Phim Bộ)
            filters.Type = "series";

            // 🔹 Truyền dữ liệu cho View
            ViewBag.CategoryName = "Phim bộ";
            ViewBag.Countries = countries;
            ViewBag.Categories = categories; // 💡 Thêm
            ViewBag.CategorySlug = "phim-bo";
            ViewBag.Filters = filters; // 💡 filters giờ chứa Models mới
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewData["Title"] = "Phim Bộ Hay | Phim Bộ Mới Nhất Chọn Lọc";
            ViewBag.SeoDescription = "Tổng hợp Phim Bộ hay chọn lọc, cập nhật mới nhất. Xem Phim Bộ vietsub, thuyết minh nhanh nhất tại MoonPhim.";

            return View("PhimBo", movies);
        }

        // ============================================================
        // 🧠 API GỢI Ý TÌM KIẾM NHANH (SỬA LẠI CHO ĐÚNG)
        // ============================================================
        [HttpGet("api/goi-y-tim-kiem")]
        public async Task<IActionResult> SearchSuggestions(string keyword)
        {
            if (string.IsNullOrWhiteSpace(keyword))
                return Json(new List<object>());

            keyword = keyword.Trim().ToLower();

            var suggestions = await _context.Movies
                .Where(m => (m.IsActive ?? false) &&
                               (m.Name.ToLower().Contains(keyword) ||
                                (m.OriginalName != null && m.OriginalName.ToLower().Contains(keyword))))
                .OrderByDescending(m => m.ViewCount ?? 0)

                // 💡 💡 💡 PHỤC HỒI LẠI ĐOẠN CODE ĐÚNG 💡 💡 💡
                .Select(m => new
                {
                    name = m.Name,
                    slug = m.Slug,
                    image =
                        !string.IsNullOrEmpty(m.PosterUrl) && m.PosterUrl.StartsWith("http") ? m.PosterUrl :
                        !string.IsNullOrEmpty(m.PosterUrl) ? "https://img.ophim.live/uploads/movies/" + m.PosterUrl.TrimStart('/') :
                        !string.IsNullOrEmpty(m.ThumbUrl) && m.ThumbUrl.StartsWith("http") ? m.ThumbUrl :
                        !string.IsNullOrEmpty(m.ThumbUrl) ? "https://img.ophim.live/uploads/movies/" + m.ThumbUrl.TrimStart('/') :
                        "https://via.placeholder.com/300x450/444444/ffffff?text=" + Uri.EscapeDataString(m.Name ?? "No Image")
                })
                .Take(10)
                .ToListAsync();

            return Json(suggestions);
        }
    }
}
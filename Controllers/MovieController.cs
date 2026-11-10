using MovieWeb.Helpers;
// NỘI DUNG ĐẦY ĐỦ CỦA: Controllers/MovieController.cs

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
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
        private readonly IMovieSyncService _movieSync;
        private readonly MovieWebDbContext _context;
        private readonly IMovieRepository _movieRepository; // Giữ nguyên
        private readonly IAuthService _authService; // Giữ nguyên
        private readonly IMemoryCache _cache;

        public MovieController(MovieWebDbContext context, IMovieRepository movieRepository, IAuthService authService, IMovieSyncService movieSync, IMemoryCache cache)
        {
            _movieSync = movieSync;
            _context = context;
            _movieRepository = movieRepository;
            _authService = authService;
            _cache = cache;
        }

        // ============================================================
        // 🎬 TRANG CHI TIẾT PHIM & XEM TẬP (SEO CHUẨN HÓA URL & 301 REDIRECT)
        // ============================================================
        [Route("phim/{slug}")]
        [Route("phim/{slug}/{episodeSlug}")]
        public async Task<IActionResult> Detail(string slug, string? episodeSlug = null, [FromQuery] string? server = null)
        {
            if (string.IsNullOrEmpty(slug))
                return NotFound();

            IQueryable<Movie> MovieQuery() => _context.Movies
                .Include(m => m.Actors)
                .Include(m => m.Categories)
                .Include(m => m.Directors)
                .Include(m => m.Countries)
                .Include(m => m.Episodes)
                .Where(m => m.IsActive ?? false);

            // URL chuẩn của trang phim là /phim/{slug} (slug là duy nhất, đã được Google index) — tìm theo slug đầy đủ.
            var movie = await MovieQuery().FirstOrDefaultAsync(m => m.Slug == slug);

            if (movie == null && SeoUrlHelper.TryParseMovieSegment(slug, out var movieSlug, out var parsedMovieId))
            {
                // Dạng /phim/{slug}-{id} (bản thử chưa từng lên production): chỉ 301 khi slug khớp đúng phim của ID đó.
                // Không chuyển hướng chỉ theo ID: "abc-phan-2" của phim đã gỡ sẽ bị đẩy sang phim ID 2 khác hẳn.
                var movieById = await MovieQuery().FirstOrDefaultAsync(m => m.MovieId == parsedMovieId!.Value);
                if (movieById != null && string.Equals(movieById.Slug, movieSlug, StringComparison.OrdinalIgnoreCase))
                {
                    var target = string.IsNullOrEmpty(episodeSlug)
                        ? SeoUrlHelper.MovieUrl(movieById.Slug, movieById.MovieId)
                        : $"/phim/{movieById.Slug}/{episodeSlug}";
                    Response.Headers.Append("Cache-Control", "public, max-age=86400");
                    return RedirectPermanent(target);
                }
            }

            if (movie == null)
            {
                // URL cũ sau khi admin đổi slug phim → 301 sang URL mới (giữ thứ hạng Google, link chia sẻ cũ vẫn chạy).
                var renamedTo = await _context.MovieSlugRedirects.AsNoTracking()
                    .Where(r => r.OldSlug == slug && (r.Movie.IsActive ?? false))
                    .Select(r => r.Movie.Slug).FirstOrDefaultAsync();
                if (renamedTo != null)
                {
                    var target = string.IsNullOrEmpty(episodeSlug) ? $"/phim/{renamedTo}" : $"/phim/{renamedTo}/{episodeSlug}";
                    Response.Headers.Append("Cache-Control", "public, max-age=86400");
                    return RedirectPermanent(target + Request.QueryString);
                }
                return NotFound();
            }

            await _movieSync.EnsureVSMovEpisodesAsync(movie.MovieId);

            // 👁️ Tăng lượt xem
            movie.ViewCount = (movie.ViewCount ?? 0) + 1;
            await _context.SaveChangesAsync();

            // 🎞️ Lấy danh sách tập phim khả dụng
            var allEpisodes = await _context.Episodes
                .Where(e => e.MovieId == movie.MovieId)
                .OrderBy(e => e.EpisodeName)
                .ToListAsync();

            allEpisodes = allEpisodes.Where(e => VSMovSource.IsSupported(e.LinkM3u8)).ToList();

            // 🧩 Gom nhóm canonical episode để lấy MIN(EpisodeId) cho từng tập (chống trùng lặp nhiều server)
            var canonicalGroups = allEpisodes
                .GroupBy(e => SeoUrlHelper.NormalizeEpisodeSlug(e.Slug))
                .ToDictionary(
                    g => g.Key,
                    g => new
                    {
                        CleanSlug = g.Key,
                        MinEpisodeId = g.Min(e => e.EpisodeId),
                        Episodes = g.ToList()
                    });

            var episodeToCanonicalMap = new Dictionary<int, (string CleanSlug, int MinId)>();
            foreach (var grp in canonicalGroups.Values)
            {
                foreach (var ep in grp.Episodes)
                {
                    episodeToCanonicalMap[ep.EpisodeId] = (grp.CleanSlug, grp.MinEpisodeId);
                }
            }

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

            var serverUrlKeys = VSMovSource.ServerUrlKeys(serverDisplayNames.Keys);
            ViewBag.ServerUrlKeys = serverUrlKeys;

            // 🎯 XỬ LÝ KIỂM TRA ROUTE TẬP & 301 REDIRECT NẾU URL CHƯA PHẢI CANONICAL MIN(EPISODE_ID)
            Episode? selectedEpisode = null;
            if (!string.IsNullOrEmpty(episodeSlug))
            {
                bool isNewEpFormat = SeoUrlHelper.TryParseEpisodeSegment(episodeSlug, out var parsedEpSlug, out var parsedEpId);

                if (parsedEpId.HasValue)
                {
                    // User/Bot vào /tap-{cleanSlug}-{episodeId}
                    if (episodeToCanonicalMap.TryGetValue(parsedEpId.Value, out var canon))
                    {
                        // Nếu ID không phải MIN(EpisodeId) hoặc URL chưa đúng chuẩn -> 301 Permanent Redirect
                        if (parsedEpId.Value != canon.MinId || !isNewEpFormat || !string.Equals(parsedEpSlug, canon.CleanSlug, StringComparison.OrdinalIgnoreCase))
                        {
                            var targetUrl = SeoUrlHelper.EpisodeUrl(movie.Slug, canon.CleanSlug, canon.MinId);
                            Response.Headers.Append("Cache-Control", "public, max-age=86400");
                            return RedirectPermanent(targetUrl);
                        }

                        // Đúng URL canonical chứa MIN(EpisodeId)
                        var matchedGroup = canonicalGroups[canon.CleanSlug];
                        var availableServerEpisodes = matchedGroup.Episodes;

                        // Nếu có chỉ định server trong query, ưu tiên server đó nhưng KHÔNG đổi URL
                        if (!string.IsNullOrEmpty(server))
                        {
                            selectedEpisode = availableServerEpisodes.FirstOrDefault(e =>
                                NormalizeKey(e.ServerName) == NormalizeKey(server) ||
                                (serverUrlKeys.TryGetValue(NormalizeKey(e.ServerName), out var uk) && uk.Equals(server, StringComparison.OrdinalIgnoreCase)));
                        }

                        selectedEpisode ??= availableServerEpisodes.FirstOrDefault(e => NormalizeKey(e.ServerName) == defaultServerKey)
                            ?? availableServerEpisodes.FirstOrDefault(e => e.EpisodeId == canon.MinId)
                            ?? availableServerEpisodes.First();

                        defaultServerKey = NormalizeKey(selectedEpisode.ServerName);
                    }
                    else
                    {
                        return NotFound();
                    }
                }
                else
                {
                    // URL cũ dạng /phim/{slug}/{oldEpisodeSlug} -> tìm canonical và 301 Permanent Redirect
                    var matchedGrp = canonicalGroups.Values.FirstOrDefault(g =>
                        string.Equals(g.CleanSlug, parsedEpSlug, StringComparison.OrdinalIgnoreCase) ||
                        g.Episodes.Any(e => string.Equals(e.Slug, episodeSlug, StringComparison.OrdinalIgnoreCase)));

                    if (matchedGrp != null)
                    {
                        var targetUrl = SeoUrlHelper.EpisodeUrl(movie.Slug, matchedGrp.CleanSlug, matchedGrp.MinEpisodeId);
                        Response.Headers.Append("Cache-Control", "public, max-age=86400");
                        return RedirectPermanent(targetUrl);
                    }
                    else
                    {
                        return NotFound();
                    }
                }
            }

            ViewBag.SelectedEpisodeId = selectedEpisode?.EpisodeId;
            ViewBag.SelectedEpisodeName = selectedEpisode?.EpisodeName;
            ViewBag.CurrentEpisode = selectedEpisode is null ? null :
                (int.TryParse(Regex.Match(selectedEpisode.EpisodeName, @"\d+").Value, out var number) ? number : (int?)null);

            ViewBag.GroupedEpisodes = groupedByNormalized;
            ViewBag.ServerDisplayNames = serverDisplayNames;
            ViewBag.Servers = serverDisplayNames.Keys.ToList();
            ViewBag.DefaultServer = defaultServerKey;
            ViewBag.EpisodeMinIdMap = episodeToCanonicalMap;

            // 🎯 GỢI Ý PHIM LIÊN QUAN - THUẬT TOÁN "SMART ROTATION" (XOAY TUA DỰA TRÊN MOVIE_ID & DAY_OF_YEAR)
            var categoryIds = movie.Categories.Select(c => c.CategoryId).ToList();
            var candidatePool = await _context.Movies
                .AsNoTracking()
                .Include(m => m.Categories)
                .Where(m => (m.IsActive ?? false)
                            && m.MovieId != movie.MovieId
                            && m.Categories.Any(c => categoryIds.Contains(c.CategoryId)))
                .OrderByDescending(m => m.ViewCount ?? 0)
                .Take(50)
                .ToListAsync();

            int daySeed = DateTime.UtcNow.DayOfYear;
            int offset = candidatePool.Count > 8 ? Math.Abs((movie.MovieId * 31 + daySeed) % (candidatePool.Count - 7)) : 0;
            var relatedMovies = candidatePool.Skip(offset).Take(8).ToList();
            if (relatedMovies.Count < 8 && candidatePool.Count > relatedMovies.Count)
            {
                var remaining = candidatePool.Take(8 - relatedMovies.Count);
                foreach (var rm in remaining)
                {
                    if (!relatedMovies.Any(x => x.MovieId == rm.MovieId)) relatedMovies.Add(rm);
                }
            }
            ViewBag.RelatedMovies = relatedMovies;

            // ⚡ KHỐI "PHIM MỚI CẬP NHẬT HÔM NAY" (CACHE 15 PHÚT TỐI ƯU CRAWL BUDGET)
            var latestMoviesToday = await _cache.GetOrCreateAsync("seo_latest_movies_today", async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15);
                return await _context.Movies.AsNoTracking()
                    .Where(m => m.IsActive == true)
                    .OrderByDescending(m => m.UpdatedAt ?? m.CreatedAt)
                    .Take(8)
                    .Select(m => new Movie
                    {
                        MovieId = m.MovieId,
                        Slug = m.Slug,
                        Name = m.Name,
                        OriginalName = m.OriginalName,
                        PosterUrl = m.PosterUrl,
                        ThumbUrl = m.ThumbUrl,
                        Quality = m.Quality,
                        Year = m.Year,
                        EpisodeCurrent = m.EpisodeCurrent
                    })
                    .ToListAsync();
            });
            ViewBag.LatestMoviesToday = latestMoviesToday;

            // 💎 SEO CANONICAL, SPINTAX VÀ SCHEMA.ORG ĐA TẦNG
            var movieCanonicalPath = SeoUrlHelper.MovieUrl(movie.Slug, movie.MovieId);
            var movieCanonicalUrl = $"https://moonphim.me{movieCanonicalPath}";
            var currentCanonicalPath = selectedEpisode != null && episodeToCanonicalMap.TryGetValue(selectedEpisode.EpisodeId, out var epCanon)
                ? SeoUrlHelper.EpisodeUrl(movie.Slug, epCanon.CleanSlug, epCanon.MinId)
                : movieCanonicalPath;
            var currentCanonicalUrl = $"https://moonphim.me{currentCanonicalPath}";

            string spintaxDesc = SeoContentHelper.GenerateEpisodeSpintax(movie, selectedEpisode, allEpisodes, serverDisplayNames);
            ViewBag.SpintaxDescription = spintaxDesc;
            ViewBag.CanonicalUrl = currentCanonicalUrl;
            ViewBag.MovieSchemaJson = SeoSchemaHelper.BuildMovieOrSeriesSchema(movie, movieCanonicalUrl);
            ViewBag.EpisodeSchemaJson = SeoSchemaHelper.BuildEpisodeAndVideoSchema(movie, selectedEpisode, currentCanonicalUrl, spintaxDesc);
            ViewBag.BreadcrumbSchemaJson = SeoSchemaHelper.BuildBreadcrumbSchema(movie, selectedEpisode, movieCanonicalUrl, selectedEpisode != null ? currentCanonicalUrl : null);

            ViewData["Title"] = selectedEpisode != null ? $"{movie.Name} - Tập {selectedEpisode.EpisodeName}" : movie.Name;
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

            // 🎞️ Lấy tập đầu tiên để phát (cho cả phim lẻ và phim bộ)
            var firstEpisode = selectedEpisode ?? allEpisodes.FirstOrDefault();
            string? episode1Url = VSMovSource.GetPlaybackUrl(firstEpisode?.LinkM3u8);

            // 📢 Quảng cáo
            var advertisements = await _context.Advertisements
                .Where(a => a.IsActive)
                .OrderBy(a => a.DisplayOrder)
                .ToListAsync();

            ViewBag.Advertisements = advertisements;
            ViewBag.ShouldShowAds = shouldShowAds;
            ViewBag.IsSeriesType = isSeriesType;
            ViewBag.Episode1Url = episode1Url;

            // Chỉ giao diện V2 cần cột "Top thịnh hành" (đọc-only, 5 dòng, đúng cột cần hiển thị)
            if (UiVersion.IsV2(HttpContext))
            {
                ViewBag.TrendingMovies = await _context.Movies.AsNoTracking()
                    .Where(m => (m.IsActive ?? false) && m.MovieId != movie.MovieId)
                    .OrderByDescending(m => m.ViewCount ?? 0)
                    .Take(5)
                    .Select(m => new Movie { Slug = m.Slug, Name = m.Name, PosterUrl = m.PosterUrl, Quality = m.Quality, ViewCount = m.ViewCount })
                    .ToListAsync();
            }

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
            int pageSize = MovieWeb.Helpers.UiVersion.IsV2(HttpContext) ? 24 : (filters.PageSize > 0 ? filters.PageSize : 20); // V2: 24 = đủ hàng cho lưới 2/3/4/6 cột
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
                    Year = m.Year,
                    Quality = m.Quality,
                    Time = m.Time,
                    Language = m.Language,
                    Rating = m.Rating,
                    EpisodeCurrent = m.EpisodeCurrent,
                    Categories = m.Categories.Select(c => new Category { Name = c.Name }).Take(2).ToList()
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
            ViewBag.TotalPages = totalPages; ViewBag.TotalMovies = totalMovies; // Pagination

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
                await _movieSync.EnsureVSMovEpisodesAsync(movieId);
                // 1️⃣ Lấy tất cả tập phim của movieId
                var allEpisodes = await _context.Episodes
                    .Where(e => e.MovieId == movieId)
                    .OrderBy(e => e.EpisodeName)
                    .ToListAsync();

                allEpisodes = allEpisodes.Where(e => VSMovSource.IsSupported(e.LinkM3u8)).ToList();
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
        public async Task<IActionResult> PhimMoiCapNhat([FromQuery] MovieFilterViewModel filters)
        {
            int pageSize = MovieWeb.Helpers.UiVersion.IsV2(HttpContext) ? 24 : (filters.PageSize > 0 ? filters.PageSize : 24);
            int page = filters.Page > 0 ? filters.Page : 1;

            var query = _context.Movies
                .Include(m => m.Countries)
                .Include(m => m.Categories)
                .Where(m => (m.IsActive ?? false) && (m.Episodes.Any() || !string.IsNullOrEmpty(m.TrailerUrl)));

            // 1. Lọc theo Loại phim (Type)
            if (!string.IsNullOrWhiteSpace(filters.Type))
            {
                query = query.Where(m => m.Type == filters.Type);
            }

            // 2. Lọc theo Quốc gia (CHỌN NHIỀU - OR)
            if (!string.IsNullOrEmpty(filters.Countries))
            {
                var countryList = filters.Countries.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
                if (countryList.Any())
                {
                    query = query.Where(m => m.Countries.Any(c => countryList.Contains(c.Slug)));
                }
            }

            // 3. Lọc theo Thể loại (CHỌN NHIỀU - AND)
            if (!string.IsNullOrEmpty(filters.Categories))
            {
                var categoryList = filters.Categories.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
                if (categoryList.Any())
                {
                    query = query.Where(m => categoryList.All(catSlug => m.Categories.Any(c => c.Slug == catSlug)));
                }
            }

            // 4. Lọc theo Năm sản xuất (CHỌN NHIỀU - OR)
            if (!string.IsNullOrEmpty(filters.Years))
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

            // 5. Lọc theo Phiên bản (Language)
            if (!string.IsNullOrEmpty(filters.Language))
            {
                query = query.Where(m => m.Language == filters.Language);
            }

            // 6. Sắp xếp
            query = (filters.SortBy?.ToLower()) switch
            {
                "newest" => query.OrderByDescending(m => m.Year).ThenByDescending(m => m.CreatedAt),
                "rating" => query.OrderByDescending(m => m.Rating ?? 0),
                "views" or "view" => query.OrderByDescending(m => m.ViewCount ?? 0),
                _ => query.OrderByDescending(m => m.Year).ThenByDescending(m => m.UpdatedAt ?? m.CreatedAt)
            };

            var totalMovies = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalMovies / (double)pageSize);

            var movies = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // ✅ Lấy danh sách Countries & Categories cho Bộ lọc
            var countries = await _context.Countries
                .Where(c => c.IsActive == true)
                .OrderBy(c => c.Name)
                .ToListAsync();

            var categories = await _context.Categories
                .Where(c => c.IsActive == true)
                .OrderBy(c => c.Name)
                .ToListAsync();

            ViewBag.CategoryName = "Phim Mới Cập Nhật";
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages; ViewBag.TotalMovies = totalMovies;
            ViewBag.Countries = countries;
            ViewBag.Categories = categories;
            ViewBag.Filters = filters;
            ViewBag.SeoDescription = "Phim mới cập nhật liên tục, xem phim online miễn phí chất lượng cao.";

            return View(movies);
        }

        [Route("the-loai/hoat-hinh")]
        public async Task<IActionResult> HoatHinh([FromQuery] MovieFilterViewModel filters)
        {
            int pageSize = MovieWeb.Helpers.UiVersion.IsV2(HttpContext) ? 24 : (filters.PageSize > 0 ? filters.PageSize : 24);
            int page = filters.Page > 0 ? filters.Page : 1;

            var query = _context.Movies
                .Include(m => m.Countries)
                .Include(m => m.Categories)
                .Where(m => (m.IsActive ?? false) && m.Type == "hoathinh" && (m.Episodes.Any() || !string.IsNullOrEmpty(m.TrailerUrl)));

            // 1. Lọc theo Quốc gia (CHỌN NHIỀU - OR)
            if (!string.IsNullOrEmpty(filters.Countries))
            {
                var countryList = filters.Countries.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
                if (countryList.Any())
                {
                    query = query.Where(m => m.Countries.Any(c => countryList.Contains(c.Slug)));
                }
            }

            // 2. Lọc theo Thể loại (CHỌN NHIỀU - AND)
            if (!string.IsNullOrEmpty(filters.Categories))
            {
                var categoryList = filters.Categories.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
                if (categoryList.Any())
                {
                    query = query.Where(m => categoryList.All(catSlug => m.Categories.Any(c => c.Slug == catSlug)));
                }
            }

            // 3. Lọc theo Năm sản xuất (CHỌN NHIỀU - OR)
            if (!string.IsNullOrEmpty(filters.Years))
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

            // 4. Lọc theo Phiên bản (Language)
            if (!string.IsNullOrEmpty(filters.Language))
            {
                query = query.Where(m => m.Language == filters.Language);
            }

            // 5. Sắp xếp
            query = (filters.SortBy?.ToLower()) switch
            {
                "newest" => query.OrderByDescending(m => m.Year).ThenByDescending(m => m.CreatedAt),
                "rating" => query.OrderByDescending(m => m.Rating ?? 0),
                "views" or "view" => query.OrderByDescending(m => m.ViewCount ?? 0),
                _ => query.OrderByDescending(m => m.Year).ThenByDescending(m => m.UpdatedAt ?? m.CreatedAt)
            };

            var totalMovies = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalMovies / (double)pageSize);

            var movies = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // ✅ Lấy danh sách Countries & Categories cho Bộ lọc
            var countries = await _context.Countries
                .Where(c => c.IsActive == true)
                .OrderBy(c => c.Name)
                .ToListAsync();

            var categories = await _context.Categories
                .Where(c => c.IsActive == true)
                .OrderBy(c => c.Name)
                .ToListAsync();

            filters.Type = "hoathinh";

            ViewBag.CategoryName = "Phim Hoạt Hình";
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages; ViewBag.TotalMovies = totalMovies;
            ViewBag.Countries = countries;
            ViewBag.Categories = categories;
            ViewBag.Filters = filters;
            ViewBag.SeoDescription = "Xem phim hoạt hình, anime, cartoon online miễn phí.";

            return View(movies);
        }

        // ============================================================
        // 🌟 DANH SÁCH PHIM MỚI CẬP NHẬT
        // ============================================================
        [Route("the-loai/phim-moi")]
        [Route("phim-moi")]
        public async Task<IActionResult> PhimMoi([FromQuery] MovieFilterViewModel filters)
        {
            int pageSize = MovieWeb.Helpers.UiVersion.IsV2(HttpContext) ? 24 : (filters.PageSize > 0 ? filters.PageSize : 20); // V2: 24 = đủ hàng cho lưới 2/3/4/6 cột
            int page = filters.Page > 0 ? filters.Page : 1;

            var query = _context.Movies
                .Include(m => m.Countries)
                .Include(m => m.Categories)
                .Where(m => (m.IsActive ?? false) && (m.Episodes.Any() || !string.IsNullOrEmpty(m.TrailerUrl)));

            if (!string.IsNullOrWhiteSpace(filters.Type))
            {
                query = query.Where(m => m.Type == filters.Type);
            }

            if (!string.IsNullOrWhiteSpace(filters.Countries))
            {
                var countryList = filters.Countries.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
                if (countryList.Any())
                {
                    query = query.Where(m => m.Countries.Any(c => countryList.Contains(c.Slug)));
                }
            }

            if (!string.IsNullOrWhiteSpace(filters.Categories))
            {
                var categoryList = filters.Categories.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
                if (categoryList.Any())
                {
                    query = query.Where(m => categoryList.All(catSlug => m.Categories.Any(c => c.Slug == catSlug)));
                }
            }

            if (!string.IsNullOrWhiteSpace(filters.Language))
            {
                query = query.Where(m => m.Language == filters.Language);
            }

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

            query = (filters.SortBy?.ToLower()) switch
            {
                "view" or "views" => query.OrderByDescending(m => m.ViewCount ?? 0),
                "year" => query.OrderByDescending(m => m.Year).ThenByDescending(m => m.UpdatedAt ?? m.CreatedAt),
                _ => query.OrderByDescending(m => m.UpdatedAt ?? m.CreatedAt)
            };

            int totalMovies = await query.CountAsync();
            int totalPages = (int)Math.Ceiling(totalMovies / (double)pageSize);

            var movies = await query
                .Select(m => new Movie
                {
                    MovieId = m.MovieId,
                    Name = m.Name,
                    Slug = m.Slug,
                    ThumbUrl = m.ThumbUrl,
                    PosterUrl = m.PosterUrl,
                    Year = m.Year,
                    Quality = m.Quality,
                    Time = m.Time,
                    Language = m.Language,
                    Rating = m.Rating,
                    EpisodeCurrent = m.EpisodeCurrent,
                    Categories = m.Categories.Select(c => new Category { Name = c.Name }).Take(2).ToList()
                })
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var countries = await _context.Countries
                .Where(c => c.IsActive == true)
                .OrderBy(c => c.Name)
                .ToListAsync();

            var categories = await _context.Categories
                .Where(c => c.IsActive == true)
                .OrderBy(c => c.Name)
                .ToListAsync();

            ViewBag.CategoryName = "Phim mới cập nhật";
            ViewBag.Countries = countries;
            ViewBag.Categories = categories;
            ViewBag.CategorySlug = "phim-moi";
            ViewBag.Filters = filters;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages; ViewBag.TotalMovies = totalMovies;
            ViewData["Title"] = "Phim Mới Cập Nhật | Phim Mới Vietsub Thuyết Minh";
            ViewBag.SeoDescription = "Tổng hợp danh sách phim mới nhất, phim mới cập nhật vietsub, lồng tiếng chất lượng cao tại MoonPhim.";

            return View("PhimBo", movies);
        }

        // ============================================================
        // 🎬 DANH SÁCH PHIM LẺ (ĐÃ SỬA CHO CHỌN NHIỀU)
        // ============================================================
        [Route("the-loai/phim-le")]
        public async Task<IActionResult> PhimLe([FromQuery] MovieFilterViewModel filters)
        {
            int pageSize = MovieWeb.Helpers.UiVersion.IsV2(HttpContext) ? 24 : (filters.PageSize > 0 ? filters.PageSize : 20); // V2: 24 = đủ hàng cho lưới 2/3/4/6 cột
            int page = filters.Page > 0 ? filters.Page : 1;

            // 🔸 Query cơ bản
            var query = _context.Movies
                .Include(m => m.Countries)
                .Include(m => m.Categories) // 💡 Thêm Include
                .Where(m => (m.IsActive ?? false) &&
                               (!string.IsNullOrEmpty(m.TrailerUrl) || m.Episodes.Any(e =>
                                   e.LinkM3u8 != null && EF.Functions.Like(
                                       e.LinkM3u8, "https://_%.streamvsmov.com/%"))) &&
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
                _ => query.OrderByDescending(m => m.Year).ThenByDescending(m => m.UpdatedAt ?? m.CreatedAt)
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
                    PosterUrl = m.PosterUrl,
                    Year = m.Year,
                    Quality = m.Quality,
                    Time = m.Time,
                    Language = m.Language,
                    Rating = m.Rating,
                    EpisodeCurrent = m.EpisodeCurrent,
                    Categories = m.Categories.Select(c => new Category { Name = c.Name }).Take(2).ToList()
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
            ViewBag.TotalPages = totalPages; ViewBag.TotalMovies = totalMovies;
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
            int pageSize = MovieWeb.Helpers.UiVersion.IsV2(HttpContext) ? 24 : (filters.PageSize > 0 ? filters.PageSize : 20); // V2: 24 = đủ hàng cho lưới 2/3/4/6 cột
            int page = filters.Page > 0 ? filters.Page : 1;

            var query = _context.Movies
                .Include(m => m.Countries)
                .Include(m => m.Categories) // 💡 Thêm
                .Where(m => (m.IsActive ?? false) && m.Episodes.Any() &&
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
                _ => query.OrderByDescending(m => m.Year).ThenByDescending(m => m.UpdatedAt ?? m.CreatedAt)
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
                    PosterUrl = m.PosterUrl,
                    Year = m.Year,
                    Quality = m.Quality,
                    Time = m.Time,
                    Language = m.Language,
                    Rating = m.Rating,
                    EpisodeCurrent = m.EpisodeCurrent,
                    Categories = m.Categories.Select(c => new Category { Name = c.Name }).Take(2).ToList()
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
            ViewBag.TotalPages = totalPages; ViewBag.TotalMovies = totalMovies;
            ViewData["Title"] = "Phim Bộ Hay | Phim Bộ Mới Nhất Chọn Lọc";
            ViewBag.SeoDescription = "Tổng hợp Phim Bộ hay chọn lọc, cập nhật mới nhất. Xem Phim Bộ vietsub, thuyết minh nhanh nhất tại MoonPhim.";

            return View("PhimBo", movies);
        }

        // ============================================================
        // 🔥 TRANG PHIM TRENDING TRÊN MOONPHIM (TOP LƯỢT XEM GIẢM DẦN)
        // ============================================================
        [Route("phim-trending")]
        [Route("the-loai/phim-trending")]
        public async Task<IActionResult> Trending([FromQuery] MovieFilterViewModel filters)
        {
            var countries = await _context.Countries.ToListAsync();
            var categories = await _context.Categories.ToListAsync();

            var query = _context.Movies
                .Include(m => m.Countries)
                .Include(m => m.Categories)
                .Where(m => (m.IsActive ?? false) && (m.Episodes.Any() || !string.IsNullOrEmpty(m.TrailerUrl)));

            if (!string.IsNullOrWhiteSpace(filters.Type))
            {
                query = query.Where(m => m.Type == filters.Type);
            }

            if (!string.IsNullOrWhiteSpace(filters.Countries))
            {
                var countryList = filters.Countries.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
                if (countryList.Any())
                {
                    query = query.Where(m => m.Countries.Any(c => countryList.Contains(c.Slug)));
                }
            }

            if (!string.IsNullOrWhiteSpace(filters.Categories))
            {
                var categoryList = filters.Categories.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
                if (categoryList.Any())
                {
                    query = query.Where(m => categoryList.All(catSlug => m.Categories.Any(c => c.Slug == catSlug)));
                }
            }

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

            // Luôn sắp xếp theo Lượt xem nhiều nhất giảm dần (Trending)
            query = query.OrderByDescending(m => m.ViewCount ?? 0)
                         .ThenByDescending(m => m.UpdatedAt ?? m.CreatedAt);

            int page = filters.Page > 0 ? filters.Page : 1;
            int pageSize = MovieWeb.Helpers.UiVersion.IsV2(HttpContext) ? 24 : (filters.PageSize > 0 ? filters.PageSize : 24);
            int totalMovies = await query.CountAsync();
            int totalPages = (int)Math.Ceiling((double)totalMovies / pageSize);

            var movies = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.CategoryName = "Bảng Xếp Hạng Phim Trending Trên MoonPhim";
            ViewBag.Countries = countries;
            ViewBag.Categories = categories;
            ViewBag.CategorySlug = "phim-trending";
            ViewBag.Filters = filters;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages; ViewBag.TotalMovies = totalMovies;
            ViewBag.TotalMovies = totalMovies;
            ViewData["Title"] = "Phim Trending - Bảng Xếp Hạng Xem Nhiều Nhất | MoonPhim";
            ViewBag.SeoDescription = "Khám phá danh sách Phim Trending hot nhất, nhiều lượt xem nhất trên MoonPhim. Xem phim Full HD Vietsub thuyết minh mượt mà tốc độ cao.";

            return View("Trending", movies);
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
                    year = m.Year,
                    image =
                        !string.IsNullOrEmpty(m.PosterUrl) && m.PosterUrl.StartsWith("http") ? m.PosterUrl :
                        !string.IsNullOrEmpty(m.PosterUrl) ? "https://vsmov.com/storage/images/" + m.PosterUrl.TrimStart('/') :
                        !string.IsNullOrEmpty(m.ThumbUrl) && m.ThumbUrl.StartsWith("http") ? m.ThumbUrl :
                        !string.IsNullOrEmpty(m.ThumbUrl) ? "https://vsmov.com/storage/images/" + m.ThumbUrl.TrimStart('/') :
                        "/images/default-poster.jpg"
                })
                .Take(10)
                .ToListAsync();

            return Json(suggestions);
        }
    }
}

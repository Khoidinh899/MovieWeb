using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MovieWeb.Data;
using MovieWeb.Models.Entities;
using MovieWeb.Repositories;
using System.Linq;
using System.Text.RegularExpressions;

namespace MovieWeb.Controllers
{
    public class MovieController : Controller
    {
        private readonly MovieWebDbContext _context;
        private readonly IMovieRepository _movieRepository;

        public MovieController(MovieWebDbContext context, IMovieRepository movieRepository)
        {
            _context = context;
            _movieRepository = movieRepository;
        }

        // ============================================================
        // 🎬 TRANG CHI TIẾT PHIM
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

            // Gợi ý phim liên quan
            var categoryIds = movie.Categories.Select(c => c.CategoryId).ToList();

            var relatedMovies = await _context.Movies
                .Include(m => m.Categories)
                .Where(m => (m.IsActive ?? false) &&
                            m.MovieId != movie.MovieId &&
                            m.Categories.Any(c => categoryIds.Contains(c.CategoryId)))
                .OrderByDescending(m => m.ViewCount ?? 0)
                .Take(8)
                .ToListAsync();

            // ✅ Tăng lượt xem (an toàn null)
            movie.ViewCount = (movie.ViewCount ?? 0) + 1;
            await _context.SaveChangesAsync();

            // ✅ Lấy toàn bộ tập phim theo MovieId
            var allEpisodes = await _context.Episodes
                .Where(e => e.MovieId == movie.MovieId)
                .OrderBy(e => e.EpisodeName)
                .ToListAsync();

            // ====== 🔧 Chuẩn hoá key server để so khớp tuyệt đối ======
            string NormalizeKey(string? s)
            {
                s ??= "Khác";
                s = s.Trim();
                // gom nhiều khoảng trắng thành 1
                s = Regex.Replace(s, @"\s+", " ");
                return s.ToLowerInvariant(); // key chuẩn
            }

            string DisplayName(string? s)
            {
                s ??= "Khác";
                s = s.Trim();
                s = Regex.Replace(s, @"\s+", " ");
                return s;
            }

            // map: key chuẩn -> tên hiển thị
            var serverDisplayNames = new Dictionary<string, string>();
            // group: key chuẩn -> list tập
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

            // ✅ Chọn server mặc định ưu tiên Vietsub -> Thuyết Minh -> bất kỳ
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

            // ====== 🚚 Đẩy sang View ======
            ViewBag.GroupedEpisodes = groupedByNormalized;                 // Dictionary<string(normalizedKey), List<Episode>>
            ViewBag.ServerDisplayNames = serverDisplayNames;               // Dictionary<string(normalizedKey), string(display)]
            ViewBag.Servers = serverDisplayNames.Keys.ToList();            // List<string>(normalizedKey)
            ViewBag.DefaultServer = defaultServerKey;                      // string(normalizedKey)
            ViewBag.RelatedMovies = relatedMovies;

            ViewData["Title"] = movie.Name;
            ViewData["PageType"] = "Detail";

            return View(movie);
        }

        // ============================================================
        // 🔍 TÌM KIẾM PHIM
        // ============================================================
        [HttpGet("tim-kiem")]
        public async Task<IActionResult> Search(string keyword)
        {
            if (string.IsNullOrWhiteSpace(keyword))
            {
                ViewBag.Keyword = "";
                return View("Search", new List<Movie>());
            }

            keyword = keyword.Trim().ToLower();

            var movies = await _context.Movies
                .Include(m => m.Categories)
                .Include(m => m.Actors)
                .Include(m => m.Directors)
                .Include(m => m.Countries)
                .Where(m => (m.IsActive ?? false) &&
                    (
                        m.Name.ToLower().Contains(keyword) ||
                        (m.OriginalName != null && m.OriginalName.ToLower().Contains(keyword)) ||
                        m.Slug.ToLower().Contains(keyword) ||
                        (m.Description != null && m.Description.ToLower().Contains(keyword)) ||
                        m.Categories.Any(c => c.Name.ToLower().Contains(keyword) || c.Slug.ToLower().Contains(keyword)) ||
                        m.Countries.Any(c => c.Name.ToLower().Contains(keyword) || c.Slug.ToLower().Contains(keyword)) ||
                        m.Actors.Any(a => a.Name.ToLower().Contains(keyword)) ||
                        m.Directors.Any(d => d.Name.ToLower().Contains(keyword))
                    )
                )
                .OrderByDescending(m => m.ViewCount ?? 0)
                .Take(40)
                .ToListAsync();

            ViewBag.Keyword = keyword;
            ViewData["Title"] = $"Tìm kiếm: {keyword}";
            ViewData["PageType"] = "Search";

            return View("Search", movies);
        }


        // ============================================================
        // 💬 LẤY COMMENT + ĐÁNH GIÁ
        // ============================================================
        [HttpGet("api/comments/{movieId}")]
        public async Task<IActionResult> GetComments(int movieId)
        {
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
        // 💬 GỬI COMMENT + ĐÁNH GIÁ
        // ============================================================
        [HttpPost("api/comment/add")]
        public async Task<IActionResult> AddComment([FromForm] int movieId, [FromForm] string content, [FromForm] int rating)
        {
            if (string.IsNullOrWhiteSpace(content))
                return BadRequest("Nội dung trống!");

            if (rating < 1 || rating > 5)
                return BadRequest("Điểm đánh giá không hợp lệ!");

            int userId = 1; // user demo

            var comment = new Comment
            {
                MovieId = movieId,
                UserId = userId,
                Content = content.Trim(),
                Rating = rating,
                IsActive = true,
                CreatedAt = DateTime.Now
            };

            _context.Comments.Add(comment);
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Đánh giá & bình luận thành công!" });
        }

        // ============================================================
        // 🎞️ API LẤY DANH SÁCH TẬP PHIM THEO SERVER
        // ============================================================
        [HttpGet("api/episodes/{movieId}")]
        public async Task<IActionResult> GetEpisodes(int movieId, [FromQuery] string? server = null)
        {
            string NormalizeKey(string? s)
            {
                s ??= "Khác";
                s = s.Trim();
                s = Regex.Replace(s, @"\s+", " ");
                return s.ToLowerInvariant();
            }

            var query = _context.Episodes.Where(e => e.MovieId == movieId);

            if (!string.IsNullOrEmpty(server))
            {
                var serverKey = NormalizeKey(server);
                // lọc theo key chuẩn
                query = query.AsEnumerable()
                             .Where(e => NormalizeKey(e.ServerName) == serverKey)
                             .AsQueryable();
            }

            var episodes = query
                .OrderBy(e => e.EpisodeName)
                .Select(e => new
                {
                    e.EpisodeId,
                    e.EpisodeName,
                    e.Slug,
                    e.LinkM3u8,
                    e.ServerName
                })
                .ToList();

            return Json(episodes);
        }
        // ✅ OVERRIDE LINK /the-loai/phim-le
        [Route("the-loai/phim-le")]
        public async Task<IActionResult> PhimLe(int page = 1)
        {
            int pageSize = 20;

            var query = _context.Movies
                .Where(m => m.IsActive == true &&
                            (m.Type == "single" || m.Type == "phimle"))
                .OrderByDescending(m => m.CreatedAt);

            int totalMovies = await query.CountAsync();

            var movies = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // Category giả cho view
            ViewBag.CategoryName = "Phim lẻ";
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling((double)totalMovies / pageSize);

            return View("PhimLe", movies);
        }
        // ============================================================
        // 🧠 API GỢI Ý TÌM KIẾM NHANH
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

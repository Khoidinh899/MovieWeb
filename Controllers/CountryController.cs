using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MovieWeb.Data;
using MovieWeb.Models.Entities;
using MovieWeb.Models.ViewModels;

namespace MovieWeb.Controllers
{
    public class CountryController : Controller
    {
        private readonly MovieWebDbContext _context;
        private readonly ILogger<CountryController> _logger;
        private const int PAGE_SIZE = 24;

        public CountryController(MovieWebDbContext context, ILogger<CountryController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // ============================================================
        // 🌍 TRANG PHIM THEO QUỐC GIA (Có bộ lọc)
        // ============================================================
        [Route("quoc-gia/{slug}")]
        public async Task<IActionResult> Country(string slug, [FromQuery] MovieFilterViewModel filters)
        {
            if (string.IsNullOrWhiteSpace(slug))
                return NotFound("Slug quốc gia không hợp lệ.");

            // 1. Lấy thông tin quốc gia
            var country = await _context.Countries
                .FirstOrDefaultAsync(c => c.Slug == slug && c.IsActive == true);

            if (country == null)
            {
                _logger.LogWarning($"⚠️ Country not found: {slug}");
                return NotFound();
            }

            // 2. Query phim cơ bản
            var movieQuery = _context.Movies
                .Include(m => m.Categories)
                .Include(m => m.Countries)
                .Where(m => m.IsActive == true);

            // 3. Set default Countries filter = slug từ URL
            if (string.IsNullOrWhiteSpace(filters.Countries))
            {
                filters.Countries = slug;
            }

            // ===== ÁP DỤNG CÁC BỘ LỌC =====

            // 1️⃣ Lọc theo Type
            if (!string.IsNullOrWhiteSpace(filters.Type))
            {
                movieQuery = movieQuery.Where(m => m.Type == filters.Type);
            }

            // 2️⃣ Lọc theo Countries (CHỌN NHIỀU - OR)
            if (!string.IsNullOrWhiteSpace(filters.Countries))
            {
                var countryList = filters.Countries.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
                if (countryList.Any())
                {
                    movieQuery = movieQuery.Where(m => m.Countries.Any(c => countryList.Contains(c.Slug)));
                }
            }

            // 3️⃣ Lọc theo Categories (CHỌN NHIỀU - AND)
            if (!string.IsNullOrWhiteSpace(filters.Categories))
            {
                var categoryList = filters.Categories.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
                if (categoryList.Any())
                {
                    movieQuery = movieQuery.Where(m => categoryList.All(catSlug => m.Categories.Any(c => c.Slug == catSlug)));
                }
            }

            // 4️⃣ Lọc theo Language
            if (!string.IsNullOrWhiteSpace(filters.Language))
            {
                movieQuery = movieQuery.Where(m => m.Language == filters.Language);
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
                    movieQuery = movieQuery.Where(m => m.Year.HasValue && yearList.Contains(m.Year.Value));
                }
            }

            // ===== SẮP XẾP (Sửa lại đúng tên property) =====
            movieQuery = (filters.SortBy?.ToLower()) switch
            {
                "newest" => movieQuery.OrderByDescending(m => m.Year).ThenByDescending(m => m.CreatedAt),
                "rating" => movieQuery.OrderByDescending(m => m.Rating ?? 0), // ✅ Sửa từ ImdbScore → Rating
                "views" => movieQuery.OrderByDescending(m => m.ViewCount ?? 0), // ✅ Sửa từ View → ViewCount
                _ => movieQuery.OrderByDescending(m => m.UpdatedAt ?? m.CreatedAt)
            };

            // ===== PHÂN TRANG =====
            int page = filters.Page > 0 ? filters.Page : 1;
            int totalMovies = await movieQuery.CountAsync();
            int totalPages = (int)Math.Ceiling(totalMovies / (double)PAGE_SIZE);

            var movies = await movieQuery
                .Select(m => new Movie
                {
                    MovieId = m.MovieId,
                    Name = m.Name,
                    Slug = m.Slug,
                    ThumbUrl = m.ThumbUrl,
                    PosterUrl = m.PosterUrl,
                    Year = m.Year,
                    Quality = m.Quality
                })
                .Skip((page - 1) * PAGE_SIZE)
                .Take(PAGE_SIZE)
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

            // 🔹 Truyền dữ liệu cho View
            ViewBag.Country = country;
            ViewBag.Filters = filters;
            ViewBag.Countries = countries;
            ViewBag.Categories = categories;
            ViewBag.CountrySlug = slug;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;

            // ✅ SEO
            ViewData["Title"] = $"Phim {country.Name} Hay | Tuyển tập Phim {country.Name} Mới Nhất";
            ViewBag.SeoDescription = $"Tổng hợp phim {country.Name} hay chọn lọc, cập nhật mới nhất. Xem phim {country.Name} vietsub, thuyết minh nhanh nhất tại MoonPhim.";

            return View("Country", movies);
        }
    }
}
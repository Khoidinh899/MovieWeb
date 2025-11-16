// NỘI DUNG ĐẦY ĐỦ CỦA: Controllers/CategoryController.cs

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MovieWeb.Data;
using MovieWeb.Models.ViewModels;
using MovieWeb.Models.Entities;

namespace MovieWeb.Controllers
{
    public class CategoryController : Controller
    {
        private readonly MovieWebDbContext _context;
        private readonly ILogger<CategoryController> _logger;
        private const int PAGE_SIZE = 24;

        public CategoryController(MovieWebDbContext context, ILogger<CategoryController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [Route("the-loai/{slug}")]
        public async Task<IActionResult> Index(string slug, [FromQuery] MovieFilterViewModel filters)
        {
            if (string.IsNullOrWhiteSpace(slug))
                return NotFound("Slug thể loại không hợp lệ.");

            // 🔸 Lấy thể loại theo slug
            var category = await _context.Categories
                .FirstOrDefaultAsync(c => c.Slug == slug && c.IsActive == true);

            if (category == null)
            {
                _logger.LogWarning($"⚠️ Category not found: {slug}");
                return NotFound();
            }

            // 💡 ===== SỬA QUERY CƠ BẢN ===== 💡
            // Bỏ lọc theo slug ở đây, sẽ lọc trong logic filter bên dưới
            var movieQuery = _context.Movies
                .Include(m => m.Categories)
                .Include(m => m.Countries)
                .Where(m => m.IsActive == true);

            // 💡 Set giá trị mặc định cho filter Categories = slug từ URL
            //    NẾU filters.Categories đang rỗng (tức là người dùng mới vào trang)
            if (string.IsNullOrWhiteSpace(filters.Categories))
            {
                filters.Categories = slug;
            }
            // 💡 ===== KẾT THÚC SỬA QUERY ===== 💡

            // ===== ÁP DỤNG CÁC BỘ LỌC (LOGIC MỚI) =====

            // 1️⃣ Lọc theo Type (Chọn 1)
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

            // 4️⃣ Lọc theo Language (Chọn 1)
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
            // 💡 ===== KẾT THÚC LOGIC LỌC MỚI ===== 💡


            // ===== SẮP XẾP =====
            movieQuery = (filters.SortBy?.ToLower()) switch
            {
                "newest" => movieQuery.OrderByDescending(m => m.Year).ThenByDescending(m => m.CreatedAt),
                "rating" => movieQuery.OrderByDescending(m => m.Rating ?? 0),
                "views" => movieQuery.OrderByDescending(m => m.ViewCount ?? 0),
                _ => movieQuery.OrderByDescending(m => m.UpdatedAt ?? m.CreatedAt) // Sửa lại
            };

            // ===== PHÂN TRANG =====
            int page = filters.Page > 0 ? filters.Page : 1;
            int totalMovies = await movieQuery.CountAsync();
            int totalPages = (int)Math.Ceiling(totalMovies / (double)PAGE_SIZE);

            var movies = await movieQuery
                .Select(m => new Movie // Select
                {
                    MovieId = m.MovieId,
                    Name = m.Name,
                    Slug = m.Slug,
                    ThumbUrl = m.ThumbUrl,
                    PosterUrl = m.PosterUrl
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
            ViewBag.Category = category; // Thông tin thể loại chính
            ViewBag.Filters = filters; // filters đã được set filters.Categories = slug
            ViewBag.Countries = countries;
            ViewBag.Categories = categories; // Danh sách full thể loại
            ViewBag.CategorySlug = slug; 
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;

            // ✅ SEO
            ViewData["Title"] = $"Phim {category.Name} Hay | Tuyển tập Phim {category.Name} Mới Nhất";
            ViewBag.SeoDescription = $"Tổng hợp phim {category.Name} hay chọn lọc, cập nhật mới nhất. Xem phim {category.Name} vietsub, thuyết minh nhanh nhất tại MoonPhim.";

            return View("Category", movies);
        }
    }
}
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MovieWeb.Data;

namespace MovieWeb.Controllers
{
    public class CategoryController : Controller
    {
        private readonly MovieWebDbContext _context;

        public CategoryController(MovieWebDbContext context)
        {
            _context = context;
        }

        // ✅ Action chung cho tất cả thể loại
        [Route("the-loai/{slug}")]
        public async Task<IActionResult> Index(string slug, int page = 1)
        {
            if (string.IsNullOrWhiteSpace(slug))
                return NotFound("Slug thể loại không hợp lệ.");

            const int pageSize = 20;

            // 🔸 Lấy thông tin thể loại theo slug
            var category = await _context.Categories
                .FirstOrDefaultAsync(c => c.Slug == slug && c.IsActive == true);

            if (category == null)
                return NotFound($"Không tìm thấy thể loại: {slug}");

            // 🔸 Lấy danh sách phim thuộc thể loại
            var movieQuery = _context.Movies
                .Include(m => m.Categories)
                .Where(m => m.IsActive == true && m.Categories.Any(c => c.Slug == slug))
                .OrderByDescending(m => m.CreatedAt);

            var totalMovies = await movieQuery.CountAsync();
            var movies = await movieQuery
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // 🔸 Truyền dữ liệu ra View
            ViewData["Title"] = category.Name;
            ViewBag.Category = category;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling((double)totalMovies / pageSize);

            return View("Category", movies);
        }
    }
}

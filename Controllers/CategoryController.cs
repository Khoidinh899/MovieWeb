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

        // ✅ Trang Phim Bộ (/the-loai/phim-bo)
        [Route("the-loai/phim-bo")]
        public async Task<IActionResult> PhimBo(int page = 1)
        {
            const int pageSize = 20;

            // 🔸 Lấy thông tin thể loại "Phim Bộ"
            var category = await _context.Categories
                .FirstOrDefaultAsync(c => c.Slug == "phim-bo" && c.IsActive == true);

            if (category == null)
                return NotFound("Không tìm thấy thể loại 'Phim Bộ'.");

            // 🔸 Truy vấn phim thuộc thể loại này
            var movieQuery = _context.Movies
                .Include(m => m.Categories)
                .Where(m => m.IsActive == true 
                    && m.Categories.Any(c => c.Slug == "phim-bo"))
                .OrderByDescending(m => m.CreatedAt);

            var totalMovies = await movieQuery.CountAsync();

            var movies = await movieQuery
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // 🔸 Truyền dữ liệu ra View
            ViewData["Title"] = "Phim Bộ";
            ViewBag.Category = category;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling((double)totalMovies / pageSize);

            return View("PhimBo", movies);
        }
    }
}

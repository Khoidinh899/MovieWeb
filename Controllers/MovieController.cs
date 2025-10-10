using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MovieWeb.Data;
using MovieWeb.Models.Entities;
using MovieWeb.Repositories;

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
        // URL: /phim/{slug}
        // ============================================================
        [Route("phim/{slug}")]
        public async Task<IActionResult> Detail(string slug)
        {
            if (string.IsNullOrEmpty(slug))
                return NotFound();

            // 🔹 Lấy phim theo slug + include các liên kết
            var movie = await _context.Movies
                .Include(m => m.Actors)
                .Include(m => m.Categories)
                .Include(m => m.Directors)
                .Include(m => m.Countries)
                .FirstOrDefaultAsync(m => m.Slug == slug && (m.IsActive ?? false));

            if (movie == null)
                return NotFound();

            // 🔹 Lấy danh sách ID thể loại của phim hiện tại
            var categoryIds = movie.Categories.Select(c => c.CategoryId).ToList();

            // 🔹 Gợi ý phim cùng thể loại (8 phim)
            var relatedMovies = await _context.Movies
                .Where(m => (m.IsActive ?? false) &&
                            m.MovieId != movie.MovieId &&
                            m.Categories.Any(c => categoryIds.Contains(c.CategoryId)))
                .OrderByDescending(m => m.ViewCount ?? 0)
                .Take(8)
                .ToListAsync();

            // 🔹 Tăng lượt xem phim
            movie.ViewCount = (movie.ViewCount ?? 0) + 1;
            await _context.SaveChangesAsync();  

            // 🔹 Gửi dữ liệu ra View
            ViewBag.RelatedMovies = relatedMovies;
            ViewData["Title"] = movie.Name;
            ViewData["PageType"] = "Detail";

            return View(movie);
        }

    }
}

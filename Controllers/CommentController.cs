using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MovieWeb.Data;
using MovieWeb.Models.Entities;
using System.Security.Claims;

namespace MovieWeb.Controllers
{
    [Authorize]
    public class CommentController : Controller
    {
        private readonly MovieWebDbContext _context;

        public CommentController(MovieWebDbContext context)
        {
            _context = context;
        }

        // ✅ Gửi bình luận mới
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Add(int movieId, string content)
        {
            if (string.IsNullOrWhiteSpace(content))
                return BadRequest("Nội dung không được để trống.");

            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));

            var comment = new Comment
            {
                MovieId = movieId,
                UserId = userId,
                Content = content.Trim(),
                CreatedAt = DateTime.Now,
                IsActive = true
            };

            _context.Comments.Add(comment);
            await _context.SaveChangesAsync();

            return RedirectToAction("Detail", "Movie", new { id = movieId });
        }

        // ✅ Xem danh sách bình luận
        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> List(int movieId)
        {
            var comments = await _context.Comments
                .Include(c => c.User)
                .Where(c => c.MovieId == movieId && c.IsActive == true)
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();

            return PartialView("_CommentList", comments);
        }
        [HttpPost("add")]
        [Authorize] // ✅ thêm dòng này để yêu cầu đăng nhập
        public async Task<IActionResult> Add([FromForm] int movieId, [FromForm] string content, [FromForm] int rating)
        {
            var userId = int.Parse(User.FindFirst("UserId").Value);

            if (string.IsNullOrWhiteSpace(content))
                return BadRequest(new { success = false, message = "Nội dung không được để trống" });

            var comment = new Comment
            {
                MovieId = movieId,
                UserId = userId,
                Content = content,
                CreatedAt = DateTime.Now,
                IsActive = true
            };

            _context.Comments.Add(comment);
            await _context.SaveChangesAsync();

            return Ok(new { success = true });
        }

    }
}

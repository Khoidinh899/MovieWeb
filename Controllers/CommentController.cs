using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MovieWeb.Data;
using MovieWeb.Models.Entities;
using System.Security.Claims;

namespace MovieWeb.Controllers
{
    // ✅ Chỉ route API comment qua controller này
    [Route("api/comment")]
    public class CommentController : Controller
    {
        private readonly MovieWebDbContext _context;

        public CommentController(MovieWebDbContext context)
        {
            _context = context;
        }

        // ============================================================
        // 💬 GỬI BÌNH LUẬN / ĐÁNH GIÁ
        // ============================================================
        [Authorize] // ✅ Yêu cầu đăng nhập (user hoặc admin)
        [HttpPost("add")]
        public async Task<IActionResult> Add([FromForm] int movieId, [FromForm] string content, [FromForm] int? rating)
        {
            try
            {
                // ✅ Lấy user ID từ claim
                var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (userIdClaim == null)
                {
                    return Unauthorized(new { success = false, message = "Vui lòng đăng nhập để bình luận." });
                }

                var userId = int.Parse(userIdClaim);

                if (string.IsNullOrWhiteSpace(content))
                {
                    return BadRequest(new { success = false, message = "Nội dung không được để trống." });
                }

                // ✅ Kiểm tra phim có tồn tại không
                var movieExists = await _context.Movies.AnyAsync(m => m.MovieId == movieId);
                if (!movieExists)
                {
                    return NotFound(new { success = false, message = "Phim không tồn tại." });
                }

                // ✅ Tạo mới comment
                var comment = new Comment
                {
                    MovieId = movieId,
                    UserId = userId,
                    Content = content.Trim(),
                    Rating = (rating.HasValue && rating > 0) ? rating : null,
                    CreatedAt = DateTime.Now,
                    IsActive = true
                };

                _context.Comments.Add(comment);
                await _context.SaveChangesAsync();

                // ✅ Lấy thông tin user để trả về
                var user = await _context.Users.FindAsync(userId);

                return Ok(new
                {
                    success = true,
                    message = "Đăng bình luận thành công!",
                    comment = new
                    {
                        commentId = comment.CommentId,
                        content = comment.Content,
                        createdAt = comment.CreatedAt,
                        rating = comment.Rating,
                        userName = user?.UserName ?? "Ẩn danh",
                        avatar = string.IsNullOrEmpty(user?.Avatar)
                            ? "/images/nouser.png"
                            : user.Avatar
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Lỗi máy chủ: " + ex.Message });
            }
        }

        // ============================================================
        // 💬 LẤY DANH SÁCH BÌNH LUẬN (AJAX)
        // ============================================================
        // === LIST JSON (sửa canDelete & null-safe user fields)
        [AllowAnonymous]
        [HttpGet("list-json")]
        public async Task<IActionResult> ListJson(int movieId)
        {
            // Lấy danh tính user hiện tại
            var currentUserName = User.Identity?.Name ?? "";
           // 🔴 SỬA ĐỔI: Kiểm tra quyền Admin bằng cách so sánh Username.
        // Việc này giải quyết lỗi khi User.IsInRole("Admin") bị lỗi dù Role đã được gán.
        var isCurrentUserAdmin = string.Equals(currentUserName, "admin", StringComparison.OrdinalIgnoreCase); 
        // ^^^^^^ Dùng cách này thay vì User.IsInRole("Admin"); ^^^^^^

        var currentUserIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var currentUserId = currentUserIdClaim != null ? int.Parse(currentUserIdClaim) : (int?)null;

            // Lấy tất cả bình luận phim này, kèm user
            var comments = await _context.Comments
                .Include(c => c.User)
                .Where(c => c.MovieId == movieId && c.IsActive == true)
                .OrderBy(c => c.CreatedAt)
                .ToListAsync();

            var commentTree = comments
                .Where(c => c.ParentCommentId == null)
                .Select(c => new
                {
                    commentId = c.CommentId,
                    content = c.Content,
                    createdAt = c.CreatedAt,
                    rating = c.Rating,
                    userName = c.User?.UserName ?? "Ẩn danh",
                    avatar = !string.IsNullOrEmpty(c.User?.Avatar) ? c.User.Avatar : "/images/nouser.png",
                    subscriptionType = c.User.SubscriptionType,
                    isAdmin = (c.User?.UserName?.ToLower() == "admin"), // còn giữ để hiển thị badge nếu muốn
                    // canDelete: admin hoặc chính chủ comment
                    canDelete = isCurrentUserAdmin || string.Equals(currentUserName, c.User?.UserName, StringComparison.OrdinalIgnoreCase),
                    replies = comments
                        .Where(r => r.ParentCommentId == c.CommentId)
                        .Select(r => new
                        {
                            commentId = r.CommentId,
                            content = r.Content,
                            createdAt = r.CreatedAt,
                            userName = r.User?.UserName ?? "Ẩn danh",
                            avatar = !string.IsNullOrEmpty(r.User?.Avatar) ? r.User.Avatar : "/images/nouser.png",
                            subscriptionType = r.User.SubscriptionType,
                            isAdmin = (r.User?.UserName?.ToLower() == "admin"),
                            canDelete = isCurrentUserAdmin || string.Equals(currentUserName, r.User?.UserName, StringComparison.OrdinalIgnoreCase)
                        })
                        .ToList()
                })
                .ToList();

            return Ok(new { success = true, comments = commentTree });
        }

        // ============================================================
        // 💬 TRẢ LỜI BÌNH LUẬN
        [Authorize] // ⚙️ Thêm dòng này
        [HttpPost("reply")]
        public async Task<IActionResult> Reply([FromForm] int parentId, [FromForm] int movieId, [FromForm] string content)
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("UserId")
            ?? User.FindFirstValue(ClaimTypes.Name);

            if (userIdClaim == null)
            {
                return Unauthorized(new { success = false, message = "Bạn cần đăng nhập để phản hồi." });
            }

            if (string.IsNullOrWhiteSpace(content))
            {
                return BadRequest(new { success = false, message = "Nội dung phản hồi không được để trống." });
            }

            var userId = int.Parse(userIdClaim);

            var parentComment = await _context.Comments.FindAsync(parentId);
            if (parentComment == null)
            {
                return NotFound(new { success = false, message = "Bình luận gốc không tồn tại." });
            }

            var reply = new Comment
            {
                MovieId = movieId,
                UserId = userId,
                ParentCommentId = parentId,
                Content = content.Trim(),
                CreatedAt = DateTime.Now,
                IsActive = true
            };

            _context.Comments.Add(reply);
            await _context.SaveChangesAsync();

            var user = await _context.Users.FindAsync(userId);

            return Ok(new
            {
                success = true,
                message = "Đã phản hồi!",
                reply = new
                {
                    reply.CommentId,
                    reply.Content,
                    reply.CreatedAt,
                    userName = user?.UserName,
                    avatar = !string.IsNullOrEmpty(user?.Avatar) ? user.Avatar : "/images/nouser.png"
                }
            });
        }

        // ============================================================
        // ❌ XÓA BÌNH LUẬN (Admin)
        [Authorize] // yêu cầu đăng nhập
        [HttpPost("delete")]
        public async Task<IActionResult> Delete([FromQuery] int commentId) // giữ [FromQuery] để JS hiện tại còn hợp lệ
        {
            var userName = User.Identity?.Name;
            if (string.IsNullOrEmpty(userName))
                return Unauthorized(new { success = false, message = "Bạn chưa đăng nhập." });

            var comment = await _context.Comments.FindAsync(commentId);
            if (comment == null)
                return NotFound(new { success = false, message = "Không tìm thấy bình luận cần xóa." });

            // admin có thể xóa tất cả; chủ comment có thể xóa comment của mình
           var isAdmin = string.Equals(userName, "admin", StringComparison.OrdinalIgnoreCase);
            var isOwner = string.Equals(userName, (await _context.Users.FindAsync(comment.UserId))?.UserName, StringComparison.OrdinalIgnoreCase);

            if (!isAdmin && !isOwner)
                return Forbid("Chỉ admin hoặc chủ bình luận mới được phép xóa.");
                
            // ✅ Logic đã đúng: Xóa comment nếu là Admin HOẶC là Owner
            _context.Comments.Remove(comment);
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Đã xóa bình luận thành công!" });
        }
    }

    }

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MovieWeb.Data;
using MovieWeb.Models.Entities;
using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;
using MovieWeb.Hubs;
using MovieWeb.Services.Interfaces;

namespace MovieWeb.Controllers
{
    // ✅ Chỉ route API comment qua controller này
    [Route("api/comment")]
    public class CommentController : Controller
    {
        private readonly MovieWebDbContext _context;
        private readonly IHubContext<NotificationHub> _notificationHubContext;
        private readonly INotificationService _notificationService;
        private readonly ILogger<CommentController> _logger;

        public CommentController
        (MovieWebDbContext context, 
        IHubContext<NotificationHub> notificationHubContext, 
        INotificationService notificationService,
        ILogger<CommentController> logger)
        {
            _context = context;
            _notificationHubContext = notificationHubContext;
            _notificationService = notificationService;
            _logger = logger;
        }

        // ============================================================
        // 💬 GỬI BÌNH LUẬN / ĐÁNH GIÁ
        // ============================================================
        [Authorize]
        [HttpPost("add")]
        public async Task<IActionResult> Add([FromForm] int movieId, [FromForm] string content, [FromForm] int? rating)
        {
            try
            {
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

                var movieExists = await _context.Movies.AnyAsync(m => m.MovieId == movieId);
                if (!movieExists)
                {
                    return NotFound(new { success = false, message = "Phim không tồn tại." });
                }

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
        [AllowAnonymous]
        [HttpGet("list-json")]
        public async Task<IActionResult> ListJson(int movieId)
        {
            var currentUserName = User.Identity?.Name ?? "";
            var isCurrentUserAdmin = string.Equals(currentUserName, "admin", StringComparison.OrdinalIgnoreCase);

            var currentUserIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var currentUserId = currentUserIdClaim != null ? int.Parse(currentUserIdClaim) : (int?)null;

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
                    isAdmin = (c.User?.UserName?.ToLower() == "admin"),
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
        // ============================================================
        [Authorize]
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

            var parentComment = await _context.Comments
                .Include(c => c.Movie)
                .Include(c => c.User)
                .FirstOrDefaultAsync(c => c.CommentId == parentId);

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

            var currentUser = await _context.Users.FindAsync(userId);

            // ========== GỬI THÔNG BÁO ==========
            try
            {
                var receiverId = parentComment.UserId;

                if (userId != receiverId)
                {
                    var senderName = currentUser?.UserName ?? "Ai đó";
                    var movieName = parentComment.Movie?.Name ?? "phim";
                    var notificationTitle = "💬 Phản hồi mới";
                    var notificationContent = $"{senderName} đã trả lời bình luận của bạn trong phim \"{movieName}\"";
                    var notificationUrl = $"/phim/{parentComment.Movie?.Slug}";

                    // 1️⃣ Lưu vào DB
                    await _notificationService.CreateNotificationAsync(
                        receiverId,
                        notificationTitle,
                        notificationContent,
                        "CommentReply",
                        notificationUrl
                    );

                    // 2️⃣ Gửi SignalR Real-time
                    await _notificationHubContext.Clients
                        .User(receiverId.ToString())
                        .SendAsync("ReceiveNotification", new
                        {
                            Title = notificationTitle,
                            Content = notificationContent,
                            Url = notificationUrl,
                            Type = "CommentReply",
                            CreatedAt = DateTime.Now, // ✅ ĐỔI: UTC -> Local Time
                            IsRead = false
                        });

                    _logger.LogInformation("✅ Sent CommentReply notification to User {ReceiverId}", receiverId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error sending CommentReply notification");
            }

            return Ok(new
            {
                success = true,
                message = "Đã phản hồi!",
                reply = new
                {
                    reply.CommentId,
                    reply.Content,
                    reply.CreatedAt,
                    userName = currentUser?.UserName,
                    avatar = !string.IsNullOrEmpty(currentUser?.Avatar) ? currentUser.Avatar : "/images/nouser.png"
                }
            });
        }

        // ============================================================
        // ❌ XÓA BÌNH LUẬN (Admin hoặc Owner)
        // ============================================================
        [Authorize]
        [HttpPost("delete")]
        public async Task<IActionResult> Delete([FromQuery] int commentId)
        {
            var userName = User.Identity?.Name;
            if (string.IsNullOrEmpty(userName))
                return Unauthorized(new { success = false, message = "Bạn chưa đăng nhập." });

            var comment = await _context.Comments.FindAsync(commentId);
            if (comment == null)
                return NotFound(new { success = false, message = "Không tìm thấy bình luận cần xóa." });

            var isAdmin = string.Equals(userName, "admin", StringComparison.OrdinalIgnoreCase);
            var isOwner = string.Equals(userName, (await _context.Users.FindAsync(comment.UserId))?.UserName, StringComparison.OrdinalIgnoreCase);

            if (!isAdmin && !isOwner)
                return Forbid("Chỉ admin hoặc chủ bình luận mới được phép xóa.");

            _context.Comments.Remove(comment);
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Đã xóa bình luận thành công!" });
        }
    }
}
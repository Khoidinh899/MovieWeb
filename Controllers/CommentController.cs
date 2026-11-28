using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MovieWeb.Data;
using MovieWeb.Models.Entities;
using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;
using MovieWeb.Hubs;
using MovieWeb.Services.Interfaces;
using MovieWeb.Helpers;

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
        public async Task<IActionResult> Add([FromForm] int movieId, [FromForm] string content, [FromForm] int? rating, [FromForm] bool isAnonymous = false)
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
                    IsActive = true,
                    IsAnonymous = isAnonymous
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
                        userName = CommentIdentity.PublicName(user, isAnonymous),
                        avatar = CommentIdentity.PublicAvatar(user, isAnonymous),
                        isAnonymous
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding comment");
                return StatusCode(500, new { success = false, message = "Lỗi máy chủ, vui lòng thử lại." });
            }
        }

        // ============================================================
        // 💬 LẤY DANH SÁCH BÌNH LUẬN (AJAX)
        // ============================================================
        [AllowAnonymous]
        [HttpGet("list-json")]
        public async Task<IActionResult> ListJson(int movieId)
        {
            var isCurrentUserAdmin = User.IsInRole("Admin");
            var currentUserId = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsedId) ? parsedId : (int?)null;

            var comments = await _context.Comments
                .Include(c => c.User)
                .Where(c => c.MovieId == movieId && c.IsActive == true)
                .OrderBy(c => c.CreatedAt)
                .ToListAsync();

            // Không bao giờ trả email/username đăng nhập ra ngoài; admin nhận thêm danh tính gốc (realIdentity).
            object ToView(Comment c, bool withReplies)
            {
                var anonymous = c.IsAnonymous == true;
                return new
                {
                    commentId = c.CommentId,
                    content = c.Content,
                    createdAt = c.CreatedAt,
                    rating = c.Rating,
                    userName = CommentIdentity.PublicName(c.User, anonymous),
                    avatar = CommentIdentity.PublicAvatar(c.User, anonymous),
                    subscriptionType = anonymous ? null : c.User?.SubscriptionType,
                    isAdmin = !anonymous && c.User?.RoleId == 1,
                    isAnonymous = anonymous,
                    isMine = currentUserId.HasValue && c.UserId == currentUserId,
                    realIdentity = isCurrentUserAdmin ? CommentIdentity.RealIdentity(c.User) : null,
                    canDelete = isCurrentUserAdmin || (currentUserId.HasValue && c.UserId == currentUserId),
                    replies = withReplies
                        ? comments.Where(r => r.ParentCommentId == c.CommentId).Select(r => ToView(r, false)).ToList()
                        : null
                };
            }

            var commentTree = comments
                .Where(c => c.ParentCommentId == null)
                .Select(c => ToView(c, true))
                .ToList();

            return Ok(new { success = true, comments = commentTree });
        }

        // ============================================================
        // 💬 TRẢ LỜI BÌNH LUẬN
        // ============================================================
        [Authorize]
        [HttpPost("reply")]
        public async Task<IActionResult> Reply([FromForm] int parentId, [FromForm] int movieId, [FromForm] string content, [FromForm] bool isAnonymous = false)
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
                IsActive = true,
                IsAnonymous = isAnonymous
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
                    var senderName = currentUser is null ? "Ai đó" : CommentIdentity.PublicName(currentUser, isAnonymous);
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
                    userName = CommentIdentity.PublicName(currentUser, isAnonymous),
                    avatar = CommentIdentity.PublicAvatar(currentUser, isAnonymous),
                    isAnonymous
                }
            });
        }

        // ============================================================
        // 🎭 DANH TÍNH KHI BÌNH LUẬN (tên hiển thị / biệt danh ẩn danh)
        // ============================================================
        [Authorize]
        [HttpGet("me")]
        public async Task<IActionResult> Me()
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
                return Unauthorized(new { success = false });
            var user = await _context.Users.FindAsync(userId);
            if (user is null) return Unauthorized(new { success = false });
            return Ok(new
            {
                success = true,
                displayName = CommentIdentity.PublicName(user, false),
                nickname = CommentIdentity.Nickname(user),
                hasFullName = !string.IsNullOrWhiteSpace(user.FullName),
                avatar = CommentIdentity.PublicAvatar(user, false)
            });
        }

        [Authorize]
        [HttpPost("nickname")]
        public async Task<IActionResult> SetNickname([FromForm] string? nickname)
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
                return Unauthorized(new { success = false, message = "Bạn chưa đăng nhập." });
            var error = CommentIdentity.ValidateNickname(nickname);
            if (error is not null) return BadRequest(new { success = false, message = error });
            var user = await _context.Users.FindAsync(userId);
            if (user is null) return Unauthorized(new { success = false, message = "Bạn chưa đăng nhập." });
            user.CommentNickname = nickname!.Trim();
            await _context.SaveChangesAsync();
            return Ok(new { success = true, message = "Đã đổi biệt danh.", nickname = user.CommentNickname });
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

            var isAdmin = User.IsInRole("Admin");
            var isOwner = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var currentId) && comment.UserId == currentId;

            if (!isAdmin && !isOwner)
                return Forbid("Chỉ admin hoặc chủ bình luận mới được phép xóa.");

            _context.Comments.Remove(comment);
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Đã xóa bình luận thành công!" });
        }
    }
}
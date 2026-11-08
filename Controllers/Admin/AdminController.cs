using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MovieWeb.Models.Entities;
using MovieWeb.Models.DTOs;
using MovieWeb.Data;
using MovieWeb.Services;
using Microsoft.Extensions.Caching.Memory;
using MovieWeb.Extensions;
using MovieWeb.Helpers;
using MovieWeb.Services.Interfaces;
using Hangfire;                    
using Microsoft.AspNetCore.SignalR; 
using MovieWeb.Hubs;             

namespace MovieWeb.Controllers
{
    [Authorize]
    public partial class AdminController : Controller
    {
        // === TẤT CẢ CÁC SERVICE ĐỀU NẰM Ở FILE CHÍNH ===
        private readonly UserManager<User> _userManager;
        private readonly MovieWebDbContext _context;
        private readonly IAuthService _authService;
        private readonly ILogger<AdminController> _logger;
        private readonly IMemoryCache _cache;
        private readonly INotificationService _notificationService;
        private readonly IBackgroundJobClient _backgroundJobClient;
        private readonly IHubContext<NotificationHub> _notificationHubContext;
        private readonly IVSMovService _vsMovService;
        private readonly IMovieSyncService _movieSyncService;
        private readonly IEmailService _emailService;
        private readonly IAnnouncementService _announcementService;
        private readonly ISiteSettingsService _siteSettingsService;
        private readonly IBroadcastService _broadcastService;

        // === CONSTRUCTOR CHÍNH (DÙNG CHUNG CHO TẤT CẢ FILE) ===
        public AdminController(
            UserManager<User> userManager,
            MovieWebDbContext context,
            IAuthService authService,
            ILogger<AdminController> logger,
            IMemoryCache cache,
            INotificationService notificationService,
            IBackgroundJobClient backgroundJobClient,
            IHubContext<NotificationHub> notificationHubContext,
            IVSMovService vsMovService,
            IMovieSyncService movieSyncService,
            IEmailService emailService,
            IAnnouncementService announcementService,
            ISiteSettingsService siteSettingsService,
            IBroadcastService broadcastService
        )
        {
            _userManager = userManager;
            _context = context;
            _authService = authService;
            _logger = logger;
            _cache = cache;
            _notificationService = notificationService;
            _backgroundJobClient = backgroundJobClient;
            _notificationHubContext = notificationHubContext;
            _vsMovService = vsMovService;
            _movieSyncService = movieSyncService;
            _emailService = emailService;
            _announcementService = announcementService;
            _siteSettingsService = siteSettingsService;
            _broadcastService = broadcastService;
        }

        // === CÁC HÀM HELPER DÙNG CHUNG ===

        // Middleware để kiểm tra quyền admin
        private async Task<bool> IsAdminAsync()
        {
            var currentUser = await _authService.GetCurrentUserAsync();
            return currentUser?.IsAdmin == true;
        }

        // Helper method to log admin actions
        private async Task LogAdminActionAsync(string action, string description, string? targetId = null)
        {
            try
            {
                var currentUser = await _authService.GetCurrentUserAsync();
                if (currentUser?.IsAdmin == true)
                {
                    var log = new AdminLog
                    {
                        AdminId = currentUser.Id,
                        Action = action,
                        Description = description,
                        TargetId = targetId,
                        TableName = "Users", // Cần chỉnh lại nếu log cho Movies, v.v.
                        RecordId = !string.IsNullOrEmpty(targetId) ? int.TryParse(targetId, out var id) ? id : null : null,
                        IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                        UserAgent = HttpContext.Request.Headers.UserAgent.ToString(),
                        CreatedAt = DateTime.Now
                    };

                    _context.AdminLogs.Add(log);
                    await _context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging admin action: {Action}", action);
            }
        }

        // Helper method: Generate slug from Vietnamese text
        // (Hàm này chỉ dùng cho Movies, nhưng để đây cũng đc)
        private string GenerateSlug(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";

            // Convert to lowercase
            text = text.ToLower().Trim();

            // Replace Vietnamese characters
            text = text.Replace("à", "a").Replace("á", "a").Replace("ạ", "a").Replace("ả", "a").Replace("ã", "a")
                        .Replace("â", "a").Replace("ầ", "a").Replace("ấ", "a").Replace("ậ", "a").Replace("ẩ", "a").Replace("ẫ", "a")
                        .Replace("ă", "a").Replace("ằ", "a").Replace("ắ", "a").Replace("ặ", "a").Replace("ẳ", "a").Replace("ẵ", "a")
                        .Replace("è", "e").Replace("é", "e").Replace("ẹ", "e").Replace("ẻ", "e").Replace("ẽ", "e")
                        .Replace("ê", "e").Replace("ề", "e").Replace("ế", "e").Replace("ệ", "e").Replace("ể", "e").Replace("ễ", "e")
                        .Replace("ì", "i").Replace("í", "i").Replace("ị", "i").Replace("ỉ", "i").Replace("ĩ", "i")
                        .Replace("ò", "o").Replace("ó", "o").Replace("ọ", "o").Replace("ỏ", "o").Replace("õ", "o")
                        .Replace("ô", "o").Replace("ồ", "o").Replace("ố", "o").Replace("ộ", "o").Replace("ổ", "o").Replace("ỗ", "o")
                        .Replace("ơ", "o").Replace("ờ", "o").Replace("ớ", "o").Replace("ợ", "o").Replace("ở", "o").Replace("ỡ", "o")
                        .Replace("ù", "u").Replace("ú", "u").Replace("ụ", "u").Replace("ủ", "u").Replace("ũ", "u")
                        .Replace("ư", "u").Replace("ừ", "u").Replace("ứ", "u").Replace("ự", "u").Replace("ử", "u").Replace("ữ", "u")
                        .Replace("ỳ", "y").Replace("ý", "y").Replace("ỵ", "y").Replace("ỷ", "y").Replace("ỹ", "y")
                        .Replace("đ", "d");

            // Remove special characters
            text = System.Text.RegularExpressions.Regex.Replace(text, @"[^a-z0-9\s-]", "");

            // Replace spaces with hyphens
            text = System.Text.RegularExpressions.Regex.Replace(text, @"\s+", "-");

            // Remove consecutive hyphens
            text = System.Text.RegularExpressions.Regex.Replace(text, @"-+", "-");

            return text.Trim('-');
        }


        // === CÁC ACTION CỐT LÕI (CORE) ===

        // GET: /Admin/Dashboard
        public async Task<IActionResult> Dashboard()
        {
            if (!await IsAdminAsync())
            {
                return Forbid();
            }

            try
            {
                var stats = new
                {
                    TotalUsers = await _context.Users.CountAsync(),
                    ActiveUsers = await _context.Users.CountAsync(u => u.IsActive == true),
                    AdminUsers = await _context.Users.CountAsync(u => u.RoleId == 1),
                    RecentRegistrations = await _context.Users
                        .Where(u => u.CreatedAt >= DateTime.Now.AddDays(-7))
                        .CountAsync(),
                    UnconfirmedEmails = await _context.Users.CountAsync(u => !u.EmailConfirmed),

                    PendingRequests = await _context.RequestsMovies.CountAsync(r => r.Status == RequestStatus.Pending),
                    ProcessingRequests = await _context.RequestsMovies.CountAsync(r => r.Status == RequestStatus.Processing),
                    VerificationRequests = await _context.RequestsMovies.CountAsync(r => r.Status == RequestStatus.NeedsVerification)
                };

                ViewBag.Stats = stats;
                ViewBag.AnnouncementSettings = await _announcementService.GetSettingsAsync();
                return View("Dashboard");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading admin dashboard");
                TempData["ErrorMessage"] = "Có lỗi xảy ra khi tải dashboard";
                return RedirectToAction("TrangChu", "TrangChu");
            }
        }

        // GET: /Admin/AdminLogs
        public async Task<IActionResult> AdminLogs(int page = 1, int pageSize = 50)
        {
            if (!await IsAdminAsync())
            {
                return Forbid();
            }

            try
            {
                var totalLogs = await _context.AdminLogs.CountAsync();
                var logs = await _context.AdminLogs
                    .Include(l => l.Admin)
                    .OrderByDescending(l => l.CreatedAt)
                    .Skip((Math.Max(page, 1) - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                ViewBag.TotalPages = (int)Math.Ceiling((double)totalLogs / pageSize);
                ViewBag.CurrentPage = page;
                ViewBag.TotalLogs = totalLogs;

                return View(logs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading admin logs");
                TempData["ErrorMessage"] = "Có lỗi xảy ra khi tải nhật ký admin";
                return RedirectToAction("Dashboard");
            }
        }
        // =================================================================
        // VSMov backfill từ trang quản lý phim (POST + CSRF + chỉ Admin)
        // Đã bỏ RunBackfillJob (không còn dùng). Giới hạn 20 trang mỗi lần,
        // không cho chạy đồng thời.
        // =================================================================
        private const int MaxVSMovBackfillPages = 20;
        private static readonly SemaphoreSlim VSMovBackfillGate = new(1, 1);

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RunVSMovBackfillFromPanel([FromForm] int? fromPage, [FromForm] int? toPage)
        {
            if (!await IsAdminAsync()) return Forbid();

            var (ok, message) = await RunVSMovBackfillCoreAsync(fromPage, toPage);
            TempData[ok ? "SuccessMessage" : "ErrorMessage"] = message;
            return RedirectToAction("Movies");
        }

        // GET cũ: vẫn hoạt động cho Admin đã đăng nhập (trước đây là AllowAnonymous).
        // Chưa trả 410 vì chưa đủ bằng chứng không còn bên ngoài nào gọi.
        [HttpGet("run-vsmov-backfill-123xyz")]
        public async Task<IActionResult> RunVSMovBackfill([FromQuery] int? fromPage, [FromQuery] int? toPage)
        {
            if (!await IsAdminAsync()) return Forbid();

            var (ok, message) = await RunVSMovBackfillCoreAsync(fromPage, toPage);
            return ok ? Ok(message) : BadRequest(message);
        }

        private async Task<(bool Ok, string Message)> RunVSMovBackfillCoreAsync(int? fromPage, int? toPage)
        {
            int start = fromPage ?? 1;
            int end = toPage ?? 10;
            if (start < 1 || end < start || end - start + 1 > MaxVSMovBackfillPages)
                return (false, $"Khoảng trang không hợp lệ. Tối đa {MaxVSMovBackfillPages} trang mỗi lần, bắt đầu từ trang 1.");

            if (!await VSMovBackfillGate.WaitAsync(0))
                return (false, "Đang có tác vụ VSMov backfill chạy. Vui lòng thử lại sau.");

            try
            {
                _logger.LogWarning("[VSMov Backfill] Admin chạy đồng bộ trang {Start} đến {End}", start, end);
                await _movieSyncService.BackfillFromVSMovAsync(start, end);
                return (true, $"Đã chạy xong VSMov Backfill (trang {start} đến {end}).");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[VSMov Backfill] Lỗi khi chạy trang {Start} đến {End}", start, end);
                return (false, "Lỗi server khi chạy VSMov Backfill. Xem log để biết chi tiết.");
            }
            finally
            {
                VSMovBackfillGate.Release();
            }
        }
    }
}
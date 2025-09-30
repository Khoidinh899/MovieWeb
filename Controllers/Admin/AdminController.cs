using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MovieWeb.Models.Entities;
using MovieWeb.Models.DTOs;
using MovieWeb.Data;
using MovieWeb.Services;

namespace MovieWeb.Controllers
{
    [Authorize]
    public class AdminController : Controller
    {
        private readonly UserManager<User> _userManager;
        private readonly MovieWebDbContext _context;
        private readonly IAuthService _authService;
        private readonly ILogger<AdminController> _logger;

        public AdminController(
            UserManager<User> userManager,
            MovieWebDbContext context,
            IAuthService authService,
            ILogger<AdminController> logger)
        {
            _userManager = userManager;
            _context = context;
            _authService = authService;
            _logger = logger;
        }

        // Middleware để kiểm tra quyền admin
        private async Task<bool> IsAdminAsync()
        {
            var currentUser = await _authService.GetCurrentUserAsync();
            return currentUser?.IsAdmin == true;
        }

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
                    UnconfirmedEmails = await _context.Users.CountAsync(u => !u.EmailConfirmed)
                };

                ViewBag.Stats = stats;
                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading admin dashboard");
                TempData["ErrorMessage"] = "Có lỗi xảy ra khi tải dashboard";
                return RedirectToAction("TrangChu", "TrangChu");
            }
        }

        // GET: /Admin/Users
        public async Task<IActionResult> Users(string? search, int? roleId, bool? isActive, int page = 1, int pageSize = 20)
        {
            if (!await IsAdminAsync())
            {
                return Forbid();
            }

            try
            {
                var query = _context.Users.Include(u => u.Role).AsQueryable();

                // Tìm kiếm
                if (!string.IsNullOrWhiteSpace(search))
                {
                    query = query.Where(u =>
                        u.UserName!.Contains(search) ||
                        u.Email!.Contains(search) ||
                        u.FirstName!.Contains(search) ||
                        u.LastName!.Contains(search));
                    ViewBag.Search = search;
                }

                // Lọc theo role
                if (roleId.HasValue)
                {
                    query = query.Where(u => u.RoleId == roleId.Value);
                    ViewBag.RoleId = roleId.Value;
                }

                // Lọc theo trạng thái
                if (isActive.HasValue)
                {
                    query = query.Where(u => u.IsActive == isActive.Value);
                    ViewBag.IsActive = isActive.Value;
                }

                // Phân trang
                var totalUsers = await query.CountAsync();
                var users = await query
                    .OrderByDescending(u => u.CreatedAt)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(u => new UserProfileDto
                    {
                        UserId = u.Id,
                        Username = u.UserName!,
                        Email = u.Email!,
                        FirstName = u.FirstName,
                        LastName = u.LastName,
                        Avatar = u.Avatar,
                        IsActive = u.IsActive ?? true,
                        CreatedAt = u.CreatedAt ?? DateTime.MinValue,
                        LastLogin = u.LastLogin,
                        IsAdmin = u.RoleId == 1
                    })
                    .ToListAsync();

                ViewBag.TotalPages = (int)Math.Ceiling((double)totalUsers / pageSize);
                ViewBag.CurrentPage = page;
                ViewBag.PageSize = pageSize;
                ViewBag.TotalUsers = totalUsers;

                // Load roles cho dropdown
                ViewBag.Roles = await _context.Roles.ToListAsync();

                return View(users);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading users list");
                TempData["ErrorMessage"] = "Có lỗi xảy ra khi tải danh sách người dùng";
                return RedirectToAction("Dashboard");
            }
        }

        // GET: /Admin/UserDetail/5
        public async Task<IActionResult> UserDetail(int id)
        {
            if (!await IsAdminAsync())
            {
                return Forbid();
            }

            try
            {
                var user = await _context.Users
                    .Include(u => u.Role)
                    .FirstOrDefaultAsync(u => u.Id == id);

                if (user == null)
                {
                    TempData["ErrorMessage"] = "Người dùng không tồn tại";
                    return RedirectToAction("Users");
                }

                var userDetail = new UserProfileDto
                {
                    UserId = user.Id,
                    Username = user.UserName!,
                    Email = user.Email!,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    Avatar = user.Avatar,
                    IsActive = user.IsActive ?? true,
                    CreatedAt = user.CreatedAt ?? DateTime.MinValue,
                    LastLogin = user.LastLogin,
                    IsAdmin = user.RoleId == 1
                };

                // Load thống kê của user
                ViewBag.UserStats = new
                {
                    TotalComments = await _context.Comments.CountAsync(c => c.UserId == id),
                    TotalFavorites = await _context.Favorites.CountAsync(f => f.UserId == id),
                    TotalRatings = await _context.Ratings.CountAsync(r => r.UserId == id),
                    WatchHistory = await _context.WatchHistories.CountAsync(w => w.UserId == id)
                };

                ViewBag.Roles = await _context.Roles.ToListAsync();

                return View(userDetail);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading user detail for ID: {UserId}", id);
                TempData["ErrorMessage"] = "Có lỗi xảy ra khi tải thông tin người dùng";
                return RedirectToAction("Users");
            }
        }

        // POST: /Admin/UpdateUserStatus
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateUserStatus(int userId, bool isActive)
        {
            if (!await IsAdminAsync())
            {
                return Json(new { success = false, message = "Không có quyền truy cập" });
            }

            try
            {
                var currentUser = await _authService.GetCurrentUserAsync();
                if (currentUser?.Id == userId)
                {
                    return Json(new { success = false, message = "Không thể thay đổi trạng thái của chính mình" });
                }

                var user = await _userManager.FindByIdAsync(userId.ToString());
                if (user == null)
                {
                    return Json(new { success = false, message = "Người dùng không tồn tại" });
                }

                user.IsActive = isActive;
                user.UpdatedAt = DateTime.Now;

                var result = await _userManager.UpdateAsync(user);
                if (result.Succeeded)
                {
                    // Log admin action
                    await LogAdminActionAsync("UPDATE_USER_STATUS", $"Changed user {user.UserName} status to {(isActive ? "Active" : "Inactive")}", userId.ToString());

                    _logger.LogInformation("Admin {AdminId} updated user {UserId} status to {Status}",
                        currentUser?.Id, userId, isActive ? "Active" : "Inactive");

                    return Json(new { success = true, message = $"Đã {(isActive ? "kích hoạt" : "khóa")} tài khoản thành công" });
                }

                return Json(new { success = false, message = "Có lỗi xảy ra khi cập nhật trạng thái" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating user status for ID: {UserId}", userId);
                return Json(new { success = false, message = "Có lỗi xảy ra khi cập nhật trạng thái" });
            }
        }

        // POST: /Admin/UpdateUserRole
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateUserRole(int userId, int roleId)
        {
            if (!await IsAdminAsync())
            {
                return Json(new { success = false, message = "Không có quyền truy cập" });
            }

            try
            {
                var currentUser = await _authService.GetCurrentUserAsync();
                if (currentUser?.Id == userId)
                {
                    return Json(new { success = false, message = "Không thể thay đổi quyền của chính mình" });
                }

                var user = await _userManager.FindByIdAsync(userId.ToString());
                if (user == null)
                {
                    return Json(new { success = false, message = "Người dùng không tồn tại" });
                }

                var role = await _context.Roles.FindAsync(roleId);
                if (role == null)
                {
                    return Json(new { success = false, message = "Vai trò không tồn tại" });
                }

                var oldRoleId = user.RoleId;
                user.RoleId = roleId;
                user.UpdatedAt = DateTime.Now;

                var result = await _userManager.UpdateAsync(user);
                if (result.Succeeded)
                {
                    // Log admin action
                    await LogAdminActionAsync("UPDATE_USER_ROLE", $"Changed user {user.UserName} role from {oldRoleId} to {roleId}", userId.ToString());

                    _logger.LogInformation("Admin {AdminId} updated user {UserId} role to {RoleId}",
                        currentUser?.Id, userId, roleId);

                    return Json(new { success = true, message = $"Đã cập nhật quyền thành {role.Name}" });
                }

                return Json(new { success = false, message = "Có lỗi xảy ra khi cập nhật quyền" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating user role for ID: {UserId}", userId);
                return Json(new { success = false, message = "Có lỗi xảy ra khi cập nhật quyền" });
            }
        }

        // POST: /Admin/DeleteUser
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteUser(int userId)
        {
            if (!await IsAdminAsync())
            {
                return Json(new { success = false, message = "Không có quyền truy cập" });
            }

            try
            {
                var currentUser = await _authService.GetCurrentUserAsync();
                if (currentUser?.Id == userId)
                {
                    return Json(new { success = false, message = "Không thể xóa chính mình" });
                }

                var user = await _userManager.FindByIdAsync(userId.ToString());
                if (user == null)
                {
                    return Json(new { success = false, message = "Người dùng không tồn tại" });
                }

                // Kiểm tra user có dữ liệu liên quan không
                var hasRelatedData = await _context.Comments.AnyAsync(c => c.UserId == userId) ||
                                   await _context.Favorites.AnyAsync(f => f.UserId == userId) ||
                                   await _context.Ratings.AnyAsync(r => r.UserId == userId) ||
                                   await _context.WatchHistories.AnyAsync(w => w.UserId == userId);

                if (hasRelatedData)
                {
                    return Json(new { success = false, message = "Không thể xóa user có dữ liệu liên quan. Hãy khóa tài khoản thay thế." });
                }

                var result = await _userManager.DeleteAsync(user);
                if (result.Succeeded)
                {
                    // Log admin action
                    await LogAdminActionAsync("DELETE_USER", $"Deleted user {user.UserName} (ID: {userId})", userId.ToString());

                    _logger.LogInformation("Admin {AdminId} deleted user {UserId}", currentUser?.Id, userId);

                    return Json(new { success = true, message = "Đã xóa người dùng thành công" });
                }

                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                return Json(new { success = false, message = $"Có lỗi xảy ra: {errors}" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting user ID: {UserId}", userId);
                return Json(new { success = false, message = "Có lỗi xảy ra khi xóa người dùng" });
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
                    .Skip((page - 1) * pageSize)
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
                        TargetId = targetId,  // Giữ nguyên
                        TableName = "Users",  // Thêm nếu cần
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
    }
}
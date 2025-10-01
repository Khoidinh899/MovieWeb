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
                return View("Dashboard"); // ✅ Fix: trỏ view đúng tên
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

                if (!string.IsNullOrWhiteSpace(search))
                {
                    query = query.Where(u =>
                        u.UserName!.Contains(search) ||
                        u.Email!.Contains(search) ||
                        u.FirstName!.Contains(search) ||
                        u.LastName!.Contains(search));
                    ViewBag.Search = search;
                }

                if (roleId.HasValue)
                {
                    query = query.Where(u => u.RoleId == roleId.Value);
                    ViewBag.RoleId = roleId.Value;
                }

                if (isActive.HasValue)
                {
                    query = query.Where(u => u.IsActive == isActive.Value);
                    ViewBag.IsActive = isActive.Value;
                }

                var totalUsers = await query.CountAsync();
                var userList = await query
                    .OrderByDescending(u => u.CreatedAt)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                var users = new List<UserProfileDto>();
                foreach (var user in userList)
                {
                    var userDto = new UserProfileDto
                    {
                        UserId = user.Id,
                        Username = user.UserName!,
                        Email = user.Email!,
                        FirstName = user.FirstName ?? "",
                        LastName = user.LastName ?? "",
                        Avatar = user.Avatar,
                        IsActive = user.IsActive ?? true,
                        EmailConfirmed = user.EmailConfirmed,
                        CreatedAt = user.CreatedAt ?? DateTime.MinValue,
                        UpdatedAt = user.UpdatedAt,
                        LastLogin = user.LastLogin,
                        RoleId = user.RoleId,
                        TotalComments = await _context.Comments.CountAsync(c => c.UserId == user.Id),
                        TotalFavorites = await _context.Favorites.CountAsync(f => f.UserId == user.Id),
                        TotalRatings = await _context.Ratings.CountAsync(r => r.UserId == user.Id),
                        TotalWatchHistory = await _context.WatchHistories.CountAsync(w => w.UserId == user.Id)
                    };
                    users.Add(userDto);
                }

                ViewBag.TotalPages = (int)Math.Ceiling((double)totalUsers / pageSize);
                ViewBag.CurrentPage = page;
                ViewBag.PageSize = pageSize;
                ViewBag.TotalUsers = totalUsers;

                ViewBag.Roles = await _context.Roles.ToListAsync();

                return View("Users", users); // ✅ Fix: trỏ view đúng tên
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
                    FirstName = user.FirstName ?? "",
                    LastName = user.LastName ?? "",
                    Avatar = user.Avatar,
                    IsActive = user.IsActive ?? true,
                    EmailConfirmed = user.EmailConfirmed,
                    CreatedAt = user.CreatedAt ?? DateTime.MinValue,
                    UpdatedAt = user.UpdatedAt,
                    LastLogin = user.LastLogin,
                    RoleId = user.RoleId,
                    PhoneNumber = user.PhoneNumber,
                    DateOfBirth = user.DateOfBirth,
                    Gender = user.Gender,
                    Address = user.Address,
                    Bio = user.Bio
                };

                ViewBag.UserStats = new
                {
                    TotalComments = await _context.Comments.CountAsync(c => c.UserId == id),
                    TotalFavorites = await _context.Favorites.CountAsync(f => f.UserId == id),
                    TotalRatings = await _context.Ratings.CountAsync(r => r.UserId == id),
                    WatchHistory = await _context.WatchHistories.CountAsync(w => w.UserId == id)
                };

                ViewBag.Roles = await _context.Roles.ToListAsync();

                return View("UserDetail", userDetail); // ✅ Fix: trỏ view đúng tên
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
// GET: /Admin/GetUserData/{id} - Lấy dữ liệu user để edit
[HttpGet]
public async Task<IActionResult> GetUserData(int id)
{
    if (!await IsAdminAsync())
    {
        return Json(new { success = false, message = "Không có quyền truy cập" });
    }

    try
    {
        var user = await _context.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Id == id);

        if (user == null)
        {
            return Json(new { success = false, message = "Không tìm thấy người dùng" });
        }

        return Json(new
        {
            success = true,
            data = new
            {
                userId = user.Id,
                username = user.UserName,
                email = user.Email,
                firstName = user.FirstName,
                lastName = user.LastName,
                roleId = user.RoleId,
                isActive = user.IsActive,
                phoneNumber = user.PhoneNumber,
                gender = user.Gender,
                dateOfBirth = user.DateOfBirth?.ToString("yyyy-MM-dd"),
                address = user.Address,
                bio = user.Bio
            }
        });
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error getting user data for ID: {UserId}", id);
        return Json(new { success = false, message = "Có lỗi xảy ra khi tải dữ liệu" });
    }
}

// POST: /Admin/CreateUser - Tạo user mới
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> CreateUser(CreateUserDto model)
{
    if (!await IsAdminAsync())
    {
        return Json(new { success = false, message = "Không có quyền truy cập" });
    }

    try
    {
        if (!ModelState.IsValid)
        {
            var errors = string.Join(", ", ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage));
            return Json(new { success = false, message = errors });
        }

        // Kiểm tra email đã tồn tại
        if (await _userManager.FindByEmailAsync(model.Email) != null)
        {
            return Json(new { success = false, message = "Email đã được sử dụng" });
        }

        // Kiểm tra username đã tồn tại
        if (await _userManager.FindByNameAsync(model.Username) != null)
        {
            return Json(new { success = false, message = "Username đã được sử dụng" });
        }

        var user = new User
        {
            UserName = model.Username,
            Email = model.Email,
            FirstName = model.FirstName,
            LastName = model.LastName,
            RoleId = model.RoleId,
            IsActive = model.IsActive,
            EmailConfirmed = true, // Admin tạo thì auto confirm
            PhoneNumber = model.PhoneNumber,
            Gender = model.Gender,
            DateOfBirth = model.DateOfBirth,
            Address = model.Address,
            Bio = model.Bio,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        };

        var result = await _userManager.CreateAsync(user, model.Password);
        
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            return Json(new { success = false, message = errors });
        }

        // Log admin action
        await LogAdminActionAsync("CREATE_USER", $"Created new user: {user.UserName}", user.Id.ToString());

        _logger.LogInformation("Admin created new user: {Username}", user.UserName);

        return Json(new { success = true, message = "Tạo người dùng thành công!" });
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error creating user");
        return Json(new { success = false, message = "Có lỗi xảy ra khi tạo người dùng" });
    }
}

// POST: /Admin/UpdateUser - Cập nhật thông tin user
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> UpdateUser(UpdateUserDto model)
{
    if (!await IsAdminAsync())
    {
        return Json(new { success = false, message = "Không có quyền truy cập" });
    }

    try
    {
        if (!ModelState.IsValid)
        {
            var errors = string.Join(", ", ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage));
            return Json(new { success = false, message = errors });
        }

        var user = await _userManager.FindByIdAsync(model.UserId.ToString());
        if (user == null)
        {
            return Json(new { success = false, message = "Không tìm thấy người dùng" });
        }

        // Kiểm tra email đã tồn tại (trừ user hiện tại)
        var existingEmailUser = await _userManager.FindByEmailAsync(model.Email);
        if (existingEmailUser != null && existingEmailUser.Id != user.Id)
        {
            return Json(new { success = false, message = "Email đã được sử dụng bởi người dùng khác" });
        }

        // Cập nhật thông tin
        user.Email = model.Email;
        user.FirstName = model.FirstName;
        user.LastName = model.LastName;
        user.RoleId = model.RoleId;
        user.IsActive = model.IsActive;
        user.PhoneNumber = model.PhoneNumber;
        user.Gender = model.Gender;
        user.DateOfBirth = model.DateOfBirth;
        user.Address = model.Address;
        user.Bio = model.Bio;
        user.UpdatedAt = DateTime.Now;

        var result = await _userManager.UpdateAsync(user);
        
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            return Json(new { success = false, message = errors });
        }

        // Đổi mật khẩu nếu có
        if (!string.IsNullOrWhiteSpace(model.NewPassword))
        {
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var passwordResult = await _userManager.ResetPasswordAsync(user, token, model.NewPassword);
            
            if (!passwordResult.Succeeded)
            {
                var errors = string.Join(", ", passwordResult.Errors.Select(e => e.Description));
                return Json(new { success = false, message = "Cập nhật thông tin thành công nhưng đổi mật khẩu thất bại: " + errors });
            }
        }

        // Log admin action
        await LogAdminActionAsync("UPDATE_USER", $"Updated user: {user.UserName}", user.Id.ToString());

        _logger.LogInformation("Admin updated user: {Username}", user.UserName);

        return Json(new { success = true, message = "Cập nhật người dùng thành công!" });
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error updating user");
        return Json(new { success = false, message = "Có lỗi xảy ra khi cập nhật" });
    }
}
    }
}
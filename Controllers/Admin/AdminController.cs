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

namespace MovieWeb.Controllers
{
    [Authorize]
    public class AdminController : Controller
    {
        private readonly UserManager<User> _userManager;
        private readonly MovieWebDbContext _context;
        private readonly IAuthService _authService;
        private readonly ILogger<AdminController> _logger;
        private readonly IMemoryCache _cache;

        public AdminController(
            UserManager<User> userManager,
            MovieWebDbContext context,
            IAuthService authService,
            ILogger<AdminController> logger,
            IMemoryCache cache)
        {
            _userManager = userManager;
            _context = context;
            _authService = authService;
            _logger = logger;
            _cache = cache;
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
        // GET: /Admin/Subscriptions
        public async Task<IActionResult> Subscriptions()
        {
            try
            {
                // Lấy tất cả user có subscription (premium/student)
                var users = await _context.Users
                    .Include(u => u.Role)
                    .Where(u => u.SubscriptionType == "premium" || u.SubscriptionType == "student")
                    .OrderByDescending(u => u.SubscriptionEndDate)
                    .ToListAsync();

                // Mapping User -> DTO (giống Users action)
                var subscriptions = users.Select(u => u.ToUserProfileDto()).ToList();

                // Một số thống kê nhanh
                ViewBag.TotalSubscriptions = subscriptions.Count;
                ViewBag.ActiveSubscriptions = subscriptions.Count(u => u.SubscriptionEndDate.HasValue && u.SubscriptionEndDate > DateTime.Now);
                ViewBag.ExpiredSubscriptions = subscriptions.Count(u => u.SubscriptionEndDate.HasValue && u.SubscriptionEndDate <= DateTime.Now);

                return View("Subscriptions", subscriptions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading subscriptions page");
                TempData["ErrorMessage"] = "Có lỗi xảy ra khi tải trang subscriptions";
                return RedirectToAction("Dashboard");
            }
        }

        // GET: /Admin/Users
        public async Task<IActionResult> Users(string? search, int? roleId, bool? isActive, string? subscriptionType, int page = 1, int pageSize = 20)
        {
            if (!await IsAdminAsync())
                return Forbid();

            try
            {
                var query = _context.Users.Include(u => u.Role).AsQueryable();

                // Filter subscriptionType
                if (!string.IsNullOrEmpty(subscriptionType))
                {
                    query = query.Where(u => u.SubscriptionType == subscriptionType);
                    ViewBag.SubscriptionType = subscriptionType;
                }

                // Search
                if (!string.IsNullOrWhiteSpace(search))
                {
                    query = query.Where(u =>
                        u.UserName!.Contains(search) ||
                        u.Email!.Contains(search) ||
                        u.FirstName!.Contains(search) ||
                        u.LastName!.Contains(search));
                    ViewBag.Search = search;
                }

                // Filter Role
                if (roleId.HasValue)
                {
                    query = query.Where(u => u.RoleId == roleId.Value);
                    ViewBag.RoleId = roleId.Value;
                }

                // Filter Active
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

                // Mapping User -> UserProfileDto
                var users = userList.Select(u => u.ToUserProfileDto()).ToList();

                // Premium users count
                ViewBag.PremiumUsers = await _context.Users
                    .CountAsync(u => (u.SubscriptionType == "premium" || u.SubscriptionType == "student")
                        && u.SubscriptionEndDate.HasValue
                        && u.SubscriptionEndDate.Value > DateTime.Now);

                ViewBag.TotalPages = (int)Math.Ceiling((double)totalUsers / pageSize);
                ViewBag.CurrentPage = page;
                ViewBag.PageSize = pageSize;
                ViewBag.TotalUsers = totalUsers;
                ViewBag.Roles = await _context.Roles.ToListAsync();

                return View("Users", users);
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
                    .Include(u => u.Favorites)
                    .Include(u => u.Comments)
                    .Include(u => u.Ratings)
                    .Include(u => u.WatchHistories)
                    .FirstOrDefaultAsync(u => u.Id == id);

                if (user == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy người dùng" });
                }

                // Mapping sang DTO
                var userDto = user.ToUserProfileDto();

                return Json(new
                {
                    success = true,
                    data = new
                    {
                        userDto.UserId,
                        userDto.Username,
                        userDto.Email,
                        userDto.FirstName,
                        userDto.LastName,
                        userDto.RoleId,
                        userDto.IsActive,
                        userDto.PhoneNumber,
                        userDto.Gender,
                        DateOfBirth = userDto.DateOfBirth?.ToString("yyyy-MM-dd"),
                        userDto.Address,
                        userDto.Bio,
                        // Subscription info
                        userDto.SubscriptionType,
                        SubscriptionStartDate = userDto.SubscriptionStartDate?.ToString("yyyy-MM-dd"),
                        SubscriptionEndDate = userDto.SubscriptionEndDate?.ToString("yyyy-MM-dd"),
                        userDto.IsPremium
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
        // GET: /Admin/Movies
        public async Task<IActionResult> Movies(string? search, string? type, bool? isActive, bool? isManual, int page = 1, int pageSize = 20)
        {
            if (!await IsAdminAsync())
            {
                return Forbid();
            }

            try
            {
                var query = _context.Movies
                    .Include(m => m.Categories)
                    .Include(m => m.Countries)
                    .AsQueryable();

                // Search
                if (!string.IsNullOrWhiteSpace(search))
                {
                    query = query.Where(m =>
                        m.Name.Contains(search) ||
                        m.OriginalName.Contains(search) ||
                        m.Slug.Contains(search));
                    ViewBag.Search = search;
                }

                // Filter by type
                if (!string.IsNullOrWhiteSpace(type))
                {
                    query = query.Where(m => m.Type == type);
                    ViewBag.Type = type;
                }

                // Filter by active status
                if (isActive.HasValue)
                {
                    query = query.Where(m => m.IsActive == isActive.Value);
                    ViewBag.IsActive = isActive.Value;
                }

                // Filter by manual/API source
                if (isManual.HasValue)
                {
                    if (isManual.Value)
                        query = query.Where(m => m.ApiId == null);
                    else
                        query = query.Where(m => m.ApiId != null);
                    ViewBag.IsManual = isManual.Value;
                }

                var totalMovies = await query.CountAsync();
                var movieList = await query
                    .OrderByDescending(m => m.CreatedAt)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                var movies = new List<AdminMovieListDto>();
                foreach (var movie in movieList)
                {
                    var movieDto = new AdminMovieListDto
                    {
                        MovieId = movie.MovieId,
                        Slug = movie.Slug,
                        Name = movie.Name,
                        OriginalName = movie.OriginalName,
                        PosterUrl = movie.PosterUrl,
                        Type = movie.Type,
                        Status = movie.Status,
                        Quality = movie.Quality,
                        Year = movie.Year,
                        ViewCount = movie.ViewCount ?? 0,
                        Rating = movie.Rating ?? 0,
                        RatingCount = movie.RatingCount ?? 0,
                        IsRecommended = movie.IsRecommended ?? false,
                        IsBanner = movie.IsBanner ?? false, // ✅ THÊM DÒNG NÀY

                        IsActive = movie.IsActive ?? true,
                        IsManual = string.IsNullOrEmpty(movie.ApiId),
                        CreatedAt = movie.CreatedAt ?? DateTime.MinValue,
                        UpdatedAt = movie.UpdatedAt ?? DateTime.MinValue,
                        TotalComments = await _context.Comments.CountAsync(c => c.MovieId == movie.MovieId),
                        TotalFavorites = await _context.Favorites.CountAsync(f => f.MovieId == movie.MovieId),
                        Categories = movie.Categories.Select(c => c.Name).ToList(),
                        Countries = movie.Countries.Select(c => c.Name).ToList()
                    };
                    movies.Add(movieDto);
                }

                ViewBag.TotalPages = (int)Math.Ceiling((double)totalMovies / pageSize);
                ViewBag.CurrentPage = page;
                ViewBag.PageSize = pageSize;
                ViewBag.TotalMovies = totalMovies;
                ViewBag.ActiveMovies = await _context.Movies.CountAsync(m => m.IsActive == true);
                ViewBag.ManualMovies = await _context.Movies.CountAsync(m => m.ApiId == null);
                ViewBag.ApiMovies = await _context.Movies.CountAsync(m => m.ApiId != null);

                // Load categories và countries cho dropdown
                ViewBag.Categories = await _context.Categories.OrderBy(c => c.Name).ToListAsync();
                ViewBag.Countries = await _context.Countries.OrderBy(c => c.Name).ToListAsync();

                return View("Movies", movies);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading movies list");
                TempData["ErrorMessage"] = "Có lỗi xảy ra khi tải danh sách phim";
                return RedirectToAction("Dashboard");
            }
        }

        // GET: /Admin/GetMovieData/{id}
        [HttpGet]
        public async Task<IActionResult> GetMovieData(int id)
        {
            if (!await IsAdminAsync())
            {
                return Json(new { success = false, message = "Không có quyền truy cập" });
            }

            try
            {
                var movie = await _context.Movies
                    .Include(m => m.Categories)
                    .Include(m => m.Countries)
                    .Include(m => m.Actors)
                    .Include(m => m.Directors)
                    .FirstOrDefaultAsync(m => m.MovieId == id);

                if (movie == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy phim" });
                }

                return Json(new
                {
                    success = true,
                    data = new
                    {
                        movieId = movie.MovieId,
                        slug = movie.Slug,
                        name = movie.Name,
                        originalName = movie.OriginalName,
                        content = movie.Content,
                        type = movie.Type,
                        status = movie.Status,
                        posterUrl = movie.PosterUrl,
                        thumbUrl = movie.ThumbUrl,
                        trailerUrl = movie.TrailerUrl,
                        time = movie.Time,
                        episodeCurrent = movie.EpisodeCurrent,
                        episodeTotal = movie.EpisodeTotal,
                        quality = movie.Quality,
                        language = movie.Language,
                        year = movie.Year,
                        isRecommended = movie.IsRecommended,
                        isActive = movie.IsActive,
                        categoryIds = string.Join(",", movie.Categories.Select(c => c.CategoryId)),
                        countryIds = string.Join(",", movie.Countries.Select(c => c.CountryId)),
                        actorNames = string.Join(",", movie.Actors.Select(a => a.Name)),
                        directorNames = string.Join(",", movie.Directors.Select(d => d.Name))
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting movie data for ID: {MovieId}", id);
                return Json(new { success = false, message = "Có lỗi xảy ra khi tải dữ liệu" });
            }
        }

        // POST: /Admin/CreateMovie
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateMovie(CreateMovieDto model)
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

                // Kiểm tra slug đã tồn tại
                if (await _context.Movies.AnyAsync(m => m.Slug == model.Slug))
                {
                    return Json(new { success = false, message = "Slug đã được sử dụng" });
                }

                var movie = new MovieWeb.Models.Entities.Movie
                {
                    ApiId = null, // Phim thủ công không có ApiId
                    Slug = model.Slug,
                    Name = model.Name,
                    OriginalName = model.OriginalName,
                    Content = model.Content,
                    Type = model.Type,
                    Status = model.Status,

                    // Chỉ lưu tên file, bỏ full url
                    PosterUrl = string.IsNullOrWhiteSpace(model.PosterUrl)
                                ? null
                                : Path.GetFileName(model.PosterUrl),
                    ThumbUrl = string.IsNullOrWhiteSpace(model.ThumbUrl)
                                ? null
                                : Path.GetFileName(model.ThumbUrl),

                    TrailerUrl = model.TrailerUrl,
                    Time = model.Time,
                    EpisodeCurrent = model.EpisodeCurrent,
                    EpisodeTotal = model.EpisodeTotal,
                    Quality = model.Quality,
                    Language = model.Language,
                    Year = model.Year,
                    ViewCount = 0,
                    Rating = 0,
                    RatingCount = 0,
                    IsRecommended = model.IsRecommended,
                    IsBanner = model.IsBanner ?? false,
                    IsActive = model.IsActive,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };

                _context.Movies.Add(movie);
                await _context.SaveChangesAsync();

                // Thêm categories
                if (!string.IsNullOrWhiteSpace(model.CategoryIds))
                {
                    var categoryIds = model.CategoryIds.Split(',')
                        .Select(id => int.TryParse(id.Trim(), out var result) ? result : 0)
                        .Where(id => id > 0)
                        .ToList();

                    var categories = await _context.Categories
                        .Where(c => categoryIds.Contains(c.CategoryId))
                        .ToListAsync();

                    foreach (var category in categories)
                    {
                        movie.Categories.Add(category);
                    }
                }

                // Thêm countries
                if (!string.IsNullOrWhiteSpace(model.CountryIds))
                {
                    var countryIds = model.CountryIds.Split(',')
                        .Select(id => int.TryParse(id.Trim(), out var result) ? result : 0)
                        .Where(id => id > 0)
                        .ToList();

                    var countries = await _context.Countries
                        .Where(c => countryIds.Contains(c.CountryId))
                        .ToListAsync();

                    foreach (var country in countries)
                    {
                        movie.Countries.Add(country);
                    }
                }

                // Thêm actors
                if (!string.IsNullOrWhiteSpace(model.ActorNames))
                {
                    var actorNames = model.ActorNames.Split(',')
                        .Select(n => n.Trim())
                        .Where(n => !string.IsNullOrWhiteSpace(n))
                        .ToList();

                    foreach (var actorName in actorNames)
                    {
                        var slug = GenerateSlug(actorName);
                        var actor = await _context.Actors.FirstOrDefaultAsync(a => a.Slug == slug);

                        if (actor == null)
                        {
                            actor = new MovieWeb.Models.Entities.Actor
                            {
                                Name = actorName,
                                Slug = slug
                            };
                            _context.Actors.Add(actor);
                        }

                        movie.Actors.Add(actor);
                    }
                }

                // Thêm directors
                if (!string.IsNullOrWhiteSpace(model.DirectorNames))
                {
                    var directorNames = model.DirectorNames.Split(',')
                        .Select(n => n.Trim())
                        .Where(n => !string.IsNullOrWhiteSpace(n))
                        .ToList();

                    foreach (var directorName in directorNames)
                    {
                        var slug = GenerateSlug(directorName);
                        var director = await _context.Directors.FirstOrDefaultAsync(d => d.Slug == slug);

                        if (director == null)
                        {
                            director = new MovieWeb.Models.Entities.Director
                            {
                                Name = directorName,
                                Slug = slug
                            };
                            _context.Directors.Add(director);
                        }

                        movie.Directors.Add(director);
                    }
                }

                await _context.SaveChangesAsync();

                await LogAdminActionAsync("CREATE_MOVIE", $"Created manual movie: {movie.Name}", movie.MovieId.ToString());

                _logger.LogInformation("Admin created new movie: {MovieName}", movie.Name);

                return Json(new { success = true, message = "Tạo phim thành công!" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating movie");
                return Json(new { success = false, message = ex.InnerException?.Message ?? ex.Message });
            }
        }

        // POST: /Admin/UpdateMovie
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateMovie(UpdateMovieDto model)
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

                var movie = await _context.Movies
                    .Include(m => m.Categories)
                    .Include(m => m.Countries)
                    .Include(m => m.Actors)
                    .Include(m => m.Directors)
                    .FirstOrDefaultAsync(m => m.MovieId == model.MovieId);

                if (movie == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy phim" });
                }

                // Kiểm tra slug trùng (ngoại trừ phim hiện tại)
                if (await _context.Movies.AnyAsync(m => m.Slug == model.Slug && m.MovieId != model.MovieId))
                {
                    return Json(new { success = false, message = "Slug đã được sử dụng bởi phim khác" });
                }

                // Cập nhật thông tin cơ bản
                movie.Slug = model.Slug;
                movie.Name = model.Name;
                movie.OriginalName = model.OriginalName;
                movie.Content = model.Content;
                movie.Type = model.Type;
                movie.Status = model.Status;
                movie.PosterUrl = model.PosterUrl;
                movie.ThumbUrl = model.ThumbUrl;
                movie.TrailerUrl = model.TrailerUrl;
                movie.Time = model.Time;
                movie.EpisodeCurrent = model.EpisodeCurrent;
                movie.EpisodeTotal = model.EpisodeTotal;
                movie.Quality = model.Quality;
                movie.Language = model.Language;
                movie.Year = model.Year;
                movie.IsRecommended = model.IsRecommended;
                movie.IsActive = model.IsActive;
                movie.UpdatedAt = DateTime.Now;

                // Cập nhật categories
                movie.Categories.Clear();
                if (!string.IsNullOrWhiteSpace(model.CategoryIds))
                {
                    var categoryIds = model.CategoryIds.Split(',')
                        .Select(id => int.TryParse(id.Trim(), out var result) ? result : 0)
                        .Where(id => id > 0)
                        .ToList();

                    var categories = await _context.Categories
                        .Where(c => categoryIds.Contains(c.CategoryId))
                        .ToListAsync();

                    foreach (var category in categories)
                    {
                        movie.Categories.Add(category);
                    }
                }

                // Cập nhật countries
                movie.Countries.Clear();
                if (!string.IsNullOrWhiteSpace(model.CountryIds))
                {
                    var countryIds = model.CountryIds.Split(',')
                        .Select(id => int.TryParse(id.Trim(), out var result) ? result : 0)
                        .Where(id => id > 0)
                        .ToList();

                    var countries = await _context.Countries
                        .Where(c => countryIds.Contains(c.CountryId))
                        .ToListAsync();

                    foreach (var country in countries)
                    {
                        movie.Countries.Add(country);
                    }
                }

                // Cập nhật actors
                movie.Actors.Clear();
                if (!string.IsNullOrWhiteSpace(model.ActorNames))
                {
                    var actorNames = model.ActorNames.Split(',')
                        .Select(n => n.Trim())
                        .Where(n => !string.IsNullOrWhiteSpace(n))
                        .ToList();

                    foreach (var actorName in actorNames)
                    {
                        var slug = GenerateSlug(actorName);
                        var actor = await _context.Actors.FirstOrDefaultAsync(a => a.Slug == slug);

                        if (actor == null)
                        {
                            actor = new MovieWeb.Models.Entities.Actor
                            {
                                Name = actorName,
                                Slug = slug
                            };
                            _context.Actors.Add(actor);
                        }

                        movie.Actors.Add(actor);
                    }
                }

                // Cập nhật directors
                movie.Directors.Clear();
                if (!string.IsNullOrWhiteSpace(model.DirectorNames))
                {
                    var directorNames = model.DirectorNames.Split(',')
                        .Select(n => n.Trim())
                        .Where(n => !string.IsNullOrWhiteSpace(n))
                        .ToList();

                    foreach (var directorName in directorNames)
                    {
                        var slug = GenerateSlug(directorName);
                        var director = await _context.Directors.FirstOrDefaultAsync(d => d.Slug == slug);

                        if (director == null)
                        {
                            director = new MovieWeb.Models.Entities.Director
                            {
                                Name = directorName,
                                Slug = slug
                            };
                            _context.Directors.Add(director);
                        }

                        movie.Directors.Add(director);
                    }
                }

                await _context.SaveChangesAsync();

                await LogAdminActionAsync("UPDATE_MOVIE", $"Updated movie: {movie.Name}", movie.MovieId.ToString());

                _logger.LogInformation("Admin updated movie: {MovieName}", movie.Name);

                return Json(new { success = true, message = "Cập nhật phim thành công!" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating movie");
                return Json(new { success = false, message = "Có lỗi xảy ra khi cập nhật phim" });
            }
        }

        // POST: /Admin/ToggleMovieStatus
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleMovieStatus(int movieId, bool isActive)
        {
            if (!await IsAdminAsync())
            {
                return Json(new { success = false, message = "Không có quyền truy cập" });
            }

            try
            {
                var movie = await _context.Movies.FindAsync(movieId);
                if (movie == null)
                {
                    return Json(new { success = false, message = "Phim không tồn tại" });
                }

                movie.IsActive = isActive;
                movie.UpdatedAt = DateTime.Now;
                await _context.SaveChangesAsync();

                await LogAdminActionAsync("TOGGLE_MOVIE_STATUS",
                    $"Changed movie {movie.Name} status to {(isActive ? "Active" : "Inactive")}",
                    movieId.ToString());

                return Json(new { success = true, message = $"Đã {(isActive ? "kích hoạt" : "ẩn")} phim thành công" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling movie status for ID: {MovieId}", movieId);
                return Json(new { success = false, message = "Có lỗi xảy ra" });
            }
        }

        // POST: /Admin/ToggleMovieRecommended
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleMovieRecommended(int movieId, bool isRecommended)
        {
            if (!await IsAdminAsync())
            {
                return Json(new { success = false, message = "Không có quyền truy cập" });
            }

            try
            {
                var movie = await _context.Movies.FindAsync(movieId);
                if (movie == null)
                {
                    return Json(new { success = false, message = "Phim không tồn tại" });
                }

                movie.IsRecommended = isRecommended;
                movie.UpdatedAt = DateTime.Now;
                await _context.SaveChangesAsync();

                await LogAdminActionAsync("TOGGLE_MOVIE_RECOMMENDED",
                    $"Changed movie {movie.Name} recommended status to {isRecommended}",
                    movieId.ToString());

                return Json(new { success = true, message = $"Đã {(isRecommended ? "đánh dấu" : "bỏ đánh dấu")} phim đề xuất" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling movie recommended for ID: {MovieId}", movieId);
                return Json(new { success = false, message = "Có lỗi xảy ra" });
            }
        }

        // POST: /Admin/DeleteMovie
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteMovie(int movieId)
        {
            if (!await IsAdminAsync())
            {
                return Json(new { success = false, message = "Không có quyền truy cập" });
            }

            try
            {
                var movie = await _context.Movies
                    .Include(m => m.Categories)
                    .Include(m => m.Countries)
                    .Include(m => m.Actors)
                    .Include(m => m.Directors)
                    .FirstOrDefaultAsync(m => m.MovieId == movieId);

                if (movie == null)
                {
                    return Json(new { success = false, message = "Phim không tồn tại" });
                }

                // Kiểm tra dữ liệu liên quan
                var hasRelatedData = await _context.Comments.AnyAsync(c => c.MovieId == movieId) ||
                                   await _context.Favorites.AnyAsync(f => f.MovieId == movieId) ||
                                   await _context.Ratings.AnyAsync(r => r.MovieId == movieId) ||
                                   await _context.WatchHistories.AnyAsync(w => w.MovieId == movieId);

                if (hasRelatedData)
                {
                    return Json(new { success = false, message = "Không thể xóa phim có dữ liệu liên quan. Hãy ẩn phim thay thế." });
                }

                // Xóa relationships
                movie.Categories.Clear();
                movie.Countries.Clear();
                movie.Actors.Clear();
                movie.Directors.Clear();

                _context.Movies.Remove(movie);
                await _context.SaveChangesAsync();

                await LogAdminActionAsync("DELETE_MOVIE", $"Deleted movie: {movie.Name} (ID: {movieId})", movieId.ToString());

                _logger.LogInformation("Admin deleted movie: {MovieName}", movie.Name);

                return Json(new { success = true, message = "Đã xóa phim thành công" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting movie ID: {MovieId}", movieId);
                return Json(new { success = false, message = "Có lỗi xảy ra khi xóa phim" });
            }
        }

        // Helper method: Generate slug from Vietnamese text
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
        // POST: /Admin/ToggleMovieBanner
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleMovieBanner(int movieId, bool isBanner)
        {
            if (!await IsAdminAsync())
            {
                return Json(new { success = false, message = "Không có quyền truy cập" });
            }

            try
            {
                var movie = await _context.Movies.FindAsync(movieId);
                if (movie == null)
                {
                    return Json(new { success = false, message = "Phim không tồn tại" });
                }

                // Giới hạn tối đa 5 phim làm banner
                if (isBanner)
                {
                    var currentBannerCount = await _context.Movies
                        .CountAsync(m => m.IsBanner == true && m.IsActive == true);

                    if (currentBannerCount >= 5)
                    {
                        return Json(new
                        {
                            success = false,
                            message = "Đã đạt giới hạn 5 phim banner. Hãy bỏ banner của phim khác trước."
                        });
                    }
                }

                movie.IsBanner = isBanner;
                movie.UpdatedAt = DateTime.Now;
                await _context.SaveChangesAsync();
                // ❗ Xóa cache để TrangChu load lại danh sách mới
                _cache.Remove("banner_movies_with_content");
                await LogAdminActionAsync("TOGGLE_MOVIE_BANNER",
                    $"Changed movie {movie.Name} banner status to {isBanner}",
                    movieId.ToString());

                return Json(new
                {
                    success = true,
                    message = $"Đã {(isBanner ? "thêm vào" : "bỏ khỏi")} banner thành công"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling movie banner for ID: {MovieId}", movieId);
                return Json(new { success = false, message = "Có lỗi xảy ra" });
            }
        }
    }
}
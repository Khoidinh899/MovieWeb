using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MovieWeb.Models.Entities;
using MovieWeb.Models.DTOs;
using MovieWeb.Extensions;
using Microsoft.AspNetCore.SignalR;
using MovieWeb.Hubs;
using System.Net;

namespace MovieWeb.Controllers
{
    public partial class AdminController : Controller
    {
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

                return View("UserDetail", userDetail);
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

                // Khi bạn gọi UpdateAsync, Security Stamp CŨNG tự động thay đổi
                var result = await _userManager.UpdateAsync(user);

                if (result.Succeeded)
                {
                    // Log admin action
                    await LogAdminActionAsync("UPDATE_USER_STATUS", $"Changed user {user.UserName} status to {(isActive ? "Active" : "Inactive")}", userId.ToString());

                    _logger.LogInformation("Admin {AdminId} updated user {UserId} status to {Status}",
                        currentUser?.Id, userId, isActive ? "Active" : "Inactive");

                    // ===== ✅ BẮT ĐẦU SỬA (LOGIC SIGNALR) =====
                    if (!isActive) // Nếu hành động là "KHÓA"
                    {
                        try
                        {
                            _logger.LogInformation("Đang gửi lệnh ForceLogout qua SignalR cho User {UserId}", userId);

                            // Gửi tín hiệu "ForceLogout" CHỈ cho user bị khóa
                            await _notificationHubContext.Clients
                                .User(userId.ToString())
                                .SendAsync("ForceLogout", "Tài khoản của bạn đã bị Quản trị viên khóa.");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Lỗi khi gửi SignalR ForceLogout cho User {UserId}", userId);
                        }
                    }
                    // ===== ✅ KẾT THÚC SỬA =====

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

                // Thực hiện Soft-Delete (vô hiệu hóa tài khoản) để lưu trữ lịch sử và chặn email đăng nhập/đăng ký lại
                user.IsActive = false;
                user.UpdatedAt = DateTime.Now;

                // Cập nhật Security Stamp để kích hoạt force logout ngay lập tức cho các phiên đăng nhập hiện tại
                await _userManager.UpdateSecurityStampAsync(user);

                var result = await _userManager.UpdateAsync(user);
                if (result.Succeeded)
                {
                    // Log admin action
                    await LogAdminActionAsync("DELETE_USER", $"Soft deleted (disabled) user {user.UserName} (ID: {userId})", userId.ToString());

                    _logger.LogInformation("Admin {AdminId} soft-deleted user {UserId}", currentUser?.Id, userId);

                    return Json(new { success = true, message = "Đã xóa và hạn chế tài khoản người dùng thành công" });
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

                var currentUser = await _authService.GetCurrentUserAsync();

                // ==== 💡 FIX 1: CHECK TỰ KHÓA ====
                if (currentUser?.Id == user.Id && !model.IsActive)
                {
                    // Nếu admin đang tự sửa mình VÀ họ đang cố gắng bỏ tick "IsActive"
                    return Json(new { success = false, message = "Không thể tự khóa tài khoản của chính mình." });
                }

                // ==== 💡 FIX 2: CHECK TỰ ĐỔI QUYỀN ====
                if (currentUser?.Id == user.Id && user.RoleId != model.RoleId)
                {
                    // Nếu admin đang tự sửa mình VÀ họ đang cố đổi RoleId
                    return Json(new { success = false, message = "Không thể tự thay đổi quyền của chính mình." });
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
                user.RoleId = model.RoleId; // Dòng này giờ đã an toàn
                user.IsActive = model.IsActive; // Dòng này giờ đã an toàn
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
        // POST: /Admin/SendVerificationEmail
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendVerificationEmail(int userId)
        {
            if (!await IsAdminAsync())
            {
                return Json(new { success = false, message = "Không có quyền truy cập" });
            }

            try
            {
                var user = await _userManager.FindByIdAsync(userId.ToString());
                if (user == null)
                {
                    return Json(new { success = false, message = "Người dùng không tồn tại" });
                }

                if (user.EmailConfirmed)
                {
                    return Json(new { success = false, message = "Tài khoản này đã được xác thực rồi" });
                }

                // Tạo token
                var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                var encodedToken = WebUtility.UrlEncode(token); // Mã hóa cho URL

                // Tạo link (callbackUrl)
                var callbackUrl = Url.Action(
                    "ConfirmEmail",  // Tên Action trong AuthController
                    "Auth",          // Tên Controller (ví dụ: AuthController)
                    new { userId = user.Id, token = encodedToken },
                    protocol: Request.Scheme
                );

                if (string.IsNullOrEmpty(callbackUrl))
                {
                    return Json(new { success = false, message = "Lỗi hệ thống: Không thể tạo link xác thực" });
                }

                // ==== 💡 THAY THẾ NỘI DUNG EMAIL ====
                // (Sử dụng template đẹp của ông)

                var userName = user.FirstName ?? user.UserName; // Ưu tiên FirstName

                var htmlBody = $@"
        <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px;'>
            <div style='text-align: center; margin-bottom: 30px;'>
                <h1 style='color: #333; margin-bottom: 10px;'>MoonPhim</h1>
                <p style='color: #666; font-size: 16px;'>Bay bổng cùng điện ảnh</p>
            </div>
            
            <div style='background: #f8f9fa; padding: 30px; border-radius: 10px; border-left: 4px solid #007bff;'>
                <h2 style='color: #333; margin-bottom: 20px;'>Xác thực tài khoản của bạn</h2>
                <p style='color: #555; font-size: 16px; line-height: 1.6; margin-bottom: 20px;'>
                    Chào {userName},
                </p>
                <p style='color: #555; font-size: 16px; line-height: 1.6; margin-bottom: 20px;'>
                    Quản trị viên MoonPhim đã gửi lại email xác thực cho tài khoản của bạn. 
                    Vui lòng nhấn vào nút bên dưới để hoàn tất.
                </p>
                
                <div style='text-align: center; margin: 30px 0;'>
                    <a href='{callbackUrl}' 
                       style='background: linear-gradient(45deg, #007bff, #0056b3); 
                              color: white; 
                              padding: 15px 30px; 
                              text-decoration: none; 
                              border-radius: 25px; 
                              font-weight: bold; 
                              font-size: 16px;
                              display: inline-block;'>
                        Xác thực tài khoản
                    </a>
                </div>
                
                <p style='color: #777; font-size: 14px; margin-top: 20px;'>
                    Nếu bạn không thể nhấn vào nút trên, hãy copy và paste link sau vào trình duyệt:
                </p>
                <p style='background: #e9ecef; padding: 10px; border-radius: 5px; word-break: break-all; font-size: 12px;'>
                    {callbackUrl}
                </p>
            </div>
            
            <div style='text-align: center; margin-top: 30px; color: #999; font-size: 12px;'>
                <p>© {DateTime.Now.Year} MoonPhim. All rights reserved.</p>
                <p>Nếu bạn không yêu cầu điều này, vui lòng bỏ qua email này.</p>
            </div>
        </div>";
                // ==== KẾT THÚC THAY THẾ ====


                // Gửi email (Dùng service của ông)
                await _emailService.SendEmailAsync(user.Email, "Xác thực tài khoản MoonPhim của bạn", htmlBody);

                // Log lại
                await LogAdminActionAsync("SEND_VERIFICATION_EMAIL", $"Admin sent verification email to {user.Email}", user.Id.ToString());
                _logger.LogInformation("Admin sent verification email to {Email}", user.Email);

                return Json(new { success = true, message = "Đã gửi email xác thực thành công!" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending verification email for {UserId}", userId);
                return Json(new { success = false, message = "Lỗi hệ thống khi gửi email" });
            }
        }
    }
}
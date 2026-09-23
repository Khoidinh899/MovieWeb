using System;
using System.Linq;
using System.Threading.Tasks;
using Hangfire;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MovieWeb.Data;
using MovieWeb.Models.Entities;

namespace MovieWeb.Jobs
{
    public class CleanupUnconfirmedUsersJob
    {
        private readonly MovieWebDbContext _context;
        private readonly UserManager<User> _userManager;
        private readonly ILogger<CleanupUnconfirmedUsersJob> _logger;

        public CleanupUnconfirmedUsersJob(
            MovieWebDbContext context,
            UserManager<User> userManager,
            ILogger<CleanupUnconfirmedUsersJob> logger)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
        }

        [AutomaticRetry(Attempts = 2)]
        public async Task Execute()
        {
            try
            {
                _logger.LogInformation("╔══════════════════════════════════════════════════════╗");
                _logger.LogInformation("║   CLEANUP UNCONFIRMED USERS JOB - BẮT ĐẦU            ║");
                _logger.LogInformation("╚══════════════════════════════════════════════════════╝");

                // Ngưỡng thời gian: các tài khoản tạo hơn 24 giờ trước nhưng chưa xác thực email (hoặc IsActive = false)
                var cutoffTime = DateTime.UtcNow.AddHours(-24);

                _logger.LogInformation("🔍 Đang tìm các tài khoản chưa xác thực/chưa active tạo trước {CutoffTime:yyyy-MM-dd HH:mm:ss} UTC...", cutoffTime);

                // Lấy danh sách user thỏa mãn điều kiện lọc an toàn tuyệt đối
                var targetUsers = await _context.Users
                    .Where(u => (!u.EmailConfirmed || u.IsActive == false)
                                && u.RoleId != 1 // Không bao giờ xóa Admin
                                && u.SubscriptionType == "free" // Không xóa user có gói trả phí
                                && string.IsNullOrEmpty(u.StripeCustomerId) // Không xóa khách hàng Stripe
                                && !u.Transactions.Any() // Không xóa user có phát sinh giao dịch
                                && !u.UserSubscriptions.Any() // Không xóa user có gói đăng ký
                                && (u.CreatedAt == null || u.CreatedAt < cutoffTime))
                    .OrderBy(u => u.Id)
                    .Take(200) // Xử lý tối đa 200 user mỗi lần chạy để tránh lock DB
                    .ToListAsync();

                if (!targetUsers.Any())
                {
                    _logger.LogInformation("✅ Không có tài khoản rác/chưa xác thực nào cần dọn dẹp.");
                    return;
                }

                _logger.LogInformation("🧹 Phát hiện {Count} tài khoản rác/chưa active cần dọn dẹp. Bắt đầu xóa...", targetUsers.Count);

                int successCount = 0;
                int failureCount = 0;

                foreach (var user in targetUsers)
                {
                    try
                    {
                        var userId = user.Id;

                        // 1. Dọn dẹp dữ liệu liên kết nếu có
                        var notifications = await _context.Notifications.Where(n => n.UserId == userId).ToListAsync();
                        if (notifications.Any()) _context.Notifications.RemoveRange(notifications);

                        var watchHistories = await _context.WatchHistories.Where(w => w.UserId == userId).ToListAsync();
                        if (watchHistories.Any()) _context.WatchHistories.RemoveRange(watchHistories);

                        var favorites = await _context.Favorites.Where(f => f.UserId == userId).ToListAsync();
                        if (favorites.Any()) _context.Favorites.RemoveRange(favorites);

                        var ratings = await _context.Ratings.Where(r => r.UserId == userId).ToListAsync();
                        if (ratings.Any()) _context.Ratings.RemoveRange(ratings);

                        var comments = await _context.Comments.Where(c => c.UserId == userId).ToListAsync();
                        if (comments.Any()) _context.Comments.RemoveRange(comments);

                        var userRequests = await _context.UserRequestMovies.Where(ur => ur.UserId == userId).ToListAsync();
                        if (userRequests.Any()) _context.UserRequestMovies.RemoveRange(userRequests);

                        await _context.SaveChangesAsync();

                        // 2. Sử dụng UserManager để xóa user cùng Identity Roles/Claims/Tokens
                        var result = await _userManager.DeleteAsync(user);
                        if (result.Succeeded)
                        {
                            successCount++;
                            _logger.LogInformation("🗑️ Đã xóa tài khoản rác: ID={UserId}, Username={Username}, Email={Email}", user.Id, user.UserName, user.Email);
                        }
                        else
                        {
                            failureCount++;
                            var errors = string.Join("; ", result.Errors.Select(e => e.Description));
                            _logger.LogWarning("⚠️ Không thể xóa user {UserId}: {Errors}", user.Id, errors);
                        }
                    }
                    catch (Exception itemEx)
                    {
                        failureCount++;
                        _logger.LogError(itemEx, "❌ Lỗi khi xóa user ID={UserId}", user.Id);
                    }
                }

                _logger.LogInformation("🎉 [Cleanup Job Hoàn tất] Thành công: {SuccessCount}, Thất bại: {FailureCount}", successCount, failureCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Lỗi trong quá trình chạy CleanupUnconfirmedUsersJob");
                throw;
            }
        }
    }
}

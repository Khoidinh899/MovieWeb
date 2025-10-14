using Microsoft.EntityFrameworkCore;
using MovieWeb.Data;
using MovieWeb.Models.DTOs;
using MovieWeb.Models.Entities;
using MovieWeb.Services.Interfaces;

namespace MovieWeb.Services
{
    public class SubscriptionService : ISubscriptionService
    {
        private readonly MovieWebDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly string[] _studentEmailDomains;

        public SubscriptionService(MovieWebDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
            _studentEmailDomains = configuration.GetSection("SubscriptionSettings:StudentEmailDomains").Get<string[]>()
                                    ?? new[] { ".edu", ".edu.vn", ".ac.vn" };
        }

        // ===== SUBSCRIPTION PLANS =====
        public async Task<bool> FulfillOrderAsync(string sessionId)
        {
            // Tạm thời trả về true để hết lỗi
            // Logic xử lý đơn hàng thật sẽ được thêm vào đây ở bước tiếp theo
            await Task.CompletedTask; // Giả lập một công việc bất đồng bộ
            return true;
        }
        public async Task<List<SubscriptionPlanDto>> GetAllPlansAsync(bool activeOnly = true)
        {
            var query = _context.SubscriptionPlans.AsQueryable();

            if (activeOnly)
                query = query.Where(p => p.IsActive);

            var plans = await query
                .OrderBy(p => p.DisplayOrder)
                .ThenBy(p => p.PriceVND)
                .ToListAsync();

            return plans.Select(MapToDto).ToList();
        }

        public async Task<SubscriptionPlanDto?> GetPlanByIdAsync(int planId)
        {
            var plan = await _context.SubscriptionPlans.FindAsync(planId);
            return plan != null ? MapToDto(plan) : null;
        }

        public async Task<List<SubscriptionPlanDto>> GetPlansByTypeAsync(string planType)
        {
            var plans = await _context.SubscriptionPlans
                .Where(p => p.IsActive && p.PlanType == planType)
                .OrderBy(p => p.DisplayOrder)
                .ThenBy(p => p.PriceVND)
                .ToListAsync();

            return plans.Select(MapToDto).ToList();
        }

        // ===== USER SUBSCRIPTIONS =====
        // Method 1: Kiểm tra gói còn hạn (active hoặc cancelled)
        public async Task<UserSubscriptionDto?> GetActiveOrCancelledWithTimeAsync(int userId)
        {
            var subscription = await _context.UserSubscriptions
                .Include(s => s.SubscriptionPlan)
                .Where(s => s.UserId == userId
                    && s.EndDate > DateTime.Now
                    && (s.Status == "active" || s.Status == "cancelled"))
                .OrderByDescending(s => s.EndDate)
                .FirstOrDefaultAsync();

            return subscription != null ? MapToUserSubscriptionDto(subscription) : null;
        }

        // Method 2: Nâng cấp/Mua mới với cộng dồn ngày - ĐÃ CẬP NHẬT
        public async Task<bool> UpgradeSubscriptionAsync(int userId, int newPlanId, string? stripeSubscriptionId = null)
        {
            var newPlan = await _context.SubscriptionPlans.FindAsync(newPlanId);
            if (newPlan == null)
                throw new ArgumentException("Plan not found");

            // Tìm gói cũ đã bị cancel nhưng còn thời gian
            var oldSubscription = await _context.UserSubscriptions
                .Where(s => s.UserId == userId
                    && s.Status == "cancelled"
                    && s.EndDate > DateTime.Now)
                .OrderByDescending(s => s.EndDate)
                .FirstOrDefaultAsync();

            int bonusDays = 0;

            // Nếu có gói cũ còn thời gian, tính số ngày bonus
            if (oldSubscription != null)
            {
                bonusDays = (oldSubscription.EndDate - DateTime.Now).Days;

                // Đánh dấu gói cũ là expired
                oldSubscription.Status = "expired";
                oldSubscription.UpdatedAt = DateTime.Now;
                _context.UserSubscriptions.Update(oldSubscription);
            }

            // Tính tổng số ngày = ngày gói mới + ngày bonus từ gói cũ
            var totalDays = (newPlan.ActualMonths * 30) + bonusDays;

            // Tạo subscription mới với bonus days
            var newSubscription = new UserSubscription
            {
                UserId = userId,
                PlanId = newPlanId,
                StripeSubscriptionId = stripeSubscriptionId,
                Status = "active",
                StartDate = DateTime.Now,
                EndDate = DateTime.Now.AddDays(totalDays),
                NextBillingDate = DateTime.Now.AddMonths(newPlan.DurationMonths),
                AutoRenew = true,
                BonusDaysFromPreviousPackage = bonusDays, // 🆕 LƯU BONUS DAYS
                CreatedAt = DateTime.Now
            };

            _context.UserSubscriptions.Add(newSubscription);

            // Update thông tin user
            var user = await _context.Users.FindAsync(userId);
            if (user != null)
            {
                user.SubscriptionType = newPlan.PlanType.ToLower();
                user.SubscriptionStartDate = newSubscription.StartDate;
                user.SubscriptionEndDate = newSubscription.EndDate;
                user.UpdatedAt = DateTime.Now;
                _context.Users.Update(user);
            }

            await _context.SaveChangesAsync();
            return true;
        }

        // 🆕 THÊM METHOD MỚI - Lấy bonus days từ subscription hiện tại
        public async Task<int> GetBonusDaysFromCurrentSubscriptionAsync(int userId)
        {
            var subscription = await _context.UserSubscriptions
                .Where(s => s.UserId == userId
                    && s.Status == "active"
                    && s.EndDate > DateTime.Now)
                .OrderByDescending(s => s.EndDate)
                .FirstOrDefaultAsync();

            return subscription?.BonusDaysFromPreviousPackage ?? 0;
        }
        public async Task<UserSubscriptionDto?> GetActiveSubscriptionAsync(int userId)
        {
            var subscription = await _context.UserSubscriptions
                .Include(s => s.SubscriptionPlan)
                .Where(s => s.UserId == userId && s.Status == "active" && s.EndDate > DateTime.Now)
                .OrderByDescending(s => s.EndDate)
                .FirstOrDefaultAsync();

            return subscription != null ? MapToUserSubscriptionDto(subscription) : null;
        }

        public async Task<UserSubscription?> GetSubscriptionByIdAsync(int subscriptionId)
        {
            return await _context.UserSubscriptions
                .Include(s => s.SubscriptionPlan)
                .Include(s => s.User)
                .FirstOrDefaultAsync(s => s.SubscriptionId == subscriptionId);
        }

        public async Task<List<UserSubscriptionDto>> GetUserSubscriptionHistoryAsync(int userId)
        {
            var subscriptions = await _context.UserSubscriptions
                .Include(s => s.SubscriptionPlan)
                .Where(s => s.UserId == userId)
                .OrderByDescending(s => s.CreatedAt)
                .ToListAsync();

            return subscriptions.Select(MapToUserSubscriptionDto).ToList();
        }

        public async Task<bool> HasActiveSubscriptionAsync(int userId)
        {
            return await _context.UserSubscriptions
                .AnyAsync(s => s.UserId == userId && s.Status == "active" && s.EndDate > DateTime.Now);
        }

        public async Task<bool> IsPremiumUserAsync(int userId)
        {
            var user = await _context.Users.FindAsync(userId);
            return user?.IsPremium ?? false;
        }

        // ===== SUBSCRIPTION ACTIONS =====
        public async Task<UserSubscription> CreateSubscriptionAsync(
            int userId,
            int planId,
            string? stripeSubscriptionId = null)
        {
            var plan = await _context.SubscriptionPlans.FindAsync(planId);
            if (plan == null)
                throw new ArgumentException("Plan not found");

            // 🔥 FIX: Tìm gói cũ ĐANG CÒN HẠN (active HOẶC cancelled)
            var oldSubscription = await _context.UserSubscriptions
                .Where(s => s.UserId == userId
                    && s.EndDate > DateTime.Now
                    && (s.Status == "active" || s.Status == "cancelled")) // ✅ CHECK CẢ 2 TRẠNG THÁI
                .OrderByDescending(s => s.EndDate)
                .FirstOrDefaultAsync();

            int bonusDays = 0;

            if (oldSubscription != null)
            {
                // Tính số ngày còn lại từ gói cũ
                bonusDays = (int)(oldSubscription.EndDate - DateTime.Now).TotalDays;

                // Đánh dấu gói cũ là expired
                oldSubscription.Status = "expired";
                oldSubscription.UpdatedAt = DateTime.Now;
                _context.UserSubscriptions.Update(oldSubscription);

                Console.WriteLine($"🔍 Tìm thấy gói cũ:\n" +
                    $"- Status: {oldSubscription.Status}\n" +
                    $"- Ngày còn lại: {bonusDays} ngày\n" +
                    $"- EndDate: {oldSubscription.EndDate:dd/MM/yyyy}");
            }

            // ✅ TÍNH ĐÚNG: (ActualMonths × 30) + bonusDays
            int totalDays = (plan.ActualMonths * 30) + bonusDays;
            DateTime newEndDate = DateTime.Now.AddDays(totalDays);

            Console.WriteLine($"📊 Tính toán subscription mới:\n" +
                $"- Gói: {plan.DisplayName} ({plan.ActualMonths} tháng)\n" +
                $"- Ngày gói mới: {plan.ActualMonths * 30} ngày\n" +
                $"- Bonus từ gói cũ: {bonusDays} ngày\n" +
                $"- Tổng: {totalDays} ngày\n" +
                $"- EndDate: {newEndDate:dd/MM/yyyy}");

            // Tạo subscription mới
            var subscription = new UserSubscription
            {
                UserId = userId,
                PlanId = planId,
                StripeSubscriptionId = stripeSubscriptionId,
                Status = "active",
                StartDate = DateTime.Now,
                EndDate = newEndDate,
                NextBillingDate = null,
                AutoRenew = false,
                BonusDaysFromPreviousPackage = bonusDays, // ✅ LƯU BONUS DAYS
                CreatedAt = DateTime.Now
            };

            _context.UserSubscriptions.Add(subscription);

            // Update user
            var user = await _context.Users.FindAsync(userId);
            if (user != null)
            {
                user.SubscriptionType = plan.PlanType.ToLower();
                user.SubscriptionStartDate = subscription.StartDate;
                user.SubscriptionEndDate = subscription.EndDate;
                user.UpdatedAt = DateTime.Now;
                _context.Users.Update(user);
            }

            await _context.SaveChangesAsync();
            return subscription;
        }
        public async Task<bool> CancelSubscriptionAsync(int subscriptionId, string? reason = null, bool immediately = false)
        {
            var subscription = await _context.UserSubscriptions
                .Include(s => s.User)
                .FirstOrDefaultAsync(s => s.SubscriptionId == subscriptionId);

            if (subscription == null) return false;

            // 1. Đổi status subscription thành "cancelled"
            subscription.Status = "cancelled";
            subscription.CancelledAt = DateTime.Now;
            subscription.CancellationReason = reason;
            subscription.AutoRenew = false;
            subscription.UpdatedAt = DateTime.Now;
            if (subscription.User != null)
            {
                subscription.User.UpdatedAt = DateTime.Now;
            }
            await _context.SaveChangesAsync();

            Console.WriteLine($"✅ Đã hủy gói thành công:\n" +
                $"- SubscriptionId: {subscriptionId}\n" +
                $"- User vẫn dùng đến: {subscription.EndDate:dd/MM/yyyy}\n" +
                $"- Ngày còn lại: {(subscription.EndDate - DateTime.Now).Days} ngày");
            return true;
        }
        public async Task<bool> RenewSubscriptionAsync(int subscriptionId)
        {
            var subscription = await _context.UserSubscriptions
                .Include(s => s.SubscriptionPlan)
                .Include(s => s.User)
                .FirstOrDefaultAsync(s => s.SubscriptionId == subscriptionId);

            if (subscription == null || subscription.SubscriptionPlan == null) return false;

            subscription.Status = "active";
            subscription.EndDate = subscription.EndDate.AddMonths(subscription.SubscriptionPlan.ActualMonths);
            subscription.NextBillingDate = DateTime.Now.AddMonths(subscription.SubscriptionPlan.DurationMonths);
            subscription.UpdatedAt = DateTime.Now;

            if (subscription.User != null)
            {
                subscription.User.SubscriptionEndDate = subscription.EndDate;
                subscription.User.UpdatedAt = DateTime.Now;
            }

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> ExtendSubscriptionAsync(int subscriptionId, int months)
        {
            var subscription = await _context.UserSubscriptions
                .Include(s => s.User)
                .FirstOrDefaultAsync(s => s.SubscriptionId == subscriptionId);

            if (subscription == null) return false;

            subscription.EndDate = subscription.EndDate.AddMonths(months);
            subscription.UpdatedAt = DateTime.Now;

            if (subscription.User != null)
            {
                subscription.User.SubscriptionEndDate = subscription.EndDate;
                subscription.User.UpdatedAt = DateTime.Now;
            }

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task UpdateSubscriptionStatusAsync(int subscriptionId, string status)
        {
            var subscription = await _context.UserSubscriptions.FindAsync(subscriptionId);
            if (subscription != null)
            {
                subscription.Status = status;
                subscription.UpdatedAt = DateTime.Now;
                await _context.SaveChangesAsync();
            }
        }

        // FIX: Thay đổi return type từ Task sang Task<bool>
        public async Task<bool> UpdateAsync(UserSubscription subscription)
        {
            try
            {
                _context.UserSubscriptions.Update(subscription);
                await _context.SaveChangesAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> CancelAsync(string stripeSubscriptionId, bool immediately = false)
        {
            var subscription = await _context.UserSubscriptions
                .Include(s => s.User)
                .FirstOrDefaultAsync(s => s.StripeSubscriptionId == stripeSubscriptionId);

            if (subscription == null) return false;

            return await CancelSubscriptionAsync(subscription.SubscriptionId, "Cancelled via Stripe", immediately);
        }

        // ===== STUDENT VERIFICATION =====

        public async Task<bool> SendStudentVerificationEmailAsync(int userId, string studentEmail)
        {
            if (!await IsStudentEmailValidAsync(studentEmail))
                return false;

            var user = await _context.Users.FindAsync(userId);
            if (user == null) return false;

            var verificationCode = GenerateVerificationCode();
            user.StudentEmail = studentEmail;
            user.EmailConfirmToken = verificationCode;
            user.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();

            // TODO: Send email với verification code

            return true;
        }

        public async Task<bool> VerifyStudentEmailAsync(int userId, string verificationCode)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null || user.EmailConfirmToken != verificationCode)
                return false;

            user.IsStudentVerified = true;
            user.StudentVerifiedAt = DateTime.Now;
            user.EmailConfirmToken = null;
            user.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> IsStudentEmailValidAsync(string email)
        {
            return await Task.FromResult(
                _studentEmailDomains.Any(domain => email.EndsWith(domain, StringComparison.OrdinalIgnoreCase))
            );
        }

        // ===== TRANSACTIONS =====

        public async Task<Transaction> CreateTransactionAsync(int userId, int planId, string paymentMethod = "stripe")
        {
            var plan = await _context.SubscriptionPlans.FindAsync(planId);
            if (plan == null)
                throw new ArgumentException("Plan not found");

            var transaction = new Transaction
            {
                UserId = userId,
                PlanId = planId,
                TransactionCode = $"TXN_{DateTime.Now:yyyyMMddHHmmss}_{userId}_{Guid.NewGuid().ToString().Substring(0, 8)}",
                Amount = plan.PriceUSD,
                AmountVND = plan.PriceVND,
                Currency = "USD",
                PaymentMethod = paymentMethod,
                Status = "pending",
                Description = $"Thanh toán gói {plan.DisplayName}",
                CreatedAt = DateTime.Now
            };

            _context.Transactions.Add(transaction);
            await _context.SaveChangesAsync();

            return transaction;
        }

        public async Task<bool> CompleteTransactionAsync(int transactionId, string? stripePaymentIntentId = null, string? stripeChargeId = null)
        {
            var transaction = await _context.Transactions.FindAsync(transactionId);
            if (transaction == null) return false;

            transaction.Status = "completed";
            transaction.CompletedAt = DateTime.Now;
            transaction.UpdatedAt = DateTime.Now;
            transaction.StripePaymentIntentId = stripePaymentIntentId;
            transaction.StripeChargeId = stripeChargeId;

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> FailTransactionAsync(int transactionId, string? reason = null)
        {
            var transaction = await _context.Transactions.FindAsync(transactionId);
            if (transaction == null) return false;

            transaction.Status = "failed";
            transaction.FailureReason = reason;
            transaction.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<List<TransactionDto>> GetUserTransactionsAsync(int userId, int pageNumber = 1, int pageSize = 10)
        {
            var transactions = await _context.Transactions
                .Include(t => t.SubscriptionPlan)
                .Where(t => t.UserId == userId)
                .OrderByDescending(t => t.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return transactions.Select(MapToTransactionDto).ToList();
        }

        // ===== ADMIN FUNCTIONS =====

        public async Task<RevenueStatsDto> GetRevenueStatsAsync(DateTime? startDate = null, DateTime? endDate = null)
        {
            // 1. Xác định khoảng thời gian: Mặc định là tháng hiện tại
            var now = DateTime.Now;
            var start = startDate ?? new DateTime(now.Year, now.Month, 1);
            var end = endDate ?? start.AddMonths(1); // Lấy đến đầu tháng sau để so sánh dễ hơn

            // 2. Tạo câu truy vấn cơ sở (chưa thực thi) để tối ưu hiệu năng
            //    Tất cả tính toán sẽ được đẩy về phía database
            var transactionsInPeriod = _context.Transactions
                                               .Where(t => t.CreatedAt >= start && t.CreatedAt < end);

            // 3. Thực thi các câu truy vấn để tính toán từng chỉ số
            var totalRevenue = await transactionsInPeriod
                                        .Where(t => t.Status == "completed") // Chỉ tính doanh thu cho giao dịch THÀNH CÔNG
                                        .SumAsync(t => t.AmountVND);

            var totalTransactions = await transactionsInPeriod.CountAsync();
            var completedTransactions = await transactionsInPeriod.CountAsync(t => t.Status == "completed");
            var pendingTransactions = await transactionsInPeriod.CountAsync(t => t.Status == "pending");
            var failedTransactions = await transactionsInPeriod.CountAsync(t => t.Status == "failed");
            var refundedTransactions = await transactionsInPeriod.CountAsync(t => t.Status == "refunded");

            // 4. Tạo và trả về đối tượng DTO với dữ liệu thật đã tính toán
            var stats = new RevenueStatsDto
            {
                TotalRevenue = totalRevenue,
                TotalTransactions = totalTransactions,
                CompletedTransactions = completedTransactions,
                PendingTransactions = pendingTransactions,
                FailedTransactions = failedTransactions,
                RefundedTransactions = refundedTransactions
            };

            return stats;
        }

        public async Task<List<PlanRevenueDto>> GetRevenueByPlanAsync(DateTime? startDate = null, DateTime? endDate = null)
        {
            startDate ??= DateTime.Now.AddMonths(-1);
            endDate ??= DateTime.Now;

            var result = await _context.Transactions
                .Include(t => t.SubscriptionPlan)
                .Where(t => t.CreatedAt >= startDate && t.CreatedAt <= endDate && t.PlanId != null)
                .GroupBy(t => new { t.SubscriptionPlan!.PlanType, t.SubscriptionPlan.DisplayName })
                .Select(g => new PlanRevenueDto
                {
                    PlanType = g.Key.PlanType,
                    DisplayName = g.Key.DisplayName,
                    TransactionCount = g.Count(),
                    TotalRevenue = g.Where(t => t.Status == "completed").Sum(t => t.AmountVND),
                    AvgRevenue = g.Where(t => t.Status == "completed").Any()
                        ? g.Where(t => t.Status == "completed").Average(t => t.AmountVND)
                        : 0
                })
                .OrderByDescending(p => p.TotalRevenue)
                .ToListAsync();

            return result;
        }

        public async Task<List<RevenueTrendDto>> GetRevenueTrendAsync(int days = 30)
        {
            var startDate = DateTime.Now.AddDays(-days);

            var transactions = await _context.Transactions
                .Where(t => t.CreatedAt >= startDate && t.Status == "completed")
                .ToListAsync();

            var trend = transactions
                .GroupBy(t => t.CreatedAt.Date)
                .Select(g => new RevenueTrendDto
                {
                    Date = g.Key.ToString("yyyy-MM-dd"),
                    Revenue = g.Sum(t => t.AmountVND),
                    Transactions = g.Count()
                })
                .OrderBy(t => t.Date)
                .ToList();

            return trend;
        }

        public async Task<List<UserSubscriptionDto>> GetExpiringSubscriptionsAsync(int daysThreshold = 7)
        {
            var expiryDate = DateTime.Now.AddDays(daysThreshold);

            var subscriptions = await _context.UserSubscriptions
                .Include(s => s.SubscriptionPlan)
                .Where(s => s.Status == "active"
                    && s.EndDate <= expiryDate
                    && s.EndDate > DateTime.Now)
                .OrderBy(s => s.EndDate)
                .ToListAsync();

            return subscriptions.Select(MapToUserSubscriptionDto).ToList();
        }

        public async Task<PaginatedResponse<UserSubscriptionDto>> GetAllSubscriptionsAsync(int page = 1, int pageSize = 20, string? status = null)
        {
            var query = _context.UserSubscriptions
                .Include(s => s.SubscriptionPlan)
                .Include(s => s.User)
                .AsQueryable();

            if (!string.IsNullOrEmpty(status))
                query = query.Where(s => s.Status == status);

            var totalItems = await query.CountAsync();

            var items = await query
                .OrderByDescending(s => s.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new PaginatedResponse<UserSubscriptionDto>
            {
                Items = items.Select(MapToUserSubscriptionDto).ToList(),
                TotalItems = totalItems,
                PageNumber = page,
                PageSize = pageSize
            };
        }

        public async Task<PaginatedResponse<TransactionDto>> GetAllTransactionsAsync(int page = 1, int pageSize = 20, string? status = null)
        {
            var query = _context.Transactions
                .Include(t => t.SubscriptionPlan)
                .Include(t => t.User)
                .AsQueryable();

            if (!string.IsNullOrEmpty(status))
                query = query.Where(t => t.Status == status);

            var totalItems = await query.CountAsync();

            var items = await query
                .OrderByDescending(t => t.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new PaginatedResponse<TransactionDto>
            {
                Items = items.Select(MapToTransactionDto).ToList(),
                TotalItems = totalItems,
                PageNumber = page,
                PageSize = pageSize
            };
        }

        public async Task<int> GetTotalActiveSubscriptionsAsync()
        {
            return await _context.UserSubscriptions
                .CountAsync(s => s.Status == "active" && s.EndDate > DateTime.Now);
        }

        public async Task<int> GetTotalPremiumUsersAsync()
        {
            return await _context.Users
                .CountAsync(u => (u.SubscriptionType == "premium" || u.SubscriptionType == "student")
                    && u.SubscriptionEndDate.HasValue
                    && u.SubscriptionEndDate.Value > DateTime.Now);
        }

        // ===== NOTIFICATIONS =====

        public async Task SendExpiryReminderAsync(int subscriptionId)
        {
            var subscription = await _context.UserSubscriptions
                .Include(s => s.User)
                .Include(s => s.SubscriptionPlan)
                .FirstOrDefaultAsync(s => s.SubscriptionId == subscriptionId);

            if (subscription?.User == null || subscription.SubscriptionPlan == null) return;

            var notification = new Notification
            {
                UserId = subscription.UserId,
                Title = "Gói đăng ký sắp hết hạn",
                Content = $"Gói {subscription.SubscriptionPlan.DisplayName} của bạn sẽ hết hạn vào {subscription.EndDate:dd/MM/yyyy}. Gia hạn ngay để tiếp tục trải nghiệm!",
                Type = "warning",
                Url = "/subscription",
                IsRead = false,
                CreatedAt = DateTime.Now
            };

            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();
        }

        public async Task SendPaymentFailedNotificationAsync(int userId, int transactionId)
        {
            var transaction = await _context.Transactions
                .Include(t => t.SubscriptionPlan)
                .FirstOrDefaultAsync(t => t.TransactionId == transactionId);

            if (transaction?.SubscriptionPlan == null) return;

            var notification = new Notification
            {
                UserId = userId,
                Title = "Thanh toán thất bại",
                Content = $"Thanh toán cho gói {transaction.SubscriptionPlan.DisplayName} không thành công. Vui lòng thử lại hoặc kiểm tra thông tin thanh toán.",
                Type = "error",
                Url = "/subscription",
                IsRead = false,
                CreatedAt = DateTime.Now
            };

            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();
        }

        public async Task SendSubscriptionActivatedNotificationAsync(int userId, int subscriptionId)
        {
            var subscription = await _context.UserSubscriptions
                .Include(s => s.SubscriptionPlan)
                .FirstOrDefaultAsync(s => s.SubscriptionId == subscriptionId);

            if (subscription?.SubscriptionPlan == null) return;

            var notification = new Notification
            {
                UserId = userId,
                Title = "Kích hoạt gói thành công! 🎉",
                Content = $"Chúc mừng! Gói {subscription.SubscriptionPlan.DisplayName} đã được kích hoạt. Bạn có thể xem phim không giới hạn đến {subscription.EndDate:dd/MM/yyyy}.",
                Type = "success",
                Url = "/",
                IsRead = false,
                CreatedAt = DateTime.Now
            };

            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();
        }

        // ===== HELPER METHODS =====

        public async Task<User?> GetUserByIdAsync(int userId)
        {
            return await _context.Users.FindAsync(userId);
        }

        public async Task<SubscriptionPlan?> GetPlanEntityByIdAsync(int planId)
        {
            return await _context.SubscriptionPlans.FindAsync(planId);
        }

        private SubscriptionPlanDto MapToDto(SubscriptionPlan plan)
        {
            var features = new List<string>
            {
                "Tắt quảng cáo hoàn toàn",
                "Xem phim chất lượng 4K",
                "Không giới hạn độ phân giải",
                "Hỗ trợ đa thiết bị"
            };

            if (plan.BonusMonths > 0)
                features.Add($"🎁 Tặng {plan.BonusMonths} tháng");

            var basePrice = plan.PlanType == "Student" ? 39000 : 59000;
            var savings = plan.DurationMonths > 1
                ? ((basePrice * plan.DurationMonths) - plan.PriceVND)
                : 0;

            return new SubscriptionPlanDto
            {
                PlanId = plan.PlanId,
                PlanName = plan.PlanName,
                DisplayName = plan.DisplayName,
                Description = plan.Description,
                PriceVND = plan.PriceVND,
                PriceUSD = plan.PriceUSD,
                DurationMonths = plan.DurationMonths,
                ActualMonths = plan.ActualMonths,
                BonusMonths = plan.BonusMonths,
                PlanType = plan.PlanType,
                IsPopular = plan.IsPopular,
                PriceDisplay = $"{plan.PriceVND:N0} ₫",
                DurationDisplay = plan.ActualMonths > plan.DurationMonths
                    ? $"{plan.DurationMonths} tháng (Tặng {plan.BonusMonths} tháng)"
                    : $"{plan.DurationMonths} tháng",
                MonthlySavings = savings > 0 ? savings : null,
                SavingsDisplay = savings > 0 ? $"Tiết kiệm {savings:N0} ₫" : null,
                Features = features
            };
        }

        private UserSubscriptionDto MapToUserSubscriptionDto(UserSubscription subscription)
        {
            return new UserSubscriptionDto
            {
                SubscriptionId = subscription.SubscriptionId,
                UserId = subscription.UserId,
                Plan = subscription.SubscriptionPlan != null ? MapToDto(subscription.SubscriptionPlan) : null,
                Status = subscription.Status,
                StatusDisplay = subscription.StatusDisplay,
                StartDate = subscription.StartDate,
                EndDate = subscription.EndDate,
                NextBillingDate = subscription.NextBillingDate,
                AutoRenew = subscription.AutoRenew,
                IsActive = subscription.IsActive,
                DaysRemaining = subscription.DaysRemaining,
                IsExpiringSoon = subscription.IsExpiringSoon
            };
        }

        private TransactionDto MapToTransactionDto(Transaction transaction)
        {
            return new TransactionDto
            {
                TransactionId = transaction.TransactionId,
                TransactionCode = transaction.TransactionCode,
                AmountVND = transaction.AmountVND,
                AmountDisplay = transaction.AmountDisplay,
                Currency = transaction.Currency,
                PaymentMethod = transaction.PaymentMethod,
                Status = transaction.Status,
                StatusDisplay = transaction.StatusDisplay,
                Description = transaction.Description,
                CreatedAt = transaction.CreatedAt,
                CompletedAt = transaction.CompletedAt,
                Plan = transaction.SubscriptionPlan != null ? MapToDto(transaction.SubscriptionPlan) : null
            };
        }

        private string GenerateVerificationCode()
        {
            return Guid.NewGuid().ToString("N").Substring(0, 6).ToUpper();
        }
    }
}
// Jobs/PaymentReminderJob.cs
using Hangfire;
using Microsoft.EntityFrameworkCore;
using MovieWeb.Data;
using MovieWeb.Services.Interfaces; // ✅ Import service

namespace MovieWeb.Jobs
{
    public class PaymentReminderJob
    {
        private readonly MovieWebDbContext _context;
        private readonly ILogger<PaymentReminderJob> _logger;
        private readonly IBackgroundJobClient _backgroundJobClient; // ✅ Dùng để xếp hàng job

        public PaymentReminderJob(
            MovieWebDbContext context,
            ILogger<PaymentReminderJob> logger,
            IBackgroundJobClient backgroundJobClient) // ✅ Inject
        {
            _context = context;
            _logger = logger;
            _backgroundJobClient = backgroundJobClient;
        }

        public async Task Execute()
        {
            try
            {
                _logger.LogInformation("╔════════════════════════════════════════╗");
                _logger.LogInformation("║  PAYMENT REMINDER FINDER JOB - BẮT ĐẦU ║");
                _logger.LogInformation("╚════════════════════════════════════════╝");
                
                var today = DateTime.UtcNow.Date; // Dùng UTC
                var threeDaysFromNow = today.AddDays(3);
                
                _logger.LogInformation("📅 Tìm users có subscription hết hạn vào: {Date}", 
                    threeDaysFromNow.ToString("dd/MM/yyyy"));
                
                // 1. Chỉ Select Id để chạy cho nhanh
                var userIdsToRemind = await _context.Users
                    .Where(u => u.SubscriptionEndDate.HasValue 
                                && u.SubscriptionEndDate.Value.Date == threeDaysFromNow
                                && (u.IsActive == true || u.IsActive == null)
                                && !string.IsNullOrEmpty(u.SubscriptionType)
                                && u.SubscriptionType != "free")
                    .Select(u => u.Id) // ✅ Chỉ lấy ID
                    .ToListAsync();

                _logger.LogInformation("🔍 Tìm thấy: {Count} user(s). Bắt đầu xếp hàng job...", userIdsToRemind.Count);

                if (!userIdsToRemind.Any())
                {
                    _logger.LogInformation("✅ Không có user nào cần nhắc nhở hôm nay.");
                    _logger.LogInformation("═══════════════════════════════════════════");
                    return;
                }

                // 2. Xếp hàng (Enqueue) các job con
                // Dùng INotificationService để xử lý logic
                foreach (var userId in userIdsToRemind)
                {
                    _backgroundJobClient.Enqueue<INotificationService>(
                        service => service.CreatePaymentReminderAsync(userId)
                    );
                }
                
                _logger.LogInformation("✅ Đã xếp hàng {Count} job con vào Hangfire!", userIdsToRemind.Count);
                _logger.LogInformation("╔════════════════════════════════════════╗");
                _logger.LogInformation("║  PAYMENT REMINDER FINDER JOB - KẾT THÚC ║");
                _logger.LogInformation("╚════════════════════════════════════════╝");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ LỖI KHI CHẠY PAYMENT REMINDER FINDER JOB");
                throw; // Ném lỗi để Hangfire tự động retry
            }
        }
    }
}
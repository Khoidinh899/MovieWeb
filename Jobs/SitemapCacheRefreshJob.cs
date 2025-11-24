using Hangfire;
using Microsoft.Extensions.Caching.Memory;

namespace MovieWeb.Jobs
{
    /// <summary>
    /// Job tự động làm mới cache của sitemap
    /// Chạy định kỳ hoặc trigger khi có phim mới
    /// </summary>
    public class SitemapCacheRefreshJob
    {
        private readonly IMemoryCache _cache;
        private readonly ILogger<SitemapCacheRefreshJob> _logger;
        private const string CACHE_KEY = "sitemap_xml";

        public SitemapCacheRefreshJob(
            IMemoryCache cache,
            ILogger<SitemapCacheRefreshJob> logger)
        {
            _cache = cache;
            _logger = logger;
        }

        /// <summary>
        /// Xóa cache sitemap để bắt buộc tạo lại khi có request tiếp theo
        /// </summary>
        [AutomaticRetry(Attempts = 3)]
        public void ClearSitemapCache()
        {
            try
            {
                _cache.Remove(CACHE_KEY);
                _logger.LogInformation("✅ [SitemapJob] Sitemap cache cleared at {Time}", DateTime.Now);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ [SitemapJob] Error clearing sitemap cache");
                throw; // Hangfire sẽ retry
            }
        }

        /// <summary>
        /// Schedule job chạy mỗi giờ để làm mới sitemap
        /// Gọi hàm này trong Program.cs hoặc Startup.cs
        /// </summary>
        public static void ScheduleRecurringJob()
        {
            // Chạy mỗi giờ vào phút thứ 5
            RecurringJob.AddOrUpdate<SitemapCacheRefreshJob>(
                "sitemap-cache-refresh",
                job => job.ClearSitemapCache(),
                "5 * * * *" // Cron: Mỗi giờ vào phút thứ 5
            );
        }

        /// <summary>
        /// Trigger job ngay lập tức (gọi khi sync phim mới)
        /// </summary>
        public static void TriggerImmediately()
        {
            BackgroundJob.Enqueue<SitemapCacheRefreshJob>(job => job.ClearSitemapCache());
        }
    }
}

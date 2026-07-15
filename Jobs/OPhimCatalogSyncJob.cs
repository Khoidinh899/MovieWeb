// Jobs/OPhimCatalogSyncJob.cs
using Microsoft.Extensions.Logging;
using MovieWeb.Services;
using System;
using System.Linq;
using System.Threading.Tasks;
using Hangfire;

namespace MovieWeb.Jobs
{
    public class OPhimCatalogSyncJob
    {
        private readonly IOPhimService _oPhimService;
        private readonly IMovieSyncService _movieSyncService;
        private readonly ILogger<OPhimCatalogSyncJob> _logger;

        public OPhimCatalogSyncJob(
            IOPhimService oPhimService,
            IMovieSyncService movieSyncService,
            ILogger<OPhimCatalogSyncJob> logger)
        {
            _oPhimService = oPhimService;
            _movieSyncService = movieSyncService;
            _logger = logger;
        }

        [AutomaticRetry(Attempts = 3)] // Retry 3 times if API fails
        public async Task Execute()
        {
            _logger.LogInformation("╔══════════════════════════════════════╗");
            _logger.LogInformation("║     OPHIM CATALOG SYNC JOB - BẮT ĐẦU ║");
            _logger.LogInformation("╚══════════════════════════════════════╝");

            // Quét qua 3 trang phim mới cập nhật để không bỏ sót bất kỳ phim/tập phim nào
            const int pagesToSync = 3;
            int totalProcessed = 0;

            for (int page = 1; page <= pagesToSync; page++)
            {
                _logger.LogInformation($"🔄 Đang quét API phim mới cập nhật - Trang {page}/{pagesToSync}...");

                try
                {
                    var apiResponse = await _oPhimService.GetLatestMoviesAsync(page);
                    var recentMovies = apiResponse?.Data?.Items;

                    if (recentMovies != null && recentMovies.Any())
                    {
                        // Gọi service đồng bộ các phim thuộc trang này (chỉ đồng bộ từ năm 2023 trở đi)
                        await _movieSyncService.SyncMoviesFromApiToDbAsync(recentMovies, minYear: 2023);
                        totalProcessed += recentMovies.Count;
                        _logger.LogInformation($"✅ Hoàn tất đồng bộ Trang {page}: đã xử lý {recentMovies.Count} phim.");
                    }
                    else
                    {
                        _logger.LogWarning($"⚠️ API trả về danh sách phim trống tại Trang {page}.");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"❌ Lỗi khi đồng bộ Trang {page}. Tiến trình vẫn tiếp tục các trang khác.");
                }
            }

            _logger.LogInformation($"✅ Kết thúc chu kỳ đồng bộ. Tổng số phim được quét qua: {totalProcessed} phim.");
            _logger.LogInformation("==========================================");
        }
    }
}

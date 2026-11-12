// Jobs/VSMovCatalogSyncJob.cs
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MovieWeb.Services;
using System;
using System.Linq;
using System.Threading.Tasks;
using Hangfire;

using Microsoft.Extensions.Caching.Memory;

namespace MovieWeb.Jobs
{
    public class VSMovCatalogSyncJob
    {
        private readonly IVSMovService _vsMovService;
        private readonly IMovieSyncService _movieSyncService;
        private readonly IMemoryCache _cache;
        private readonly ILogger<VSMovCatalogSyncJob> _logger;
        private readonly MovieWeb.Services.Interfaces.IIndexNowService _indexNow;
        private readonly Microsoft.AspNetCore.Hosting.IWebHostEnvironment _env;

        public VSMovCatalogSyncJob(
            IVSMovService vsMovService,
            IMovieSyncService movieSyncService,
            IMemoryCache cache,
            ILogger<VSMovCatalogSyncJob> logger,
            MovieWeb.Services.Interfaces.IIndexNowService indexNow,
            Microsoft.AspNetCore.Hosting.IWebHostEnvironment env)
        {
            _vsMovService = vsMovService;
            _movieSyncService = movieSyncService;
            _cache = cache;
            _logger = logger;
            _indexNow = indexNow;
            _env = env;
        }

        [AutomaticRetry(Attempts = 3)]
        public async Task Execute()
        {
            _logger.LogInformation("╔══════════════════════════════════════════════════════╗");
            _logger.LogInformation("║     VSMOV CATALOG AUTO-SYNC JOB - BẮT ĐẦU (60 PHÚT)  ║");
            _logger.LogInformation("╚══════════════════════════════════════════════════════╝");

            const int maxPages = 5;
            const int maxConsecutiveAlreadyUpToDate = 5;

            int totalProcessed = 0;
            int consecutiveAlreadyUpToDate = 0;
            bool stopSyncEarly = false;
            // URL phim vừa thêm/cập nhật tập: gửi IndexNow (Bing, Yandex…) để được thu thập sớm.
            var changedMovieUrls = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);

            for (int page = 1; page <= maxPages; page++)
            {
                if (stopSyncEarly) break;

                _logger.LogInformation($"🔄 [VSMov Job] Đang quét API phim mới cập nhật - Trang {page}/{maxPages}...");

                try
                {
                    var recentResponse = await _vsMovService.GetLatestMoviesAsync(page);
                    var recentItems = recentResponse?.Items;

                    if (recentItems == null || !recentItems.Any())
                    {
                        _logger.LogWarning($"⚠️ [VSMov Job] API VSMov trả về danh sách phim trống tại Trang {page}. Dừng quét.");
                        break;
                    }

                    foreach (var item in recentItems)
                    {
                        if (string.IsNullOrEmpty(item.Slug)) continue;

                        try
                        {
                            var (movie, wasUpdatedOrAdded) = await _movieSyncService.SyncSingleMovieFromVSMovBySlugWithStatusAsync(item.Slug);
                            totalProcessed++;

                            if (wasUpdatedOrAdded)
                            {
                                if (movie != null && (movie.IsActive ?? false))
                                    changedMovieUrls.Add("https://moonphim.me" + MovieWeb.Helpers.SeoUrlHelper.MovieUrl(movie.Slug, movie.MovieId));
                                consecutiveAlreadyUpToDate = 0; // Reset đếm trùng liên tiếp vì vừa có phim/tập mới
                                _logger.LogInformation($"✅ [VSMov Job] Đã cập nhật/thêm mới: '{item.Name}' ({item.Slug})");
                            }
                            else
                            {
                                consecutiveAlreadyUpToDate++;
                                _logger.LogInformation($"ℹ️ [VSMov Job] Phim '{item.Name}' đã đầy đủ trong DB ({consecutiveAlreadyUpToDate}/{maxConsecutiveAlreadyUpToDate} liên tiếp).");

                                if (consecutiveAlreadyUpToDate >= maxConsecutiveAlreadyUpToDate)
                                {
                                    _logger.LogInformation($"🛑 [VSMov Job] Đã phát hiện {maxConsecutiveAlreadyUpToDate} phim liên tiếp đã cập nhật đầy đủ trong CSDL. Tự động ngưng quét sớm!");
                                    stopSyncEarly = true;
                                    break;
                                }
                            }
                        }
                        catch (Exception exMovie)
                        {
                            _logger.LogError(exMovie, $"❌ [VSMov Job] Lỗi khi cào phim '{item.Slug}': {exMovie.Message}");
                        }
                    }

                    _logger.LogInformation($"✅ [VSMov Job] Hoàn tất Trang {page}. Tổng số phim đã xử lý: {totalProcessed}.");
                }
                catch (Exception exPage)
                {
                    _logger.LogError(exPage, $"❌ [VSMov Job] Lỗi khi lấy danh sách phim Trang {page}: {exPage.Message}");
                    break;
                }
            }

            // Tự động xóa cache sitemap để Google Search Console luôn nhận phim mới nhất
            _cache.Remove("sitemap_xml");
            _cache.Remove("sitemap_index_xml");

            if (changedMovieUrls.Count > 0 && !_env.IsDevelopment())
            {
                try
                {
                    var ok = await _indexNow.SubmitUrlsAsync(changedMovieUrls);
                    _logger.LogInformation("📡 [VSMov Job] IndexNow {Status} cho {Count} phim mới/cập nhật.", ok ? "đã gửi" : "không thành công", changedMovieUrls.Count);
                }
                catch (Exception exIndex)
                {
                    _logger.LogWarning(exIndex, "IndexNow submit failed");
                }
            }
            _logger.LogInformation("🔄 [VSMov Job] Đã làm mới sitemap cache cho các phim mới cào.");

            _logger.LogInformation($"🎉 [VSMov Job] Báo cáo hoàn thành: Đã cào {totalProcessed} phim. Kết thúc job.");
        }
    }
}

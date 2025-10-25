// File: Services/BackfillService.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MovieWeb.Data;
using MovieWeb.Services;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MovieWeb.Services
{
    /// <summary>
    /// Service này CHỈ DÙNG ĐỂ CHẠY THỦ CÔNG các tác vụ backfill một lần.
    /// Kích hoạt trong Program.cs khi cần, và gỡ ra ngay sau khi chạy xong.
    /// </summary>
    public class BackfillService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<BackfillService> _logger;

        public BackfillService(IServiceProvider serviceProvider, ILogger<BackfillService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);

            try
            {
                _logger.LogInformation(">>> BẮT ĐẦU CHẠY TÁC VỤ BACKFILL METADATA (chạy một lần duy nhất)...");
                
                using var scope = _serviceProvider.CreateScope();
                
                // Lấy các services cần thiết
                var context = scope.ServiceProvider.GetRequiredService<MovieWebDbContext>();
                var oPhimService = scope.ServiceProvider.GetRequiredService<IOPhimService>();
                var categorySyncService = scope.ServiceProvider.GetRequiredService<ICategorySyncService>();
                var countrySyncService = scope.ServiceProvider.GetRequiredService<ICountrySyncService>();
                var actorSyncService = scope.ServiceProvider.GetRequiredService<IActorSyncService>();
                var directorSyncService = scope.ServiceProvider.GetRequiredService<IDirectorSyncService>();
                
                await BackfillMetadataAsync(context, oPhimService, categorySyncService, countrySyncService, actorSyncService, directorSyncService);
                
                _logger.LogInformation(">>> TÁC VỤ BACKFILL METADATA ĐÃ HOÀN TẤT. Service sẽ dừng lại.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi nghiêm trọng xảy ra trong quá trình backfill metadata.");
            }
        }

        /// <summary>
        /// Quét lại tất cả phim trong DB và sync metadata (Categories, Countries, Actors, Directors)
        /// </summary>
        private async Task BackfillMetadataAsync(
            MovieWebDbContext context,
            IOPhimService oPhimService,
            ICategorySyncService categorySyncService,
            ICountrySyncService countrySyncService,
            IActorSyncService actorSyncService,
            IDirectorSyncService directorSyncService)
        {
            _logger.LogInformation("🔄🔄🔄 BẮT ĐẦU BACKFILL METADATA cho tất cả phim trong DB");

            var allMovies = await context.Movies
                                         .Include(m => m.Categories)
                                         .Include(m => m.Countries)
                                         .Include(m => m.Actors)
                                         .Include(m => m.Directors)
                                         .Where(m => !string.IsNullOrEmpty(m.Slug))
                                         .ToListAsync();

            if (!allMovies.Any())
            {
                _logger.LogWarning("❌ Không tìm thấy phim nào trong DB!");
                return;
            }

            _logger.LogInformation($"📊 Tìm thấy {allMovies.Count} phim trong DB. Bắt đầu quét...");

            int processedCount = 0;
            int updatedCount = 0;
            int skippedCount = 0;

            foreach (var movie in allMovies)
            {
                processedCount++;
                try
                {
                    _logger.LogInformation($"🎬 [{processedCount}/{allMovies.Count}] Đang xử lý: {movie.Name}");

                    // Gọi API để lấy chi tiết phim
                    var apiResponse = await oPhimService.GetMovieDetailAsync(movie.Slug);
                    var apiItem = apiResponse?.Item;

                    if (apiItem == null)
                    {
                        _logger.LogWarning($"⚠️  Không lấy được chi tiết API cho slug: {movie.Slug}");
                        skippedCount++;
                        continue;
                    }

                    bool hasChanges = false;

                    // === SYNC CATEGORIES ===
                    if (apiItem.Category != null && apiItem.Category.Any())
                    {
                        var syncedCategories = await categorySyncService.SyncCategoriesAsync(apiItem.Category);
                        
                        // Xóa categories cũ và thêm mới
                        movie.Categories.Clear();
                        foreach (var category in syncedCategories)
                        {
                            movie.Categories.Add(category);
                        }
                        
                        _logger.LogInformation($"   ✅ Đã sync {syncedCategories.Count} categories");
                        hasChanges = true;
                    }

                    // === SYNC COUNTRIES ===
                    if (apiItem.Country != null && apiItem.Country.Any())
                    {
                        var syncedCountries = await countrySyncService.SyncCountriesAsync(apiItem.Country);
                        
                        // Xóa countries cũ và thêm mới
                        movie.Countries.Clear();
                        foreach (var country in syncedCountries)
                        {
                            movie.Countries.Add(country);
                        }
                        
                        _logger.LogInformation($"   ✅ Đã sync {syncedCountries.Count} countries");
                        hasChanges = true;
                    }

                    // === SYNC ACTORS ===
                    if (apiItem.Actor != null && apiItem.Actor.Any())
                    {
                        var syncedActors = await actorSyncService.SyncActorsAsync(apiItem.Actor);
                        
                        // Xóa actors cũ và thêm mới
                        movie.Actors.Clear();
                        foreach (var actor in syncedActors)
                        {
                            movie.Actors.Add(actor);
                        }
                        
                        _logger.LogInformation($"   ✅ Đã sync {syncedActors.Count} actors");
                        hasChanges = true;
                    }

                    // === SYNC DIRECTORS ===
                    if (apiItem.Director != null && apiItem.Director.Any())
                    {
                        var syncedDirectors = await directorSyncService.SyncDirectorsAsync(apiItem.Director);
                        
                        // Xóa directors cũ và thêm mới
                        movie.Directors.Clear();
                        foreach (var director in syncedDirectors)
                        {
                            movie.Directors.Add(director);
                        }
                        
                        _logger.LogInformation($"   ✅ Đã sync {syncedDirectors.Count} directors");
                        hasChanges = true;
                    }

                    if (hasChanges)
                    {
                        movie.UpdatedAt = DateTime.Now;
                        updatedCount++;
                        _logger.LogInformation($"   💾 Đã cập nhật metadata cho phim: {movie.Name}");
                    }
                    else
                    {
                        _logger.LogInformation($"   ⏭️  Không có metadata mới cho phim: {movie.Name}");
                    }

                    // Lưu theo lô (mỗi 10 phim)
                    if (processedCount % 10 == 0 && context.ChangeTracker.HasChanges())
                    {
                        await context.SaveChangesAsync();
                        _logger.LogInformation($"💾 =======> Đã lưu 1 lô phim vào DB (Progress: {processedCount}/{allMovies.Count}) <=======");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"❌ Lỗi khi xử lý phim: {movie.Name}");
                    skippedCount++;
                }
            }

            // Lưu lô cuối cùng
            if (context.ChangeTracker.HasChanges())
            {
                await context.SaveChangesAsync();
                _logger.LogInformation("💾 Đã lưu lô phim cuối cùng vào DB.");
            }

            _logger.LogInformation($"🎬 === HOÀN TẤT === Cập nhật: {updatedCount}, Bỏ qua: {skippedCount}, Tổng xử lý: {processedCount}");
        }
    }
}
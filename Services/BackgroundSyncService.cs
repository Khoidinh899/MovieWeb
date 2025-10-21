// File: Services/BackgroundSyncService.cs
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MovieWeb.Services;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MovieWeb.Services
{
    public class BackgroundSyncService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<BackgroundSyncService> _logger;

        public BackgroundSyncService(IServiceProvider serviceProvider, ILogger<BackgroundSyncService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Background Sync Service is starting.");

            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Starting new movie sync cycle...");

                try
                {
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var oPhimService = scope.ServiceProvider.GetRequiredService<IOPhimService>();
                        var movieSyncService = scope.ServiceProvider.GetRequiredService<IMovieSyncService>();

                        // === SỬA LỖI Ở ĐÂY: Gọi đúng tên hàm là GetLatestMoviesAsync ===
                        var apiResponse = await oPhimService.GetLatestMoviesAsync(1);
                        var recentMovies = apiResponse?.Data?.Items;

                        if (recentMovies != null && recentMovies.Any())
                        {
                            // Sync chỉ phim từ 2023 trở đi
                            await movieSyncService.SyncMoviesFromApiToDbAsync(recentMovies, minYear: 2023);
                            _logger.LogInformation($"Sync cycle completed. Processed {recentMovies.Count} items.");
                        }
                        else
                        {
                            _logger.LogInformation("No new movies to sync in this cycle.");
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "An error occurred during the sync cycle.");
                }

                _logger.LogInformation("Next sync cycle will start in 6 hours.");
                await Task.Delay(TimeSpan.FromHours(6), stoppingToken);
            }

            _logger.LogInformation("Background Sync Service is stopping.");
        }
    }
}
using MovieWeb.Services;
using ApiMovie = MovieWeb.Models.API.Movie;

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
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var syncService = scope.ServiceProvider.GetRequiredService<IMovieSyncService>();
                    var oPhimService = scope.ServiceProvider.GetRequiredService<IOPhimService>();

                    // Lấy phim mới từ API
                    var latestMovies = await oPhimService.GetLatestMoviesAsync(1);
                    if (latestMovies?.Data?.Items != null)
                    {
                        // Chỉ lấy phim có Year >= 2023
                        var apiMovieList = latestMovies.Data.Items
                            .Where(m => m.Year >= 2023)
                            .ToList();

                        if (apiMovieList.Any())
                        {
                            await syncService.SyncMoviesFromApiToDbAsync(apiMovieList);
                            _logger.LogInformation($"Synced {apiMovieList.Count} movies (Year >= 2023) to database");
                        }
                        else
                        {
                            _logger.LogInformation("Không có phim nào từ 2023 trở đi để sync.");
                        }
                    }

                    // Chờ 1 giờ trước khi sync lần tiếp theo
                    await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in background sync");
                    // Nếu lỗi thì delay 10 phút rồi thử lại
                    await Task.Delay(TimeSpan.FromMinutes(10), stoppingToken);
                }
            }
        }
    }
}

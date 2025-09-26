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

                    // Sync phim mới mỗi 1 giờ
                    var latestMovies = await oPhimService.GetLatestMoviesAsync(1);
                    if (latestMovies?.Data?.Items != null)
                    {
                        // Convert List<Movie> API thành List<ApiMovie>
                        var apiMovieList = latestMovies.Data.Items.ToList();
                        await syncService.SyncMoviesFromApiToDbAsync(apiMovieList);
                        _logger.LogInformation($"Synced {apiMovieList.Count} movies to database");
                    }

                    // Chờ 1 giờ
                    await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in background sync");
                    await Task.Delay(TimeSpan.FromMinutes(10), stoppingToken);
                }
            }
        }
    }
}
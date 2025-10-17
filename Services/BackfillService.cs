// File: Services/BackfillService.cs
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MovieWeb.Services;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace MovieWeb.Services
{
    /// <summary>
    /// Service này CHỈ DÙNG ĐỂ CHẠY THỦ CÔNG tác vụ BackfillAllEpisodesAsync một lần.
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
                _logger.LogInformation(">>> BẮT ĐẦU CHẠY TÁC VỤ SỬA LỖI TẬP PHIM (chạy một lần duy nhất)...");
                
                using var scope = _serviceProvider.CreateScope();
                var movieSyncService = scope.ServiceProvider.GetRequiredService<IMovieSyncService>();
                
                await movieSyncService.BackfillAllEpisodesAsync();
                
                _logger.LogInformation(">>> TÁC VỤ SỬA LỖI TẬP PHIM ĐÃ HOÀN TẤT. Service sẽ dừng lại.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi nghiêm trọng xảy ra trong quá trình sửa lỗi tập phim.");
            }
        }
    }
}
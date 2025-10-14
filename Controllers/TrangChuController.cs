using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using MovieWeb.Models.API;
using MovieWeb.Repositories;
using MovieWeb.Extensions;

namespace MovieWeb.Controllers
{
    public class TrangChuController : Controller
    {
        private readonly ILogger<TrangChuController> _logger;
        private readonly IMovieRepository _movieRepository;
        private readonly IMemoryCache _cache;

        public TrangChuController(ILogger<TrangChuController> logger, IMovieRepository movieRepository, IMemoryCache cache)
        {
            _logger = logger;
            _movieRepository = movieRepository;
            _cache = cache;
        }

        public async Task<IActionResult> TrangChu()
        {
            try
            {
                var viewModel = new HomeViewModel();

                // CDN Domain cho ảnh
                viewModel.CdnImageDomain = "https://img.ophim.live/uploads/movies/";

                // Lấy phim banner - THAY ĐỔI: Ưu tiên phim được đánh dấu IsBanner
                var cacheKeyBanner = "banner_movies_with_content";
                if (!_cache.TryGetValue(cacheKeyBanner, out List<Movie> bannerMovies))
                {
                    _logger.LogInformation("Loading banner movies from database...");

                    // Lấy phim được đánh dấu làm banner trước
                    var bannerMarkedMovies = await _movieRepository.GetBannerMoviesAsync(5);

                    if (bannerMarkedMovies != null && bannerMarkedMovies.Any())
                    {
                        bannerMovies = bannerMarkedMovies.ToApiModelList();
                        _logger.LogInformation($"Loaded {bannerMovies.Count} marked banner movies");
                    }
                    else
                    {
                        // Fallback: nếu không có phim nào được đánh dấu, lấy hot movies
                        _logger.LogInformation("No marked banner movies, falling back to hot movies...");
                        var hotMoviesFromDb = await _movieRepository.GetHotMoviesAsync(5);
                        
                        if (hotMoviesFromDb != null && hotMoviesFromDb.Any())
                        {
                            bannerMovies = hotMoviesFromDb.ToApiModelList();
                            _logger.LogInformation($"Loaded {bannerMovies.Count} hot movies as banner fallback");
                        }
                        else
                        {
                            bannerMovies = new List<Movie>();
                            _logger.LogWarning("No movies found for banner");
                        }
                    }

                    _cache.Set(cacheKeyBanner, bannerMovies, TimeSpan.FromMinutes(30));
                }

                viewModel.HotMovies = bannerMovies;
                viewModel.BannerMovies = bannerMovies;

                // Lấy phim mới cập nhật với cache
                var cacheKey = "latest_movies_page_1_db";
                if (!_cache.TryGetValue(cacheKey, out List<Movie> latestMovies))
                {
                    _logger.LogInformation("Loading latest movies from database...");

                    var latestResult = await _movieRepository.GetLatestMoviesAsync(1, 12);
                    latestMovies = latestResult.Items.ToApiModelList();

                    _logger.LogInformation($"Loaded {latestMovies.Count} latest movies from database");
                    _cache.Set(cacheKey, latestMovies, TimeSpan.FromMinutes(10));
                }

                viewModel.LatestMovies = latestMovies;

                // Lấy phim lẻ
                var cacheKeyMovies = "single_movies_page_1_db";
                if (!_cache.TryGetValue(cacheKeyMovies, out List<Movie> singleMovies))
                {
                    _logger.LogInformation("Loading single movies from database...");

                    var singleResult = await _movieRepository.GetMoviesByTypeAsync("single", 1, 12);
                    singleMovies = singleResult.Items.ToApiModelList();

                    _logger.LogInformation($"Loaded {singleMovies.Count} single movies from database");
                    _cache.Set(cacheKeyMovies, singleMovies, TimeSpan.FromMinutes(10));
                }

                viewModel.SingleMovies = singleMovies;

                // Lấy phim bộ
                var cacheKeySeries = "tv_series_page_1_db";
                if (!_cache.TryGetValue(cacheKeySeries, out List<Movie> tvSeries))
                {
                    _logger.LogInformation("Loading TV series from database...");

                    var tvResult = await _movieRepository.GetMoviesByTypeAsync("series", 1, 12);
                    tvSeries = tvResult.Items.ToApiModelList();

                    _logger.LogInformation($"Loaded {tvSeries.Count} TV series from database");
                    _cache.Set(cacheKeySeries, tvSeries, TimeSpan.FromMinutes(10));
                }

                viewModel.TvSeries = tvSeries;
                
                // Lấy phim hoạt hình
                var cacheKeyHoatHinh = "hoathinh_movies_page_1_db";
                if (!_cache.TryGetValue(cacheKeyHoatHinh, out List<Movie> hoatHinhMovies))
                {
                    _logger.LogInformation("Loading animation movies from database...");

                    var hoatHinhResult = await _movieRepository.GetMoviesByTypeAsync("hoathinh", 1, 12);
                    hoatHinhMovies = hoatHinhResult.Items.ToApiModelList();

                    _logger.LogInformation($"Loaded {hoatHinhMovies.Count} animation movies from database");
                    _cache.Set(cacheKeyHoatHinh, hoatHinhMovies, TimeSpan.FromMinutes(10));
                }

                viewModel.HoatHinhMovies = hoatHinhMovies;

                _logger.LogInformation($"TrangChu loaded successfully: {viewModel.BannerMovies.Count} banner, {viewModel.LatestMovies.Count} latest, {viewModel.SingleMovies.Count} single, {viewModel.TvSeries.Count} series");

                return View("~/Views/Home/TrangChu.cshtml", viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading TrangChu page from database");

                var viewModel = new HomeViewModel
                {
                    HotMovies = new List<Movie>(),
                    LatestMovies = new List<Movie>(),
                    SingleMovies = new List<Movie>(),
                    TvSeries = new List<Movie>(),
                    BannerMovies = new List<Movie>(),
                    CdnImageDomain = "https://img.ophim.live/uploads/movies/"
                };

                return View("~/Views/Home/TrangChu.cshtml", viewModel);
            }
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }

    public class ErrorViewModel
    {
        public string? RequestId { get; set; }
        public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
    }
}
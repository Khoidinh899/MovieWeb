using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using MovieWeb.Models;
using MovieWeb.Services;

namespace MovieWeb.Controllers
{
    public class TrangChuController : Controller
    {
        private readonly ILogger<TrangChuController> _logger;
        private readonly IOPhimService _oPhimService;
        private readonly IMemoryCache _cache;

        public TrangChuController(ILogger<TrangChuController> logger, IOPhimService oPhimService, IMemoryCache cache)
        {
            _logger = logger;
            _oPhimService = oPhimService;
            _cache = cache;
        }

        public async Task<IActionResult> TrangChu()
{
    try
    {
        var viewModel = new HomeViewModel();

        var cacheKeyBanner = "banner_movies_with_content";
        if (!_cache.TryGetValue(cacheKeyBanner, out List<Movie> bannerMovies))
        {
            var singleMoviesResponse = await _oPhimService.GetMoviesByTypeAsync("phim-le", 1);
            if (singleMoviesResponse != null && singleMoviesResponse.Status == "success")
            {
                var movies = singleMoviesResponse.Data?.Items?.Take(6).ToList() ?? new List<Movie>();
                bannerMovies = new List<Movie>();

                // Lấy chi tiết cho từng phim để có content
                foreach (var movie in movies)
                {
                    try
                    {
                        if (!string.IsNullOrEmpty(movie.Slug))
                        {
                            _logger.LogInformation($"Getting detail for movie: {movie.Name} - Slug: {movie.Slug}");
                            
                            var movieDetail = await _oPhimService.GetMovieDetailAsync(movie.Slug);
                            if (movieDetail != null && !string.IsNullOrEmpty(movieDetail.Content))
                            {
                                // Cập nhật content từ API chi tiết
                                movie.Content = movieDetail.Content;
                                _logger.LogInformation($"Got content for {movie.Name}: {movie.Content.Substring(0, Math.Min(50, movie.Content.Length))}...");
                            }
                            else
                            {
                                _logger.LogWarning($"No content found for movie: {movie.Name}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Error getting detail for movie: {movie.Name}");
                    }
                    
                    bannerMovies.Add(movie);
                }

                _cache.Set(cacheKeyBanner, bannerMovies, TimeSpan.FromMinutes(30));
            }
            else
            {
                bannerMovies = new List<Movie>();
            }
        }

        viewModel.HotMovies = bannerMovies;

        // Lấy phim mới cập nhật với cache
        var cacheKey = "latest_movies_page_1";
        if (!_cache.TryGetValue(cacheKey, out OPhimResponse latestMovies))
        {
            latestMovies = await _oPhimService.GetLatestMoviesAsync(1);
            if (latestMovies != null && latestMovies.Status == "success")
            {
                _cache.Set(cacheKey, latestMovies, TimeSpan.FromMinutes(10));
            }
        }

        viewModel.LatestMovies = latestMovies?.Data?.Items ?? new List<Movie>();

        // Lấy phim lẻ
        var cacheKeyMovies = "single_movies_page_1";
        if (!_cache.TryGetValue(cacheKeyMovies, out OPhimResponse singleMovies))
        {
            singleMovies = await _oPhimService.GetMoviesByTypeAsync("phim-le", 1);
            if (singleMovies != null && singleMovies.Status == "success")
            {
                _cache.Set(cacheKeyMovies, singleMovies, TimeSpan.FromMinutes(10));
            }
        }

        viewModel.SingleMovies = singleMovies?.Data?.Items ?? new List<Movie>();

        // Lấy phim bộ
        var cacheKeySeries = "tv_series_page_1";
        if (!_cache.TryGetValue(cacheKeySeries, out OPhimResponse tvSeries))
        {
            tvSeries = await _oPhimService.GetMoviesByTypeAsync("phim-bo", 1);
            if (tvSeries != null && tvSeries.Status == "success")
            {
                _cache.Set(cacheKeySeries, tvSeries, TimeSpan.FromMinutes(10));
            }
        }

        viewModel.TvSeries = tvSeries?.Data?.Items ?? new List<Movie>();

        return View("~/Views/Home/TrangChu.cshtml", viewModel);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error loading home page");
        var viewModel = new HomeViewModel
        {
            HotMovies = new List<Movie>(),
            LatestMovies = new List<Movie>(),
            SingleMovies = new List<Movie>(),
            TvSeries = new List<Movie>()
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
}
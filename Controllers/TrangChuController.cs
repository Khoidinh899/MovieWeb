using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.EntityFrameworkCore;
using MovieWeb.Models.Entities;
using MovieWeb.Data;

namespace MovieWeb.Controllers
{
    public class TrangChuController : Controller
    {
        private readonly ILogger<TrangChuController> _logger;
        private readonly IMemoryCache _cache;
        private readonly MovieWebDbContext _context;

        public TrangChuController(
            ILogger<TrangChuController> logger,
            IMemoryCache cache,
            MovieWebDbContext context)
        {
            _logger = logger;
            _cache = cache;
            _context = context;
        }

        public async Task<IActionResult> TrangChu()
        {
            try
            {
                var viewModel = new HomeViewModel
                {
                    CdnImageDomain = "https://img.ophim.live/uploads/movies/"
                };

                // ===== PHIM BANNER =====
                var cacheKeyBanner = "banner_movies_entities";
                if (!_cache.TryGetValue(cacheKeyBanner, out List<Movie> bannerMovies))
                {
                    bannerMovies = await _context.Movies
                        .Where(m => (m.IsActive ?? false) && (m.IsBanner ?? false))
                        .OrderByDescending(m => m.UpdatedAt)
                        .Take(5)
                        .ToListAsync();

                    if (!bannerMovies.Any())
                    {
                        bannerMovies = await _context.Movies
                            .Where(m => (m.IsActive ?? false))
                            .OrderByDescending(m => m.ViewCount)
                            .Take(5)
                            .ToListAsync();
                    }

                    _cache.Set(cacheKeyBanner, bannerMovies, TimeSpan.FromMinutes(30));
                }

                viewModel.BannerMovies = bannerMovies;
                viewModel.HotMovies = bannerMovies;

                // ===== PHIM MỚI =====
                var cacheKeyLatest = "latest_movies_entities";
                if (!_cache.TryGetValue(cacheKeyLatest, out List<Movie> latestMovies))
                {
                    latestMovies = await _context.Movies
                        .Where(m => (m.IsActive ?? false))
                        .OrderByDescending(m => m.UpdatedAt)
                        .Take(12)
                        .ToListAsync();

                    _cache.Set(cacheKeyLatest, latestMovies, TimeSpan.FromMinutes(15));
                }
                viewModel.LatestMovies = latestMovies;

                // ===== PHIM LẺ =====
                var cacheKeySingle = "single_movies_entities";
                if (!_cache.TryGetValue(cacheKeySingle, out List<Movie> singleMovies))
                {
                    singleMovies = await _context.Movies
                        .Where(m => (m.IsActive ?? false) && m.Type == "single")
                        .OrderByDescending(m => m.UpdatedAt)
                        .Take(12)
                        .ToListAsync();

                    _cache.Set(cacheKeySingle, singleMovies, TimeSpan.FromMinutes(15));
                }
                viewModel.SingleMovies = singleMovies;

                // ===== PHIM BỘ =====
                var cacheKeySeries = "series_movies_entities";
                if (!_cache.TryGetValue(cacheKeySeries, out List<Movie> seriesMovies))
                {
                    seriesMovies = await _context.Movies
                        .Where(m => (m.IsActive ?? false) && m.Type == "series")
                        .OrderByDescending(m => m.UpdatedAt)
                        .Take(12)
                        .ToListAsync();

                    _cache.Set(cacheKeySeries, seriesMovies, TimeSpan.FromMinutes(15));
                }
                viewModel.TvSeries = seriesMovies;

                // ===== HOẠT HÌNH =====
                var cacheKeyHoatHinh = "hoathinh_movies_entities";
                if (!_cache.TryGetValue(cacheKeyHoatHinh, out List<Movie> hoatHinhMovies))
                {
                    hoatHinhMovies = await _context.Movies
                        .Where(m => (m.IsActive ?? false) && m.Type == "hoathinh")
                        .OrderByDescending(m => m.UpdatedAt)
                        .Take(12)
                        .ToListAsync();

                    _cache.Set(cacheKeyHoatHinh, hoatHinhMovies, TimeSpan.FromMinutes(15));
                }
                viewModel.HoatHinhMovies = hoatHinhMovies;

                // ===== QUẢNG CÁO =====
                var advertisements = await _context.Advertisements
                    .Where(a => a.IsActive)
                    .OrderBy(a => a.DisplayOrder)
                    .ToListAsync();
                ViewBag.Advertisements = advertisements;

                return View("~/Views/Home/TrangChu.cshtml", viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading TrangChu");

                var viewModel = new HomeViewModel
                {
                    CdnImageDomain = "https://img.ophim.live/uploads/movies/"
                };
                ViewBag.Advertisements = new List<Advertisement>();
                return View("~/Views/Home/TrangChu.cshtml", viewModel);
            }
        }

        public IActionResult Privacy() => View();

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel
            {
                RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier
            });
        }
    }

    public class ErrorViewModel
    {
        public string? RequestId { get; set; }
        public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
    }
}

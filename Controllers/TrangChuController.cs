using System;
using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.EntityFrameworkCore;
using MovieWeb.Models.Entities;
using MovieWeb.Data;
using MovieWeb.Services.Interfaces;

namespace MovieWeb.Controllers
{
    public class TrangChuController : Controller
    {
        private readonly ILogger<TrangChuController> _logger;
        private readonly IMemoryCache _cache;
        private readonly MovieWebDbContext _context;
        private readonly IAuthService _authService; 

        public TrangChuController(
            ILogger<TrangChuController> logger,
            IMemoryCache cache,
            MovieWebDbContext context,
            IAuthService authService)
        {
            _logger = logger;
            _cache = cache;
            _context = context;
            _authService = authService;
        }

        public async Task<IActionResult> TrangChu()
        {
            try
            {
                var viewModel = new HomeViewModel
                {
                    CdnImageDomain = "https://vsmov.com/storage/images/"
                };

                // ===== PHIM BANNER =====
                var cacheKeyBanner = "banner_movies_entities";
                if (!_cache.TryGetValue(cacheKeyBanner, out List<Movie> bannerMovies))
                {
                    bannerMovies = await _context.Movies
                        .Where(m => m.IsActive == true && (m.IsBanner ?? false))
                        .OrderByDescending(m => m.UpdatedAt)
                        .Take(5)
                        .ToListAsync();

                    if (!bannerMovies.Any())
                    {
                        bannerMovies = await _context.Movies
                            .Where(m => m.IsActive == true)
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
                        .Where(m => m.IsActive == true && (m.Episodes.Any() || !string.IsNullOrEmpty(m.TrailerUrl)))
                        .OrderByDescending(m => m.Year)
                        .ThenByDescending(m => m.UpdatedAt)
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
                        .Where(m => m.IsActive == true && m.Type == "single" && !string.IsNullOrEmpty(m.TrailerUrl))
                        .OrderByDescending(m => m.Year)
                        .ThenByDescending(m => m.UpdatedAt)
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
                        .Where(m => m.IsActive == true && m.Type == "series" && m.Episodes.Any())
                        .OrderByDescending(m => m.Year)
                        .ThenByDescending(m => m.UpdatedAt)
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
                        .Where(m => m.IsActive == true && m.Type == "hoathinh" && (m.Episodes.Any() || !string.IsNullOrEmpty(m.TrailerUrl)))
                        .OrderByDescending(m => m.Year)
                        .ThenByDescending(m => m.UpdatedAt)
                        .Take(12)
                        .ToListAsync();

                    _cache.Set(cacheKeyHoatHinh, hoatHinhMovies, TimeSpan.FromMinutes(15));
                }
                viewModel.HoatHinhMovies = hoatHinhMovies;

                // ===== TOP 10 PHIM TRENDING TRÊN MOONPHIM (Lượt xem nhiều nhất) =====
                var cacheKeyTrending = "top10_trending_movies_entities";
                if (!_cache.TryGetValue(cacheKeyTrending, out List<Movie> trendingMovies))
                {
                    trendingMovies = await _context.Movies
                        .Include(m => m.Categories)
                        .Include(m => m.Episodes)
                        .Where(m => m.IsActive == true && (m.Episodes.Any() || !string.IsNullOrEmpty(m.TrailerUrl)))
                        .OrderByDescending(m => m.ViewCount ?? 0)
                        .ThenByDescending(m => m.UpdatedAt ?? m.CreatedAt)
                        .Take(10)
                        .ToListAsync();

                    _cache.Set(cacheKeyTrending, trendingMovies, TimeSpan.FromMinutes(10));
                }
                viewModel.TrendingMovies = trendingMovies;
                viewModel.UpcomingMovies = trendingMovies; // Fallback

                bool shouldShowAds = true;
                var currentUser = await _authService.GetCurrentUserAsync(); // Lấy user hiện tại

                if (currentUser != null)
                {
                    var subscriptionType = currentUser.SubscriptionType?.ToLower() ?? "free";
                    // Nếu là Admin, Premium HOẶC Student thì TẮT QUẢNG CÁO
                    if (currentUser.IsAdmin || subscriptionType == "premium" || subscriptionType == "student")
                    {
                        shouldShowAds = false;
                    }
                }
                ViewBag.ShouldShowAds = shouldShowAds;

                // ===== QUẢNG CÁO (Chỉ load nếu cần) =====
                if (shouldShowAds)
                {
                    _logger.LogInformation("User is Free/Guest. Loading homepage ads.");
                    var advertisements = await _context.Advertisements
                        .Where(a => a.IsActive)
                        .OrderBy(a => a.DisplayOrder)
                        .ToListAsync();
                    ViewBag.Advertisements = advertisements;
                }
                else
                {
                    _logger.LogInformation("User is Admin/Premium/Student. Hiding homepage ads.");
                    // Nếu là Premium/Admin, trả về danh sách rỗng
                    ViewBag.Advertisements = new List<Advertisement>();
                }

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
                ViewBag.ShouldShowAds = true; // Lỗi thì cứ hiện QC cho chắc
                return View("~/Views/Home/TrangChu.cshtml", viewModel);
            }
        }

        public IActionResult Privacy() => View();
    }
}

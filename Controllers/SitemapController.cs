using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Xml.Linq;
using MovieWeb.Models.Entities; 
using MovieWeb.Data; 

namespace MovieWeb.Controllers
{
    public class SitemapController : Controller
    {
        private readonly MovieWebDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly ILogger<SitemapController> _logger;
        
        // Cấu hình
        private const string BASE_URL = "https://moonphim.me";
        private const int MAX_MOVIES = 10000; // Giới hạn số phim trong sitemap
        private const string CACHE_KEY = "sitemap_xml";
        private const int CACHE_MINUTES = 60; // Cache 1 giờ

        public SitemapController(
            MovieWebDbContext context, 
            IMemoryCache cache,
            ILogger<SitemapController> logger)
        {
            _context = context;
            _cache = cache;
            _logger = logger;
        }

        /// <summary>
        /// Sitemap chính - Tự động cache và cập nhật
        /// </summary>
        [Route("sitemap.xml")]
        [ResponseCache(Duration = 3600, Location = ResponseCacheLocation.Any)]
        public async Task<IActionResult> Index()
        {
            try
            {
                // Kiểm tra cache trước
                if (_cache.TryGetValue(CACHE_KEY, out string? cachedXml) && !string.IsNullOrEmpty(cachedXml))
                {
                    _logger.LogInformation("✅ Sitemap loaded from cache");
                    return Content(cachedXml, "application/xml");
                }

                // Tạo sitemap mới
                var xml = await GenerateSitemapXml();
                
                // Lưu vào cache
                var cacheOptions = new MemoryCacheEntryOptions()
                    .SetAbsoluteExpiration(TimeSpan.FromMinutes(CACHE_MINUTES))
                    .SetPriority(CacheItemPriority.High);
                
                _cache.Set(CACHE_KEY, xml, cacheOptions);
                _logger.LogInformation($"✅ Sitemap generated and cached for {CACHE_MINUTES} minutes");

                return Content(xml, "application/xml");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error generating sitemap");
                return StatusCode(500, "Error generating sitemap");
            }
        }

        /// <summary>
        /// Xóa cache sitemap (gọi khi cập nhật phim mới)
        /// </summary>
        [HttpPost]
        [Route("api/sitemap/clear-cache")]
        public IActionResult ClearCache()
        {
            try
            {
                _cache.Remove(CACHE_KEY);
                _logger.LogInformation("🗑️ Sitemap cache cleared successfully");
                return Ok(new { success = true, message = "Sitemap cache cleared" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error clearing sitemap cache");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Tạo nội dung XML của sitemap
        /// </summary>
        private async Task<string> GenerateSitemapXml()
        {
            XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";
            var sitemap = new XElement(ns + "urlset");

            // 1. ===== TRANG CHỦ =====
            sitemap.Add(CreateUrlElement(ns, 
                url: BASE_URL,
                lastmod: DateTime.Now,
                changefreq: "daily",
                priority: "1.0"
            ));

            // 2. ===== CÁC TRANG DANH MỤC CHÍNH =====
            var staticPages = new[]
            {
                new { Path = "/the-loai/phim-le", Priority = "0.9", Changefreq = "daily" },
                new { Path = "/the-loai/phim-bo", Priority = "0.9", Changefreq = "daily" },
                new { Path = "/the-loai/hoat-hinh", Priority = "0.9", Changefreq = "daily" },
                new { Path = "/the-loai/phim-moi-cap-nhat", Priority = "0.9", Changefreq = "hourly" },
                new { Path = "/tim-kiem", Priority = "0.7", Changefreq = "weekly" }
            };

            foreach (var page in staticPages)
            {
                sitemap.Add(CreateUrlElement(ns,
                    url: $"{BASE_URL}{page.Path}",
                    lastmod: DateTime.Now,
                    changefreq: page.Changefreq,
                    priority: page.Priority
                ));
            }

            // 3. ===== THỂ LOẠI (CATEGORIES) =====
            var categories = await _context.Categories
                .AsNoTracking()
                .Where(c => c.IsActive == true)
                .OrderBy(c => c.Name)
                .Select(c => new { c.Slug, c.Name })
                .ToListAsync();

            foreach (var category in categories)
            {
                sitemap.Add(CreateUrlElement(ns,
                    url: $"{BASE_URL}/the-loai/{category.Slug}",
                    lastmod: DateTime.Now,
                    changefreq: "daily",
                    priority: "0.8"
                ));
            }

            // 4. ===== QUỐC GIA (COUNTRIES) - Nếu bạn có trang riêng cho từng quốc gia =====
            var countries = await _context.Countries
                .AsNoTracking()
                .Where(c => c.IsActive == true)
                .OrderBy(c => c.Name)
                .Select(c => new { c.Slug, c.Name })
                .ToListAsync();

            // Uncomment nếu bạn có route cho quốc gia, ví dụ: /quoc-gia/{slug}
            /*
            foreach (var country in countries)
            {
                sitemap.Add(CreateUrlElement(ns,
                    url: $"{BASE_URL}/quoc-gia/{country.Slug}",
                    lastmod: DateTime.Now,
                    changefreq: "daily",
                    priority: "0.7"
                ));
            }
            */

            // 5. ===== PHIM (MOVIES) =====
            var movies = await _context.Movies
                .AsNoTracking()
                .Where(m => m.IsActive == true)
                .OrderByDescending(m => m.UpdatedAt ?? m.CreatedAt)
                .Select(m => new 
                { 
                    m.Slug, 
                    m.UpdatedAt,
                    m.CreatedAt,
                    m.ViewCount
                })
                .Take(MAX_MOVIES)
                .ToListAsync();

            _logger.LogInformation($"📊 Generating sitemap for {movies.Count} movies");

            foreach (var movie in movies)
            {
                var lastModDate = movie.UpdatedAt ?? movie.CreatedAt ?? DateTime.Now;
                
                // Ưu tiên cao hơn cho phim có lượt xem cao
                var priority = movie.ViewCount > 10000 ? "0.9" :
                              movie.ViewCount > 5000 ? "0.8" :
                              movie.ViewCount > 1000 ? "0.7" : "0.6";

                sitemap.Add(CreateUrlElement(ns,
                    url: $"{BASE_URL}/phim/{movie.Slug}",
                    lastmod: lastModDate,
                    changefreq: "weekly",
                    priority: priority
                ));
            }

            // 6. Tạo XML Document
            var xml = new XDocument(
                new XDeclaration("1.0", "utf-8", "yes"), 
                sitemap
            );

            return xml.ToString();
        }

        /// <summary>
        /// Tạo element URL cho sitemap
        /// </summary>
        private XElement CreateUrlElement(
            XNamespace ns, 
            string url, 
            DateTime lastmod, 
            string changefreq, 
            string priority)
        {
            return new XElement(ns + "url",
                new XElement(ns + "loc", url),
                new XElement(ns + "lastmod", lastmod.ToString("yyyy-MM-dd")),
                new XElement(ns + "changefreq", changefreq),
                new XElement(ns + "priority", priority)
            );
        }
    }
}
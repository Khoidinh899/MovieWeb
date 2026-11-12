using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using MovieWeb.Data;
using MovieWeb.Helpers;
using System.Xml.Linq;

namespace MovieWeb.Controllers;

public class SitemapController : Controller
{
    private const int MaxMovies = 10000;
    private const string CacheKey = "sitemap_xml";
    private const string IndexCacheKey = "sitemap_index_xml";
    private const int EpisodesPerSitemap = 2000; // Giới hạn tối đa 2.000 URL/tệp sitemap để bot cào nhanh
    private static readonly SemaphoreSlim GenerationLock = new(1, 1);
    private readonly MovieWebDbContext _context;
    private readonly IMemoryCache _cache;
    private readonly ILogger<SitemapController> _logger;
    private readonly IConfiguration _configuration;

    public SitemapController(MovieWebDbContext context, IMemoryCache cache, ILogger<SitemapController> logger, IConfiguration configuration)
    {
        _context = context;
        _cache = cache;
        _logger = logger;
        _configuration = configuration;
    }

    /// <summary>/sitemap.xml là sitemap index: sitemap-main.xml (trang, phim, thể loại, quốc gia), cộng sitemap-episodes-N.xml
    /// khi bật FeatureFlags:SitemapEpisodes (mặc định tắt: dồn quota thu thập của Google cho trang phim trước).</summary>
    [HttpGet("sitemap.xml")]
    [ResponseCache(Duration = 3600, Location = ResponseCacheLocation.Any)]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        if (_cache.TryGetValue(IndexCacheKey, out string? cachedIndex) && cachedIndex is not null)
            return Content(cachedIndex, "application/xml");
        try
        {
            XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";
            var pages = 0;
            if (_configuration.GetValue<bool>("FeatureFlags:SitemapEpisodes"))
            {
                var total = await EpisodeQuery().CountAsync(cancellationToken);
                pages = (int)Math.Ceiling(total / (double)EpisodesPerSitemap);
            }
            var root = new XElement(ns + "sitemapindex",
                new XElement(ns + "sitemap", new XElement(ns + "loc", SeoMetadata.BaseUrl + "/sitemap-main.xml")));
            for (var i = 1; i <= pages; i++)
                root.Add(new XElement(ns + "sitemap", new XElement(ns + "loc", $"{SeoMetadata.BaseUrl}/sitemap-episodes-{i}.xml")));
            var xml = new XDocument(new XDeclaration("1.0", "utf-8", "yes"), root).ToString();
            _cache.Set(IndexCacheKey, xml, TimeSpan.FromHours(1));
            return Content(xml, "application/xml");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Error generating sitemap index");
            return StatusCode(503, "Sitemap temporarily unavailable");
        }
    }

    /// <summary>Tập phim phát được (có link) của phim đang hoạt động: group theo (slug phim, slug tập) và lấy MIN(EpisodeId) làm URL canonical chuẩn.</summary>
    private sealed class EpisodeKey
    {
        public string MovieSlug { get; set; } = "";
        public string EpisodeSlug { get; set; } = "";
        public int MinEpisodeId { get; set; }
    }

    private IQueryable<EpisodeKey> EpisodeQuery() =>
        _context.Episodes.AsNoTracking()
            .Where(e => e.Movie.IsActive == true && e.Movie.Slug != null && e.Movie.Slug.Trim() != ""
                && e.Slug != null && e.Slug.Trim() != "" && e.LinkM3u8 != null && e.LinkM3u8.Trim() != "")
            .GroupBy(e => new { MovieSlug = e.Movie.Slug, EpisodeSlug = e.Slug })
            .Select(g => new EpisodeKey
            {
                MovieSlug = g.Key.MovieSlug,
                EpisodeSlug = g.Key.EpisodeSlug,
                MinEpisodeId = g.Min(e => e.EpisodeId)
            });

    [HttpGet("sitemap-episodes-{page:int}.xml")]
    [ResponseCache(Duration = 3600, Location = ResponseCacheLocation.Any)]
    public async Task<IActionResult> Episodes(int page, CancellationToken cancellationToken)
    {
        if (page < 1 || !_configuration.GetValue<bool>("FeatureFlags:SitemapEpisodes")) return NotFound();
        var key = $"sitemap_episodes_{page}";
        if (_cache.TryGetValue(key, out string? cachedPage) && cachedPage is not null)
            return Content(cachedPage, "application/xml");
        try
        {
            var rows = await EpisodeQuery()
                .OrderBy(x => x.MovieSlug).ThenBy(x => x.EpisodeSlug)
                .Skip((page - 1) * EpisodesPerSitemap).Take(EpisodesPerSitemap)
                .ToListAsync(cancellationToken);
            if (rows.Count == 0) return NotFound();
            XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";
            var urlset = new XElement(ns + "urlset");
            foreach (var row in rows)
            {
                var canonicalEpUrl = $"{SeoMetadata.BaseUrl}{SeoUrlHelper.EpisodeUrl(row.MovieSlug, row.EpisodeSlug, row.MinEpisodeId)}";
                urlset.Add(new XElement(ns + "url", new XElement(ns + "loc", canonicalEpUrl)));
            }
            var xml = new XDocument(new XDeclaration("1.0", "utf-8", "yes"), urlset).ToString();
            _cache.Set(key, xml, TimeSpan.FromHours(1));
            return Content(xml, "application/xml");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Error generating episode sitemap page {Page}", page);
            return StatusCode(503, "Sitemap temporarily unavailable");
        }
    }

    [HttpGet("sitemap-main.xml")]
    [ResponseCache(Duration = 3600, Location = ResponseCacheLocation.Any)]
    public async Task<IActionResult> Main(CancellationToken cancellationToken)
    {
        if (_cache.TryGetValue(CacheKey, out string? cached) && cached is not null)
            return Content(cached, "application/xml");

        // Only one request builds the sitemap after cache invalidation.
        await GenerationLock.WaitAsync(cancellationToken);
        try
        {
            if (_cache.TryGetValue(CacheKey, out cached) && cached is not null)
                return Content(cached, "application/xml");
            var xml = await GenerateSitemapXml(cancellationToken);
            _cache.Set(CacheKey, xml, TimeSpan.FromHours(1));
            return Content(xml, "application/xml");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Error generating sitemap");
            return StatusCode(503, "Sitemap temporarily unavailable");
        }
        finally
        {
            GenerationLock.Release();
        }
    }

    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    [HttpPost("api/sitemap/clear-cache")]
    public IActionResult ClearCache()
    {
        _cache.Remove(CacheKey);
        _cache.Remove(IndexCacheKey);
        return Ok(new { success = true });
    }

    private async Task<string> GenerateSitemapXml(CancellationToken cancellationToken)
    {
        XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";
        var sitemap = new XElement(ns + "urlset");
        var seen = new HashSet<string>(StringComparer.Ordinal);
        void Add(string path, DateTime? updated = null)
        {
            var url = SeoMetadata.BaseUrl + path;
            if (!seen.Add(url)) return;
            var element = new XElement(ns + "url", new XElement(ns + "loc", url));
            // Omit unknown modification dates instead of inventing a fresh timestamp.
            if (updated.HasValue && updated.Value <= DateTime.UtcNow.AddDays(1))
                element.Add(new XElement(ns + "lastmod", updated.Value.ToString("yyyy-MM-dd")));
            sitemap.Add(element);
        }

        foreach (var path in new[] { "/", "/trang-chu", "/the-loai/phim-le", "/the-loai/phim-bo",
            "/the-loai/hoat-hinh", "/the-loai/phim-moi-cap-nhat" }) Add(path);

        var movies = await _context.Movies.AsNoTracking()
            .Where(m => m.IsActive == true && m.Slug != null && m.Slug.Trim() != ""
                && (m.Episodes.Any(e => e.LinkM3u8 != null && e.LinkM3u8.Trim() != "")
                    || (m.TrailerUrl != null && m.TrailerUrl.Trim() != "")
                    || (m.Trailer != null && m.Trailer.Trim() != "")))
            .OrderByDescending(m => m.UpdatedAt ?? m.CreatedAt)
            .ThenBy(m => m.MovieId)
            .Select(m => new
            {
                m.MovieId, m.Slug, m.UpdatedAt, m.CreatedAt, m.TrailerUrl, m.Trailer,
                Sources = m.Episodes.Select(e => new { e.LinkM3u8, e.ServerName }).ToList(),
                Categories = m.Categories.Where(c => c.IsActive == true).Select(c => c.Slug).ToList(),
                Countries = m.Countries.Where(c => c.IsActive == true).Select(c => c.Slug).ToList()
            })
            .Take(MaxMovies)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

        var categories = new HashSet<string>(StringComparer.Ordinal);
        var countries = new HashSet<string>(StringComparer.Ordinal);
        var playable = 0;
        var trailers = 0;
        foreach (var movie in movies)
        {
            var hasPlayback = movie.Sources.Any(e => VSMovSource.IsPlayable(e.LinkM3u8, e.ServerName)) || VSMovSource.IsSupported(movie.TrailerUrl);
            var hasTrailer = SeoMetadata.IsTrailer(movie.Trailer) || SeoMetadata.IsTrailer(movie.TrailerUrl);
            if (!hasPlayback && !hasTrailer) continue;
            if (hasPlayback) playable++; else trailers++;
            Add(SeoUrlHelper.MovieUrl(movie.Slug, movie.MovieId), movie.UpdatedAt ?? movie.CreatedAt);
            foreach (var slug in movie.Categories.Where(s => !string.IsNullOrWhiteSpace(s))) categories.Add(slug.Trim());
            foreach (var slug in movie.Countries.Where(s => !string.IsNullOrWhiteSpace(s))) countries.Add(slug.Trim());
        }
        foreach (var slug in categories.Order(StringComparer.Ordinal)) Add("/the-loai/" + Uri.EscapeDataString(slug));
        foreach (var slug in countries.Order(StringComparer.Ordinal)) Add("/quoc-gia/" + Uri.EscapeDataString(slug));
        _logger.LogInformation("SEO sitemap: {Urls} URLs, {Playable} VSMov movies, {Trailers} trailer-only movies, {Excluded} excluded",
            seen.Count, playable, trailers, movies.Count - playable - trailers);
        return new XDocument(new XDeclaration("1.0", "utf-8", "yes"), sitemap).ToString();
    }
}

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
    private static readonly SemaphoreSlim GenerationLock = new(1, 1);
    private readonly MovieWebDbContext _context;
    private readonly IMemoryCache _cache;
    private readonly ILogger<SitemapController> _logger;

    public SitemapController(MovieWebDbContext context, IMemoryCache cache, ILogger<SitemapController> logger)
    {
        _context = context;
        _cache = cache;
        _logger = logger;
    }

    [HttpGet("sitemap.xml")]
    [ResponseCache(Duration = 3600, Location = ResponseCacheLocation.Any)]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
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
                m.Slug, m.UpdatedAt, m.CreatedAt, m.TrailerUrl, m.Trailer,
                Sources = m.Episodes.Select(e => e.LinkM3u8).ToList(),
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
            var hasPlayback = movie.Sources.Any(VSMovSource.IsSupported) || VSMovSource.IsSupported(movie.TrailerUrl);
            var hasTrailer = SeoMetadata.IsTrailer(movie.Trailer) || SeoMetadata.IsTrailer(movie.TrailerUrl);
            if (!hasPlayback && !hasTrailer) continue;
            if (hasPlayback) playable++; else trailers++;
            Add("/phim/" + Uri.EscapeDataString(movie.Slug.Trim()));
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

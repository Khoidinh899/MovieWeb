using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using MovieWeb.Data;
using System.Text.RegularExpressions;

namespace MovieWeb.Controllers.API
{
    [Route("api/movie")]
    [ApiController]
    public class ApiMovieController : ControllerBase
    {
        private readonly MovieWebDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly ILogger<ApiMovieController> _logger;

        // Cache settings
        private const int CacheMinutes = 30;
        private const string ImageDomain = "https://img.ophim.live/uploads/movies/";

        public ApiMovieController(
            MovieWebDbContext context,
            IMemoryCache cache,
            ILogger<ApiMovieController> logger)
        {
            _context = context;
            _cache = cache;
            _logger = logger;
        }

        [HttpGet("{slug}")]
        public async Task<IActionResult> GetMovieBySlug(string slug)
        {
            if (string.IsNullOrWhiteSpace(slug))
                throw new ArgumentException("Slug không được để trống");

            // Try cache first for performance
            var cacheKey = $"movie_detail_{slug}";
            if (_cache.TryGetValue(cacheKey, out object? cachedResponse))
            {
                return Ok(cachedResponse);
            }

            // Fetch movie with all relations
            var movie = await _context.Movies
                .Include(m => m.Actors)
                .Include(m => m.Categories)
                .Include(m => m.Directors)
                .Include(m => m.Countries)
                .Include(m => m.Episodes)
                .FirstOrDefaultAsync(m => m.Slug == slug && (m.IsActive ?? false));

            if (movie == null)
                throw new KeyNotFoundException($"Không tìm thấy phim với slug: {slug}");

            // Increment view count asynchronously (fire and forget) with scoped context
            var movieId = movie.MovieId;
            _ = Task.Run(async () =>
            {
                try
                {
                    // Create new scope for DbContext to avoid disposed context error
                    using var scope = HttpContext.RequestServices.CreateScope();
                    var scopedContext = scope.ServiceProvider.GetRequiredService<MovieWebDbContext>();
                    
                    var movieToUpdate = await scopedContext.Movies.FindAsync(movieId);
                    if (movieToUpdate != null)
                    {
                        movieToUpdate.ViewCount = (movieToUpdate.ViewCount ?? 0) + 1;
                        await scopedContext.SaveChangesAsync();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to increment view count for movie {MovieId}", movieId);
                }
            });

            // Get related movies (need to await before building response)
            var relatedMovies = await GetRelatedMovies(movie.MovieId, 
                movie.Categories.Select(c => c.CategoryId).ToList());

            // Build response with normalized URLs (In-Memory processing)
            var response = new
            {
                success = true,
                data = new
                {
                    // Basic Information
                    movieId = movie.MovieId,
                    slug = movie.Slug,
                    name = movie.Name,
                    originalName = movie.OriginalName,
                    content = movie.Content,
                    description = movie.Description,
                    type = movie.Type,
                    status = movie.Status,
                    
                    // Media URLs - Normalized in-memory (không gọi trong LINQ query)
                    thumbUrl = NormalizeImageUrl(movie.ThumbUrl),
                    posterUrl = NormalizeImageUrl(movie.PosterUrl),
                    poster = NormalizeImageUrl(movie.Poster),
                    backdrop = NormalizeImageUrl(movie.Backdrop),
                    trailerUrl = movie.TrailerUrl,
                    trailer = movie.Trailer,
                    
                    // Movie Details
                    time = movie.Time,
                    episodeCurrent = movie.EpisodeCurrent,
                    episodeTotal = movie.EpisodeTotal,
                    quality = movie.Quality,
                    language = movie.Language,
                    year = movie.Year,
                    
                    // Statistics
                    viewCount = movie.ViewCount ?? 0,
                    rating = movie.Rating ?? 0,
                    ratingCount = movie.RatingCount ?? 0,
                    
                    // Flags
                    isBanner = movie.IsBanner ?? false,
                    isCopyright = movie.IsCopyright ?? false,
                    isActive = movie.IsActive ?? false,
                    
                    // Timestamps
                    createdAt = movie.CreatedAt,
                    updatedAt = movie.UpdatedAt,
                    
                    // Categories with full info for filtering
                    categories = movie.Categories.Select(c => new
                    {
                        categoryId = c.CategoryId,
                        name = c.Name,
                        slug = c.Slug
                    }).OrderBy(c => c.name).ToList(),
                    
                    // Countries with full info for filtering
                    countries = movie.Countries.Select(c => new
                    {
                        countryId = c.CountryId,
                        name = c.Name,
                        slug = c.Slug
                    }).OrderBy(c => c.name).ToList(),
                    
                    // Actors list
                    actors = movie.Actors.Select(a => new
                    {
                        actorId = a.ActorId,
                        name = a.Name
                    }).OrderBy(a => a.name).ToList(),
                    
                    // Directors list
                    directors = movie.Directors.Select(d => new
                    {
                        directorId = d.DirectorId,
                        name = d.Name
                    }).OrderBy(d => d.name).ToList(),
                    
                    // Episodes grouped by server
                    episodes = GroupEpisodesByServer(movie.Episodes.ToList()),
                    
                    // Related movies (already processed with normalized URLs)
                    relatedMovies = relatedMovies
                }
            };

            // Cache for 30 minutes
            _cache.Set(cacheKey, response, TimeSpan.FromMinutes(CacheMinutes));

            return Ok(response);
        }

        private static string? NormalizeImageUrl(string? url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return null;

            // If already absolute URL, return as-is
            if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                return url;

            // If relative path, prepend image domain
            return ImageDomain + url.TrimStart('/');
        }

        /// <summary>
        /// Group episodes by server name with normalization
        /// </summary>
        private object GroupEpisodesByServer(List<Models.Entities.Episode> episodes)
        {
            if (episodes == null || !episodes.Any())
                return new List<object>();

            // Normalize server name
            string NormalizeServerKey(string? serverName)
            {
                serverName ??= "Khác";
                serverName = serverName.Trim();
                serverName = Regex.Replace(serverName, @"\s+", " ");
                return serverName.ToLowerInvariant();
            }

            string DisplayServerName(string? serverName)
            {
                serverName ??= "Khác";
                serverName = serverName.Trim();
                return Regex.Replace(serverName, @"\s+", " ");
            }

            // Group by normalized server name
            var grouped = episodes
                .GroupBy(e => NormalizeServerKey(e.ServerName))
                .Select(g => new
                {
                    serverName = NormalizeServerKey(g.Key),
                    displayName = DisplayServerName(g.First().ServerName),
                    episodes = g.OrderBy(e => e.EpisodeName).Select(e => new
                    {
                        episodeId = e.EpisodeId,
                        episodeName = e.EpisodeName,
                        slug = e.Slug,
                        linkM3u8 = e.LinkM3u8
                    }).ToList()
                })
                .OrderBy(s => s.serverName)
                .ToList();

            return grouped;
        }

        /// <summary>
        /// Get related movies based on categories
        /// </summary>
        private async Task<List<object>> GetRelatedMovies(int currentMovieId, List<int> categoryIds)
        {
            if (categoryIds == null || !categoryIds.Any())
                return new List<object>();

            // First fetch data from DB without normalization (avoid EF projection error)
            var relatedMoviesData = await _context.Movies
                .Where(m => (m.IsActive ?? false)
                            && m.MovieId != currentMovieId
                            && m.Categories.Any(c => categoryIds.Contains(c.CategoryId)))
                .OrderByDescending(m => m.ViewCount ?? 0)
                .Take(8)
                .Select(m => new
                {
                    m.MovieId,
                    m.Slug,
                    m.Name,
                    m.OriginalName,
                    m.ThumbUrl,
                    m.PosterUrl,
                    m.Year,
                    m.Quality,
                    m.Language,
                    m.EpisodeCurrent,
                    m.EpisodeTotal,
                    m.Rating,
                    m.ViewCount
                })
                .ToListAsync();

            // Then normalize URLs in-memory (no EF Core issue)
            var relatedMovies = relatedMoviesData.Select(m => new
            {
                movieId = m.MovieId,
                slug = m.Slug,
                name = m.Name,
                originalName = m.OriginalName,
                thumbUrl = NormalizeImageUrl(m.ThumbUrl),
                posterUrl = NormalizeImageUrl(m.PosterUrl),
                year = m.Year,
                quality = m.Quality,
                language = m.Language,
                episodeCurrent = m.EpisodeCurrent,
                episodeTotal = m.EpisodeTotal,
                rating = m.Rating ?? 0,
                viewCount = m.ViewCount ?? 0
            }).ToList();

            return relatedMovies.Cast<object>().ToList();
        }

        
        [HttpGet("search")]
        public async Task<IActionResult> SearchMovies(
            [FromQuery] string? query,
            [FromQuery] string? type,
            [FromQuery] string? countries,
            [FromQuery] string? categories,
            [FromQuery] string? language,
            [FromQuery] string? years,
            [FromQuery] string? sortBy,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            try
            {
                var dbQuery = _context.Movies
                    .Include(m => m.Categories)
                    .Include(m => m.Countries)
                    .Include(m => m.Actors)
                    .Include(m => m.Directors)
                    .Where(m => (m.IsActive ?? false));

                // 1. Keyword search (Name, OriginalName, Slug, Actor Name)
                if (!string.IsNullOrWhiteSpace(query))
                {
                    string searchKeyword = query.Trim().ToLower();
                    dbQuery = dbQuery.Where(m =>
                        m.Name.ToLower().Contains(searchKeyword) ||
                        (m.OriginalName != null && m.OriginalName.ToLower().Contains(searchKeyword)) ||
                        m.Slug.ToLower().Contains(searchKeyword) ||
                        m.Actors.Any(a => a.Name.ToLower().Contains(searchKeyword))
                    );
                }

                // 2. Type filter
                if (!string.IsNullOrWhiteSpace(type))
                {
                    dbQuery = dbQuery.Where(m => m.Type == type);
                }

                // 3. Country filter (multiple - OR)
                if (!string.IsNullOrWhiteSpace(countries))
                {
                    var countryList = countries.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
                    if (countryList.Any())
                    {
                        dbQuery = dbQuery.Where(m => m.Countries.Any(c => countryList.Contains(c.Slug)));
                    }
                }

                // 4. Category filter (multiple - AND)
                if (!string.IsNullOrWhiteSpace(categories))
                {
                    var categoryList = categories.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
                    if (categoryList.Any())
                    {
                        dbQuery = dbQuery.Where(m => categoryList.All(catSlug => m.Categories.Any(c => c.Slug == catSlug)));
                    }
                }

                // 5. Language filter
                if (!string.IsNullOrWhiteSpace(language))
                {
                    dbQuery = dbQuery.Where(m => m.Language == language);
                }

                // 6. Year filter (multiple - OR)
                if (!string.IsNullOrWhiteSpace(years))
                {
                    var yearList = new List<int>();
                    foreach (var yearStr in years.Split(',', StringSplitOptions.RemoveEmptyEntries))
                    {
                        if (int.TryParse(yearStr, out int year))
                        {
                            yearList.Add(year);
                        }
                    }
                    if (yearList.Any())
                    {
                        dbQuery = dbQuery.Where(m => m.Year.HasValue && yearList.Contains(m.Year.Value));
                    }
                }

                // 7. Sort
                dbQuery = (sortBy?.ToLower()) switch
                {
                    "newest" => dbQuery.OrderByDescending(m => m.Year).ThenByDescending(m => m.CreatedAt),
                    "rating" => dbQuery.OrderByDescending(m => m.Rating ?? 0),
                    "views" => dbQuery.OrderByDescending(m => m.ViewCount ?? 0),
                    _ => dbQuery.OrderByDescending(m => m.UpdatedAt ?? m.CreatedAt)
                };

                int totalMovies = await dbQuery.CountAsync();
                int totalPages = (int)Math.Ceiling(totalMovies / (double)pageSize);

                var movies = await dbQuery
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(m => new
                    {
                        movieId = m.MovieId,
                        slug = m.Slug,
                        name = m.Name,
                        originalName = m.OriginalName,
                        thumbUrl = m.ThumbUrl,
                        posterUrl = m.PosterUrl,
                        year = m.Year,
                        quality = m.Quality,
                        language = m.Language,
                        rating = m.Rating ?? 0,
                        viewCount = m.ViewCount ?? 0,
                        categories = m.Categories.Select(c => new { categoryId = c.CategoryId, name = c.Name, slug = c.Slug }).ToList()
                    })
                    .ToListAsync();

                // Normalize image URLs in-memory
                var resultMovies = movies.Select(m => new
                {
                    m.movieId,
                    m.slug,
                    m.name,
                    m.originalName,
                    thumbUrl = NormalizeImageUrl(m.thumbUrl),
                    posterUrl = NormalizeImageUrl(m.posterUrl),
                    m.year,
                    m.quality,
                    m.language,
                    m.rating,
                    m.viewCount,
                    m.categories
                }).ToList();

                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        movies = resultMovies,
                        pagination = new
                        {
                            currentPage = page,
                            totalPages = totalPages,
                            pageSize = pageSize,
                            totalCount = totalMovies
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Lỗi Server", error = ex.Message });
            }
        }

        [HttpDelete("{slug}/cache")]
        public IActionResult ClearCache(string slug)
        {
            var cacheKey = $"movie_detail_{slug}";
            _cache.Remove(cacheKey);
            
            return Ok(new
            {
                success = true,
                message = $"Cache cleared for movie: {slug}"
            });
        }

    
    }
}

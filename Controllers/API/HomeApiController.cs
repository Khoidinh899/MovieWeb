using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using MovieWeb.Data;
using MovieWeb.Models.DTOs;
using MovieWeb.Models.Entities;

namespace MovieWeb.Controllers.Api
{
    [Route("api/[controller]")]
    [ApiController]
    public class HomeApiController : ControllerBase
    {
        private readonly MovieWebDbContext _context;
        private readonly IMemoryCache _cache;
        
        // Domain ảnh của OPhim
        private const string ImageDomain = "https://img.ophim.live/uploads/movies/";

        public HomeApiController(MovieWebDbContext context, IMemoryCache cache)
        {
            _context = context;
            _cache = cache;
        }

        [HttpGet]
        public async Task<IActionResult> GetHomeData()
        {
            try
            {
                var response = new HomeApiDto();

                // 1. Lấy Banner (Giữ logic cũ của ông: IsBanner hoặc ViewCount cao)
                var cacheKeyBanner = "banner_movies_entities";
                if (!_cache.TryGetValue(cacheKeyBanner, out List<Movie>? bannerMovies))
                {
                    bannerMovies = await _context.Movies
                        .Where(m => m.IsActive == true && (m.IsBanner == true))
                        .OrderByDescending(m => m.UpdatedAt)
                        .Take(5)
                        .ToListAsync();

                    if (bannerMovies == null || !bannerMovies.Any())
                    {
                        bannerMovies = await _context.Movies
                            .Where(m => m.IsActive == true)
                            .OrderByDescending(m => m.ViewCount)
                            .Take(5)
                            .ToListAsync();
                    }
                    // Lưu cache, đảm bảo không null
                    _cache.Set(cacheKeyBanner, bannerMovies ?? new List<Movie>(), TimeSpan.FromMinutes(30));
                }
                
                // Map sang DTO
                response.Banners = (bannerMovies ?? new List<Movie>()).Select(m => MapToDto(m)).ToList();

                // 2. Phim Mới
                var latestMovies = await GetCachedMovies("latest_movies_entities", 
                    q => q.OrderByDescending(m => m.UpdatedAt).Take(12));
                response.Sections.Add(new MovieSectionDto { Title = "Phim Mới Cập Nhật", Movies = latestMovies.Select(m => MapToDto(m)).ToList() });

                // 3. Phim Lẻ
                var singleMovies = await GetCachedMovies("single_movies_entities", 
                    q => q.Where(m => m.Type == "single").OrderByDescending(m => m.UpdatedAt).Take(12));
                response.Sections.Add(new MovieSectionDto { Title = "Phim Lẻ Mới", Movies = singleMovies.Select(m => MapToDto(m)).ToList() });

                // 4. Phim Bộ
                var seriesMovies = await GetCachedMovies("series_movies_entities", 
                    q => q.Where(m => m.Type == "series").OrderByDescending(m => m.UpdatedAt).Take(12));
                response.Sections.Add(new MovieSectionDto { Title = "Phim Bộ Hot", Movies = seriesMovies.Select(m => MapToDto(m)).ToList() });

                // 5. Hoạt Hình
                var cartoonMovies = await GetCachedMovies("hoathinh_movies_entities", 
                    q => q.Where(m => m.Type == "hoathinh").OrderByDescending(m => m.UpdatedAt).Take(12));
                response.Sections.Add(new MovieSectionDto { Title = "Hoạt Hình", Movies = cartoonMovies.Select(m => MapToDto(m)).ToList() });

                return Ok(response);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi Server", error = ex.Message });
            }
        }

        [HttpGet("filters")]
        public async Task<IActionResult> GetFilters()
        {
            try
            {
                var categories = await _context.Categories
                    .Where(c => c.IsActive == true)
                    .OrderBy(c => c.Name)
                    .Select(c => new { categoryId = c.CategoryId, name = c.Name, slug = c.Slug })
                    .ToListAsync();

                var countries = await _context.Countries
                    .Where(c => c.IsActive == true)
                    .OrderBy(c => c.Name)
                    .Select(c => new { countryId = c.CountryId, name = c.Name, slug = c.Slug })
                    .ToListAsync();

                return Ok(new { success = true, categories, countries });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Lỗi Server", error = ex.Message });
            }
        }

        // Hàm lấy Cache (đã xử lý Null Safety cho CS8600, CS8604)
        private async Task<List<Movie>> GetCachedMovies(string key, Func<IQueryable<Movie>, IQueryable<Movie>> queryBuilder)
        {
            if (!_cache.TryGetValue(key, out List<Movie>? movies))
            {
                var query = _context.Movies.Where(m => m.IsActive == true);
                query = queryBuilder(query);
                movies = await query.ToListAsync();
                _cache.Set(key, movies, TimeSpan.FromSeconds(30));
            }
            return movies ?? new List<Movie>();
        }

        // Hàm Map DTO (Đã sửa các tên cột bị sai: Id -> MovieId, VoteAverage -> Rating...)
        private MovieDto MapToDto(Movie m)
        {
            return new MovieDto
            {
                MovieId = m.MovieId,       // ✅ Đã sửa: dùng MovieId thay vì Id
                Name = m.Name ?? "",
                OriginalName = m.OriginalName ?? "", // ✅ Đã sửa: dùng OriginalName thay vì OriginName
                Slug = m.Slug ?? "",
                Content = m.Content,
                Type = m.Type,
                Status = m.Status,
                
                // Xử lý ảnh:
                // Entity.ThumbUrl -> DTO.ThumbUrl (Ảnh dọc)
                ThumbUrl = string.IsNullOrEmpty(m.ThumbUrl) ? null : 
                           (m.ThumbUrl.StartsWith("http") ? m.ThumbUrl : ImageDomain + m.ThumbUrl),
                
                // Entity.PosterUrl -> DTO.PosterUrl (Ảnh ngang/Banner)
                PosterUrl = string.IsNullOrEmpty(m.PosterUrl) ? null : 
                             (m.PosterUrl.StartsWith("http") ? m.PosterUrl : ImageDomain + m.PosterUrl),
                
                TrailerUrl = m.TrailerUrl,
                Time = m.Time,
                EpisodeCurrent = m.EpisodeCurrent,
                EpisodeTotal = m.EpisodeTotal,
                Quality = m.Quality,
                Language = m.Language,
                Year = m.Year,
                ViewCount = m.ViewCount ?? 0,
                
                // ✅ Đã sửa: dùng m.Rating thay vì VoteAverage
                Rating = m.Rating ?? 0m, 
                
                // ✅ Đã sửa: dùng m.RatingCount thay vì VoteCount
                RatingCount = m.RatingCount ?? 0,
                
                IsRecommended = m.IsRecommended ?? false,
                IsBanner = m.IsBanner,
                UpdatedAt = m.UpdatedAt ?? DateTime.Now,

                // Khởi tạo list rỗng để tránh null khi Flutter đọc
                Categories = new List<CategoryDto>(),
                Countries = new List<CountryDto>(),
                Actors = new List<ActorDto>(),
                Directors = new List<DirectorDto>()
            };
        }
    }
}
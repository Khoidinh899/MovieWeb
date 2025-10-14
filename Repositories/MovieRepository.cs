using Microsoft.EntityFrameworkCore;
using MovieWeb.Data;
using MovieWeb.Models.Entities;
using MovieWeb.Models.DTOs;

namespace MovieWeb.Repositories
{
    public class MovieRepository : IMovieRepository
    {
        private readonly MovieWebDbContext _context;

        public MovieRepository(MovieWebDbContext context)
        {
            _context = context;
        }

        // ✅ Lấy phim mới nhất
        public async Task<PagedResult<Movie>> GetLatestMoviesAsync(int page = 1, int pageSize = 24)
        {
            var query = _context.Movies
                .Where(m => m.IsActive == true)
                .OrderByDescending(m => m.UpdatedAt)
                .AsNoTracking();

            var totalCount = await query.CountAsync();
            var movies = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new PagedResult<Movie>
            {
                Items = movies,
                CurrentPage = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = (int)Math.Ceiling((double)totalCount / pageSize)
            };
        }

        // ✅ Lấy phim theo thể loại Type
        public async Task<PagedResult<Movie>> GetMoviesByTypeAsync(string type, int page = 1, int pageSize = 24)
        {
            var query = _context.Movies
                .Where(m => m.IsActive == true && m.Type == type)
                .OrderByDescending(m => m.UpdatedAt)
                .AsNoTracking();

            var totalCount = await query.CountAsync();
            var movies = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new PagedResult<Movie>
            {
                Items = movies,
                CurrentPage = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = (int)Math.Ceiling((double)totalCount / pageSize)
            };
        }

        // ✅ Lấy phim theo Category
        public async Task<PagedResult<Movie>> GetMoviesByCategoryAsync(string categorySlug, int page = 1, int pageSize = 24)
        {
            var query = _context.Movies
                .Where(m => m.IsActive == true && m.Categories.Any(c => c.Slug == categorySlug))
                .OrderByDescending(m => m.UpdatedAt)
                .Include(m => m.Categories)
                .AsNoTracking();

            var totalCount = await query.CountAsync();
            var movies = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new PagedResult<Movie>
            {
                Items = movies,
                CurrentPage = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = (int)Math.Ceiling((double)totalCount / pageSize)
            };
        }

        // ✅ Search phim
        public async Task<PagedResult<Movie>> SearchMoviesAsync(string keyword, int page = 1, int pageSize = 24)
        {
            var query = _context.Movies
                .Where(m => m.IsActive == true &&
                    (m.Name.Contains(keyword) ||
                     (m.OriginalName != null && m.OriginalName.Contains(keyword)) ||
                     (m.Content != null && m.Content.Contains(keyword))))
                .OrderByDescending(m => m.ViewCount)
                .ThenByDescending(m => m.UpdatedAt)
                .AsNoTracking();

            var totalCount = await query.CountAsync();
            var movies = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new PagedResult<Movie>
            {
                Items = movies,
                CurrentPage = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = (int)Math.Ceiling((double)totalCount / pageSize)
            };
        }

        // ✅ Lấy phim hot
        public async Task<List<Movie>> GetHotMoviesAsync(int take = 6)
        {
            return await _context.Movies
                .Where(m => m.IsActive == true)
                .OrderByDescending(m => m.ViewCount)
                .ThenByDescending(m => m.Rating)
                .Take(take)
                .AsNoTracking()
                .ToListAsync();
        }

        // ✅ Lấy phim được đề xuất
        public async Task<List<Movie>> GetRecommendedMoviesAsync(int take = 12)
        {
            return await _context.Movies
                .Where(m => m.IsActive == true && m.IsRecommended == true)
                .OrderByDescending(m => m.UpdatedAt)
                .Take(take)
                .AsNoTracking()
                .ToListAsync();
        }

        // ✅ Lấy phim mới cập nhật
        public async Task<List<Movie>> GetRecentlyUpdatedAsync(int take = 12)
        {
            return await _context.Movies
                .Where(m => m.IsActive == true)
                .OrderByDescending(m => m.UpdatedAt)
                .Take(take)
                .AsNoTracking()
                .ToListAsync();
        }

        // ✅ Lấy phim theo Slug
        public async Task<Movie?> GetMovieBySlugAsync(string slug)
        {
            return await _context.Movies
                .Include(m => m.Categories)
                .Include(m => m.Countries)
                .Include(m => m.Actors)
                .Include(m => m.Directors)
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.Slug == slug && m.IsActive == true);
        }

        // ✅ Lấy phim theo ID
        public async Task<Movie?> GetMovieByIdAsync(int movieId)
        {
            return await _context.Movies
                .Include(m => m.Categories)
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.MovieId == movieId && m.IsActive == true);
        }

        // ✅ Tổng số phim đang active
        public async Task<int> GetTotalMoviesCountAsync()
        {
            return await _context.Movies.CountAsync(m => m.IsActive == true);
        }

        // ✅ Top phim xem nhiều
        public async Task<List<Movie>> GetTopViewedMoviesAsync(int take = 10)
        {
            return await _context.Movies
                .Where(m => m.IsActive == true)
                .OrderByDescending(m => m.ViewCount)
                .Take(take)
                .AsNoTracking()
                .ToListAsync();
        }

        // ✅ Top phim đánh giá cao
        public async Task<List<Movie>> GetTopRatedMoviesAsync(int take = 10)
        {
            return await _context.Movies
                .Where(m => m.IsActive == true && m.RatingCount > 0)
                .OrderByDescending(m => m.Rating)
                .Take(take)
                .AsNoTracking()
                .ToListAsync();
        }

        // ✅ Cập nhật lượt xem
        public async Task UpdateViewCountAsync(int movieId)
        {
            var movie = await _context.Movies.FindAsync(movieId);
            if (movie != null && movie.IsActive == true)
            {
                movie.ViewCount = (movie.ViewCount ?? 0) + 1;
                movie.UpdatedAt = DateTime.Now;
                await _context.SaveChangesAsync();
            }
        }

        // ✅ Cập nhật rating
        public async Task UpdateRatingAsync(int movieId)
        {
            var movie = await _context.Movies
                .Include(m => m.Ratings)
                .FirstOrDefaultAsync(m => m.MovieId == movieId && m.IsActive == true);

            if (movie != null && movie.Ratings.Any())
            {
                movie.Rating = (decimal)movie.Ratings.Average(r => r.Rating1 ?? 0);
                movie.RatingCount = movie.Ratings.Count;
                movie.UpdatedAt = DateTime.Now;
                await _context.SaveChangesAsync();
            }
        }

        // ✅ Lấy danh sách categories
        public async Task<List<Category>> GetCategoriesAsync()
        {
            return await _context.Categories
                .Where(c => c.IsActive == true)
                .OrderBy(c => c.Name)
                .AsNoTracking()
                .ToListAsync();
        }

        // ✅ Lấy danh sách countries
        public async Task<List<Country>> GetCountriesAsync()
        {
            return await _context.Countries
                .Where(c => c.IsActive == true)
                .OrderBy(c => c.Name)
                .AsNoTracking()
                .ToListAsync();
        }

        // ✅ Lấy category theo slug
        public async Task<Category?> GetCategoryBySlugAsync(string slug)
        {
            return await _context.Categories
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Slug == slug && c.IsActive == true);
        }
        // ✅ Lấy phim được đánh dấu làm banner
        public async Task<List<Movie>> GetBannerMoviesAsync(int take = 5)
        {
            return await _context.Movies
                .Where(m => m.IsActive == true && m.IsBanner == true)
                .OrderByDescending(m => m.UpdatedAt) // Phim mới cập nhật lên trước
                .ThenByDescending(m => m.ViewCount)  // Sau đó theo lượt xem
                .Take(take)
                .AsNoTracking()
                .ToListAsync();
        }
    }
}

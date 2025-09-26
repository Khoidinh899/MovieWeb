using Microsoft.EntityFrameworkCore;
using MovieWeb.Data;
using MovieWeb.Models.Entities;
using MovieWeb.Models.DTOs;

namespace MovieWeb.Repositories
{
    public class MovieRepository : IMovieRepository
    {
        private readonly MovieWebDbContext _context; // Sửa từ AppDbContext thành MovieWebDbContext

        public MovieRepository(MovieWebDbContext context) // Sửa parameter type
        {
            _context = context;
        }

        public async Task<PagedResult<Movie>> GetLatestMoviesAsync(int page = 1, int pageSize = 24)
        {
            var query = _context.Movies
                .Where(m => m.IsActive == true) // Sửa nullable comparison
                .OrderByDescending(m => m.UpdatedAt)
                .AsQueryable();

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

        public async Task<PagedResult<Movie>> GetMoviesByTypeAsync(string type, int page = 1, int pageSize = 24)
        {
            var query = _context.Movies
                .Where(m => m.IsActive == true && m.Type == type)
                .OrderByDescending(m => m.UpdatedAt)
                .AsQueryable();

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

        public async Task<PagedResult<Movie>> GetMoviesByCategoryAsync(string categorySlug, int page = 1, int pageSize = 24)
        {
            var query = _context.Movies
                .Where(m => m.IsActive == true && m.Categories.Any(c => c.Slug == categorySlug)) // Sửa relationship
                .OrderByDescending(m => m.UpdatedAt)
                .AsQueryable();

            var totalCount = await query.CountAsync();
            var movies = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Include(m => m.Categories)
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

        public async Task<PagedResult<Movie>> SearchMoviesAsync(string keyword, int page = 1, int pageSize = 24)
        {
            var query = _context.Movies
                .Where(m => m.IsActive == true && 
                    (m.Name.Contains(keyword) || 
                     (m.OriginalName != null && m.OriginalName.Contains(keyword)) ||
                     (m.Content != null && m.Content.Contains(keyword)))) // Sửa null check
                .OrderByDescending(m => m.ViewCount)
                .ThenByDescending(m => m.UpdatedAt)
                .AsQueryable();

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

        public async Task<List<Movie>> GetHotMoviesAsync(int take = 6)
        {
            return await _context.Movies
                .Where(m => m.IsActive == true)
                .OrderByDescending(m => m.ViewCount)
                .ThenByDescending(m => m.Rating)
                .Take(take)
                .ToListAsync();
        }

        public async Task<List<Movie>> GetRecommendedMoviesAsync(int take = 12)
        {
            return await _context.Movies
                .Where(m => m.IsActive == true && m.IsRecommended == true)
                .OrderByDescending(m => m.UpdatedAt)
                .Take(take)
                .ToListAsync();
        }

        public async Task<List<Movie>> GetRecentlyUpdatedAsync(int take = 12)
        {
            return await _context.Movies
                .Where(m => m.IsActive == true)
                .OrderByDescending(m => m.UpdatedAt)
                .Take(take)
                .ToListAsync();
        }

        public async Task<Movie?> GetMovieBySlugAsync(string slug)
        {
            return await _context.Movies
                .Include(m => m.Categories)
                .Include(m => m.Countries)
                .Include(m => m.Actors)
                .Include(m => m.Directors)
                .FirstOrDefaultAsync(m => m.Slug == slug && m.IsActive == true);
        }

        public async Task<Movie?> GetMovieByIdAsync(int movieId)
        {
            return await _context.Movies
                .Include(m => m.Categories)
                .FirstOrDefaultAsync(m => m.MovieId == movieId && m.IsActive == true);
        }

        public async Task<int> GetTotalMoviesCountAsync()
        {
            return await _context.Movies.CountAsync(m => m.IsActive == true);
        }

        public async Task<List<Movie>> GetTopViewedMoviesAsync(int take = 10)
        {
            return await _context.Movies
                .Where(m => m.IsActive == true)
                .OrderByDescending(m => m.ViewCount)
                .Take(take)
                .ToListAsync();
        }

        public async Task<List<Movie>> GetTopRatedMoviesAsync(int take = 10)
        {
            return await _context.Movies
                .Where(m => m.IsActive == true && m.RatingCount > 0)
                .OrderByDescending(m => m.Rating)
                .Take(take)
                .ToListAsync();
        }

        public async Task UpdateViewCountAsync(int movieId)
        {
            var movie = await _context.Movies.FindAsync(movieId);
            if (movie != null)
            {
                movie.ViewCount = (movie.ViewCount ?? 0) + 1; // Sửa nullable handling
                movie.UpdatedAt = DateTime.Now;
                await _context.SaveChangesAsync();
            }
        }

        public async Task UpdateRatingAsync(int movieId)
        {
            var movie = await _context.Movies
                .Include(m => m.Ratings)
                .FirstOrDefaultAsync(m => m.MovieId == movieId);
                
            if (movie != null && movie.Ratings.Any())
            {
                movie.Rating = (decimal)movie.Ratings.Average(r => r.Rating1 ?? 0); // Sửa property name và nullable
                movie.RatingCount = movie.Ratings.Count;
                movie.UpdatedAt = DateTime.Now;
                await _context.SaveChangesAsync();
            }
        }

        public async Task<List<Category>> GetCategoriesAsync()
        {
            return await _context.Categories
                .Where(c => c.IsActive == true)
                .OrderBy(c => c.Name)
                .ToListAsync();
        }

        public async Task<List<Country>> GetCountriesAsync()
        {
            return await _context.Countries
                .Where(c => c.IsActive == true)
                .OrderBy(c => c.Name)
                .ToListAsync();
        }

        public async Task<Category?> GetCategoryBySlugAsync(string slug)
        {
            return await _context.Categories
                .FirstOrDefaultAsync(c => c.Slug == slug && c.IsActive == true);
        }
    }
}
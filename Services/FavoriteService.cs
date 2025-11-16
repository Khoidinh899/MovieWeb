using Microsoft.EntityFrameworkCore;
using MovieWeb.Data;
using MovieWeb.Models.DTOs;
using MovieWeb.Models.Entities;
using MovieWeb.Services.Interfaces;

namespace MovieWeb.Services
{
    public class FavoriteService : IFavoriteService
    {
        private readonly MovieWebDbContext _context;
        private readonly ILogger<FavoriteService> _logger;

        public FavoriteService(MovieWebDbContext context, ILogger<FavoriteService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<FavoriteListDto> GetUserFavoritesAsync(int userId, int page, int pageSize)
        {
            try
            {
                var query = _context.Favorites
                    .Include(f => f.Movie)
                    .Where(f => f.UserId == userId)
                    .OrderByDescending(f => f.CreatedAt);

                var totalCount = await query.CountAsync();
                var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

                var favorites = await query
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(f => new FavoriteMovieDto
                    {
                        FavoriteId = f.FavoriteId,
                        MovieId = f.MovieId,
                        Slug = f.Movie.Slug,
                        Name = f.Movie.Name,
                        OriginalName = f.Movie.OriginalName,
                        PosterUrl = f.Movie.PosterUrl,
                        Type = f.Movie.Type,
                        Status = f.Movie.Status,
                        EpisodeCurrent = f.Movie.EpisodeCurrent,
                        EpisodeTotal = f.Movie.EpisodeTotal,
                        Quality = f.Movie.Quality,
                        Year = f.Movie.Year,
                        CreatedAt = f.CreatedAt ?? DateTime.Now,
                        Rating = f.Movie.Rating ?? 0,
                        ViewCount = f.Movie.ViewCount ?? 0
                    })
                    .ToListAsync();

                return new FavoriteListDto
                {
                    Movies = favorites,
                    TotalCount = totalCount,
                    CurrentPage = page,
                    PageSize = pageSize,
                    TotalPages = totalPages
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user favorites");
                throw;
            }
        }

        public async Task<bool> AddFavoriteAsync(int userId, int movieId)
        {
            try
            {
                // Kiểm tra phim có tồn tại không
                var movieExists = await _context.Movies.AnyAsync(m => m.MovieId == movieId);
                if (!movieExists)
                {
                    return false;
                }

                // Kiểm tra đã favorite chưa
                var exists = await _context.Favorites
                    .AnyAsync(f => f.UserId == userId && f.MovieId == movieId);

                if (exists)
                {
                    return true; // Đã favorite rồi
                }

                var favorite = new Favorite
                {
                    UserId = userId,
                    MovieId = movieId,
                    CreatedAt = DateTime.Now
                };

                _context.Favorites.Add(favorite);
                await _context.SaveChangesAsync();

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding favorite");
                return false;
            }
        }

        public async Task<bool> RemoveFavoriteAsync(int userId, int movieId)
        {
            try
            {
                var favorite = await _context.Favorites
                    .FirstOrDefaultAsync(f => f.UserId == userId && f.MovieId == movieId);

                if (favorite == null)
                {
                    return false;
                }

                _context.Favorites.Remove(favorite);
                await _context.SaveChangesAsync();

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing favorite");
                return false;
            }
        }

        public async Task<CheckFavoriteDto> CheckFavoriteAsync(int userId, int movieId)
        {
            try
            {
                var favorite = await _context.Favorites
                    .FirstOrDefaultAsync(f => f.UserId == userId && f.MovieId == movieId);

                return new CheckFavoriteDto
                {
                    IsFavorited = favorite != null,
                    FavoriteId = favorite?.FavoriteId
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking favorite");
                return new CheckFavoriteDto { IsFavorited = false };
            }
        }
    }
}
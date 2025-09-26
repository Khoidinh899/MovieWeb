using Microsoft.EntityFrameworkCore;
using MovieWeb.Data;
using MovieWeb.Services;
using ApiMovie = MovieWeb.Models.API.Movie; // Alias cho API model
using DbMovie = MovieWeb.Models.Entities.Movie; // Alias cho DB entity

namespace MovieWeb.Services
{
    public interface IMovieSyncService
    {
        Task SyncMovieFromApiToDbAsync(ApiMovie apiMovie);
        Task SyncMoviesFromApiToDbAsync(List<ApiMovie> apiMovies);
        Task<DbMovie> GetMovieFromDbAsync(string slug);
    }

    public class MovieSyncService : IMovieSyncService
    {
        private readonly MovieWebDbContext _context;
        private readonly IOPhimService _oPhimService;
        private readonly ILogger<MovieSyncService> _logger;

        public MovieSyncService(MovieWebDbContext context, IOPhimService oPhimService, ILogger<MovieSyncService> logger)
        {
            _context = context;
            _oPhimService = oPhimService;
            _logger = logger;
        }

        public async Task SyncMovieFromApiToDbAsync(ApiMovie apiMovie)
        {
            try
            {
                // Kiểm tra movie đã tồn tại chưa
                var existingMovie = await _context.Movies.FirstOrDefaultAsync(m => m.Slug == apiMovie.Slug);

                if (existingMovie == null)
                {
                    // Lấy chi tiết phim từ API
                    var movieDetail = await _oPhimService.GetMovieDetailAsync(apiMovie.Slug);
                    
                    // Tạo entity mới
                    var dbMovie = new DbMovie
                    {
                        ApiId = apiMovie.Id,
                        Slug = apiMovie.Slug,
                        Name = apiMovie.Name,
                        OriginalName = apiMovie.OriginName,
                        Content = movieDetail?.Content ?? apiMovie.Content,
                        Type = apiMovie.Type,
                        Status = apiMovie.Status,
                        PosterUrl = apiMovie.PosterUrl,
                        ThumbUrl = apiMovie.ThumbUrl,
                        TrailerUrl = apiMovie.TrailerUrl,
                        Time = apiMovie.Time,
                        EpisodeCurrent = apiMovie.EpisodeCurrent,
                        EpisodeTotal = apiMovie.EpisodeTotal,
                        Quality = apiMovie.Quality,
                        Language = apiMovie.Lang,
                        Year = apiMovie.Year,
                        ViewCount = apiMovie.View,
                        Rating = 0,
                        RatingCount = 0,
                        IsActive = true,
                        CreatedAt = DateTime.Now,
                        UpdatedAt = DateTime.Now
                    };

                    _context.Movies.Add(dbMovie);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation($"Synced movie to DB: {apiMovie.Name}");
                }
                else
                {
                    // Cập nhật thông tin nếu đã tồn tại
                    existingMovie.ViewCount = apiMovie.View;
                    existingMovie.UpdatedAt = DateTime.Now;
                    await _context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error syncing movie: {apiMovie.Name}");
            }
        }

        public async Task SyncMoviesFromApiToDbAsync(List<ApiMovie> apiMovies)
        {
            foreach (var movie in apiMovies)
            {
                await SyncMovieFromApiToDbAsync(movie);
            }
        }

        public async Task<DbMovie> GetMovieFromDbAsync(string slug)
        {
            return await _context.Movies.FirstOrDefaultAsync(m => m.Slug == slug);
        }
    }
}
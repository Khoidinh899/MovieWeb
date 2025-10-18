// File: Services/MovieSyncService.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MovieWeb.Data;
using MovieWeb.Models.API;
using MovieWeb.Models.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ApiMovie = MovieWeb.Models.API.Movie;
using DbMovie = MovieWeb.Models.Entities.Movie;
using ApiEpisode = MovieWeb.Models.API.Episode;
using DbEpisode = MovieWeb.Models.Entities.Episode;

namespace MovieWeb.Services
{
    public interface IMovieSyncService
    {
        Task SyncMoviesFromApiToDbAsync(List<ApiMovie> apiMovies);
        Task BackfillAllEpisodesAsync();
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

        // =================================================================
        // HÀM CHÍNH ĐỂ ĐỒNG BỘ PHIM MỚI (CHẠY TỰ ĐỘNG)
        // =================================================================
        public async Task SyncMoviesFromApiToDbAsync(List<ApiMovie> apiMovies)
        {
            int processedCount = 0;
            foreach (var apiMovie in apiMovies)
            {
                processedCount++;
                try
                {
                    var existingMovie = await _context.Movies.AsNoTracking().FirstOrDefaultAsync(m => m.Slug == apiMovie.Slug);

                    if (existingMovie == null)
                    {
                        var apiResponse = await _oPhimService.GetMovieDetailAsync(apiMovie.Slug);
                        if (apiResponse?.Item == null)
                        {
                            _logger.LogWarning($"Không thể lấy chi tiết API cho slug (khi thêm phim mới): {apiMovie.Slug}. Bỏ qua.");
                            continue;
                        }

                        var apiItem = apiResponse.Item;
                        var dbMovie = new DbMovie
                        {
                            ApiId = apiItem.Id,
                            Slug = apiItem.Slug,
                            Name = apiItem.Name,
                            OriginalName = apiItem.OriginName,
                            Type = apiItem.Type,
                            Status = apiItem.Status,
                            PosterUrl = apiItem.PosterUrl,
                            ThumbUrl = apiItem.ThumbUrl,
                            Time = apiItem.Time,
                            EpisodeCurrent = apiItem.EpisodeCurrent,
                            EpisodeTotal = apiItem.EpisodeTotal,
                            Quality = apiItem.Quality,
                            Language = apiItem.Lang,
                            Year = apiItem.Year,
                            ViewCount = apiItem.View,
                            IsActive = true,
                            CreatedAt = DateTime.Now,
                            UpdatedAt = DateTime.Now,
                            IsBanner = false,
                            Description = apiItem.Content,
                            Content = apiResponse.SeoOnPage?.DescriptionHead,

                            // === LOGIC CHUẨN ĐÂY ===
                            // 1. Cột "Trailer" sẽ lưu link YouTube
                            Trailer = apiItem.TrailerUrl,

                            // 2. Cột "TrailerUrl" sẽ được gán ở dưới,
                            //    nó sẽ là null cho phim bộ và có giá trị cho phim lẻ
                        };

                        // === LOGIC XỬ LÝ LINK XEM PHIM (M3U8) ===
                        if (apiItem.Type == "single")
                        {
                            // 3a. Nếu là phim lẻ, gán link M3U8 vào cột TrailerUrl
                            var playbackLink = apiItem.Episodes?.FirstOrDefault()?.ServerData?.FirstOrDefault()?.LinkM3u8;
                            dbMovie.TrailerUrl = playbackLink; // Gán link xem phim
                        }
                        else if (apiItem.Type == "series" || apiItem.Type == "hoathinh")
                        {
                            // 3b. Nếu là phim bộ/hoạt hình, thêm các tập vào bảng Episodes
                            if (apiItem.Episodes != null)
                            {
                                foreach (var server in apiItem.Episodes)
                                {
                                    foreach (var episodeData in server.ServerData)
                                    {
                                        dbMovie.Episodes.Add(new DbEpisode
                                        {
                                            ServerName = server.ServerName,
                                            EpisodeName = episodeData.Name,
                                            Slug = episodeData.Slug,
                                            LinkM3u8 = episodeData.LinkM3u8
                                        });
                                    }
                                }
                            }
                        }
                        // === KẾT THÚC SỬA LỖI ===

                        _context.Movies.Add(dbMovie);
                        _logger.LogInformation($"Chuẩn bị thêm phim mới: {dbMovie.Name}");
                    }
                    else
                    {
                        // Cập nhật tập mới nhất cho phim đã có
                        var movieToUpdate = await _context.Movies.FindAsync(existingMovie.MovieId);
                        if (movieToUpdate != null && movieToUpdate.EpisodeCurrent != apiMovie.EpisodeCurrent)
                        {
                            movieToUpdate.EpisodeCurrent = apiMovie.EpisodeCurrent;
                            movieToUpdate.UpdatedAt = DateTime.Now;
                        }
                    }

                    // Lưu theo lô
                    if (processedCount % 10 == 0 && _context.ChangeTracker.HasChanges())
                    {
                        await _context.SaveChangesAsync();
                        _logger.LogInformation("=======> Đã lưu 1 lô phim vào DB <=======");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Lỗi khi đồng bộ phim: {apiMovie.Name}");
                }
            }

            // Lưu lô cuối cùng
            if (_context.ChangeTracker.HasChanges())
            {
                await _context.SaveChangesAsync();
                _logger.LogInformation("Đã lưu lô phim cuối cùng vào DB.");
            }
        }

        // =================================================================
        // HÀM SỬA LỖI TẬP PHIM (CHỈ CHẠY KHI BẠN GỌI THỦ CÔNG)
        // =================================================================
        public async Task BackfillAllEpisodesAsync()
        {
            _logger.LogInformation(">>> Bắt đầu tác vụ rà soát và điền tập phim cho tất cả phim bộ/hoạt hình.");

            var moviesToProcess = await _context.Movies
                                                .Include(m => m.Episodes)
                                                .Where(m => !string.IsNullOrEmpty(m.Slug) && (m.Type == "series" || m.Type == "hoathinh"))
                                                .ToListAsync();

            if (!moviesToProcess.Any())
            {
                _logger.LogWarning("Không tìm thấy phim bộ/hoạt hình nào để rà soát.");
                return;
            }

            _logger.LogInformation($"Tìm thấy {moviesToProcess.Count} phim. Bắt đầu gọi API...");
            int updatedMovieCount = 0;
            int processedCount = 0;

            foreach (var movie in moviesToProcess)
            {
                processedCount++;
                try
                {
                    var apiResponse = await _oPhimService.GetMovieDetailAsync(movie.Slug);
                    var apiEpisodes = apiResponse?.Item?.Episodes;

                    if (apiEpisodes == null || !apiEpisodes.Any()) continue;

                    if (movie.Episodes.Any()) _context.Episodes.RemoveRange(movie.Episodes);

                    foreach (var server in apiEpisodes)
                    {
                        foreach (var episodeData in server.ServerData)
                        {
                            movie.Episodes.Add(new DbEpisode
                            {
                                ServerName = server.ServerName,
                                EpisodeName = episodeData.Name,
                                Slug = episodeData.Slug,
                                LinkM3u8 = episodeData.LinkM3u8
                            });
                        }
                    }

                    movie.UpdatedAt = DateTime.Now;
                    updatedMovieCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Lỗi khi xử lý slug '{movie.Slug}'.");
                }

                if (processedCount % 50 == 0 && _context.ChangeTracker.HasChanges())
                {
                    await _context.SaveChangesAsync();
                }
            }

            if (_context.ChangeTracker.HasChanges())
            {
                await _context.SaveChangesAsync();
            }

            _logger.LogInformation($"*** HOÀN TẤT: Đã cập nhật tập phim cho {updatedMovieCount} phim. ***");
        }
    }
}
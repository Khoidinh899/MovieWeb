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
        Task SyncMoviesFromApiToDbAsync(List<ApiMovie> apiMovies, int minYear);
        Task BackfillAllEpisodesAsync();
        Task BackfillSingleMoviesAsync();
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
            await SyncMoviesFromApiToDbAsync(apiMovies, 0);
        }

        public async Task SyncMoviesFromApiToDbAsync(List<ApiMovie> apiMovies, int minYear)
        {
            int apiMovieCount = apiMovies?.Count ?? 0;
            _logger.LogInformation($"🎬🎬🎬 BẮT ĐẦU SYNC - API trả về: {apiMovieCount} phim, Lọc từ năm: {(minYear > 0 ? minYear : "không giới hạn")}");
            
            if (apiMovieCount == 0)
            {
                _logger.LogWarning("❌ Danh sách phim trống! Không có gì để sync.");
                return;
            }
            
            // Log chi tiết phim từ API
            var movieYears = apiMovies.GroupBy(m => m.Year).OrderByDescending(g => g.Key);
            foreach (var yearGroup in movieYears.Take(10))
            {
                _logger.LogInformation($"   📊 Năm {yearGroup.Key}: {yearGroup.Count()} phim - {string.Join(", ", yearGroup.Take(3).Select(m => m.Name))}");
            }
            
            // Lọc phim theo năm nếu có chỉ định
            var moviesToSync = minYear > 0 
                ? apiMovies.Where(m => m.Year >= minYear).ToList() 
                : apiMovies;

            _logger.LogInformation($"✅ Sau khi lọc: {moviesToSync.Count} phim sẽ được xử lý");

            int processedCount = 0;
            int addedCount = 0;
            int skippedCount = 0;

            foreach (var apiMovie in moviesToSync)
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
                            _logger.LogWarning($"❌ Không thể lấy chi tiết API cho slug: {apiMovie.Slug}");
                            skippedCount++;
                            continue;
                        }

                        var apiItem = apiResponse.Item;
                        _logger.LogInformation($"📺 Đang xử lý: {apiItem.Name} (Type: {apiItem.Type}, Year: {apiItem.Year})");

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
                            Trailer = apiItem.TrailerUrl,
                            Description = apiItem.Content,
                            Content = apiResponse.SeoOnPage?.DescriptionHead,
                        };

                        if (apiItem.Type == "series" || apiItem.Type == "hoathinh")
                        {
                            if (apiItem.Episodes == null || !apiItem.Episodes.Any())
                            {
                                _logger.LogWarning($"⚠️  '{apiItem.Slug}' không có episodes. Bỏ qua.");
                                skippedCount++;
                                continue;
                            }

                            bool hasValidEpisodes = false;
                            int episodeCount = 0;

                            foreach (var server in apiItem.Episodes)
                            {
                                foreach (var episodeData in server.ServerData)
                                {
                                    episodeCount++;
                                    string linkM3u8 = episodeData.LinkM3u8;
                                    
                                    _logger.LogInformation($"   📹 Episode: {episodeData.Name}, LinkM3u8: {(string.IsNullOrEmpty(linkM3u8) ? "NULL ❌" : "OK ✓")}");

                                    if (string.IsNullOrEmpty(linkM3u8))
                                    {
                                        _logger.LogWarning($"   ⚠️  Episode '{episodeData.Slug}' không có LinkM3u8");
                                        continue;
                                    }

                                    hasValidEpisodes = true;
                                    if (string.IsNullOrEmpty(dbMovie.TrailerUrl))
                                    {
                                        dbMovie.TrailerUrl = linkM3u8;
                                        _logger.LogInformation($"   ✅ Đã gán TrailerUrl: {linkM3u8}");
                                    }

                                    dbMovie.Episodes.Add(new DbEpisode
                                    {
                                        ServerName = server.ServerName,
                                        EpisodeName = episodeData.Name,
                                        Slug = episodeData.Slug,
                                        LinkM3u8 = linkM3u8
                                    });
                                }
                            }

                            if (!hasValidEpisodes)
                            {
                                _logger.LogWarning($"❌ '{apiItem.Slug}' không có episode nào với LinkM3u8 hợp lệ (tổng {episodeCount} episodes). Bỏ qua.");
                                skippedCount++;
                                continue;
                            }

                            _logger.LogInformation($"✅ '{apiItem.Slug}' có {dbMovie.Episodes.Count} episodes hợp lệ");
                        }

                        _context.Movies.Add(dbMovie);
                        addedCount++;
                        _logger.LogInformation($"✅ Chuẩn bị thêm: {dbMovie.Name}");
                    }
                    else
                    {
                        var movieToUpdate = await _context.Movies.Include(m => m.Episodes).FirstOrDefaultAsync(m => m.MovieId == existingMovie.MovieId);
                        if(movieToUpdate != null)
                        {
                            // Cập nhật tập phim hiện tại
                            if(movieToUpdate.EpisodeCurrent != apiMovie.EpisodeCurrent)
                            {
                                movieToUpdate.EpisodeCurrent = apiMovie.EpisodeCurrent;
                                movieToUpdate.UpdatedAt = DateTime.Now;
                                _logger.LogInformation($"📝 Cập nhật tập phim: {movieToUpdate.Name}");
                            }

                            // Nếu phim bộ/hoạt hình thiếu TrailerUrl (link m3u8 tập 1), thêm vào
                            if ((movieToUpdate.Type == "series" || movieToUpdate.Type == "hoathinh") && string.IsNullOrEmpty(movieToUpdate.TrailerUrl))
                            {
                                var apiResponse = await _oPhimService.GetMovieDetailAsync(apiMovie.Slug);
                                var apiItem = apiResponse?.Item;
                                
                                if (apiItem?.Episodes != null && apiItem.Episodes.Any())
                                {
                                    _logger.LogInformation($"🔧 Phim '{movieToUpdate.Name}' thiếu TrailerUrl, đang lấy link tập 1...");
                                    
                                    foreach (var server in apiItem.Episodes)
                                    {
                                        foreach (var episodeData in server.ServerData)
                                        {
                                            string linkM3u8 = episodeData.LinkM3u8;
                                            
                                            if (string.IsNullOrEmpty(linkM3u8))
                                            {
                                                continue;
                                            }

                                            // Lấy tập đầu tiên làm TrailerUrl
                                            movieToUpdate.TrailerUrl = linkM3u8;
                                            movieToUpdate.UpdatedAt = DateTime.Now;
                                            _logger.LogInformation($"✅ Đã gán TrailerUrl (tập 1): {linkM3u8}");
                                            break;
                                        }
                                        if (!string.IsNullOrEmpty(movieToUpdate.TrailerUrl))
                                            break;
                                    }

                                    if (string.IsNullOrEmpty(movieToUpdate.TrailerUrl))
                                    {
                                        _logger.LogWarning($"❌ Phim '{movieToUpdate.Name}' không tìm được tập 1 với LinkM3u8 hợp lệ");
                                    }
                                }
                            }
                        }
                    }

                    if (processedCount % 10 == 0 && _context.ChangeTracker.HasChanges())
                    {
                        await _context.SaveChangesAsync();
                        _logger.LogInformation("💾 =======> Đã lưu 1 lô phim vào DB <=======");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"❌ Lỗi khi đồng bộ phim: {apiMovie.Name}");
                    skippedCount++;
                }
            }
            
            if (_context.ChangeTracker.HasChanges())
            {
                await _context.SaveChangesAsync();
                _logger.LogInformation("💾 Đã lưu lô phim cuối cùng vào DB.");
            }

            _logger.LogInformation($"🎬 === HOÀN TẤT === Thêm mới: {addedCount}, Bỏ qua: {skippedCount}, Tổng xử lý: {processedCount}");
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
                    
                    if(movie.Episodes.Any()) _context.Episodes.RemoveRange(movie.Episodes);

                    foreach(var server in apiEpisodes) {
                        foreach(var episodeData in server.ServerData) {
                            string linkM3u8 = episodeData.LinkM3u8;
                            
                            // Lấy link m3u8 đầu tiên làm trailer
                            if (string.IsNullOrEmpty(movie.TrailerUrl) && !string.IsNullOrEmpty(linkM3u8))
                            {
                                movie.TrailerUrl = linkM3u8;
                            }

                            movie.Episodes.Add(new DbEpisode {
                                ServerName = server.ServerName, 
                                EpisodeName = episodeData.Name, 
                                Slug = episodeData.Slug, 
                                LinkM3u8 = linkM3u8
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

        /// <summary>
        /// Backfill episodes cho phim lẻ (movie) bị thiếu
        /// </summary>
        public async Task BackfillSingleMoviesAsync()
        {
            _logger.LogInformation(">>> Bắt đầu tác vụ rà soát và điền tập phim cho phim lẻ.");

            var moviesToProcess = await _context.Movies
                                                .Include(m => m.Episodes)
                                                .Where(m => !string.IsNullOrEmpty(m.Slug) && m.Type == "single")
                                                .ToListAsync();

            if (!moviesToProcess.Any())
            {
                _logger.LogWarning("Không tìm thấy phim lẻ nào để rà soát.");
                return;
            }

            _logger.LogInformation($"Tìm thấy {moviesToProcess.Count} phim lẻ. Bắt đầu gọi API...");
            int updatedMovieCount = 0;
            int processedCount = 0;

            foreach (var movie in moviesToProcess)
            {
                processedCount++;
                try
                {
                    var apiResponse = await _oPhimService.GetMovieDetailAsync(movie.Slug);
                    var apiItem = apiResponse?.Item;

                    if (apiItem?.Episodes == null || !apiItem.Episodes.Any())
                    {
                        _logger.LogWarning($"⚠️  Phim lẻ '{movie.Slug}' không có episodes.");
                        continue;
                    }

                    // Xoá episodes cũ nếu có
                    if(movie.Episodes.Any()) 
                        _context.Episodes.RemoveRange(movie.Episodes);

                    bool hasValidEpisodes = false;
                    foreach(var server in apiItem.Episodes) {
                        foreach(var episodeData in server.ServerData) {
                            string linkM3u8 = episodeData.LinkM3u8;
                            
                            if (string.IsNullOrEmpty(linkM3u8))
                            {
                                _logger.LogWarning($"   ⚠️  Episode '{episodeData.Slug}' không có LinkM3u8");
                                continue;
                            }

                            hasValidEpisodes = true;
                            if (string.IsNullOrEmpty(movie.TrailerUrl))
                            {
                                movie.TrailerUrl = linkM3u8;
                            }

                            movie.Episodes.Add(new DbEpisode {
                                ServerName = server.ServerName, 
                                EpisodeName = episodeData.Name, 
                                Slug = episodeData.Slug, 
                                LinkM3u8 = linkM3u8
                            });
                        }
                    }

                    if (hasValidEpisodes)
                    {
                        movie.UpdatedAt = DateTime.Now;
                        updatedMovieCount++;
                        _logger.LogInformation($"✅ Đã cập nhật {movie.Episodes.Count} episodes cho phim lẻ '{movie.Name}'");
                    }
                    else
                    {
                        _logger.LogWarning($"❌ Phim lẻ '{movie.Name}' không có episode nào với LinkM3u8 hợp lệ");
                    }
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

            _logger.LogInformation($"*** HOÀN TẤT: Đã cập nhật tập phim cho {updatedMovieCount} phim lẻ. ***");
        }
    }
}
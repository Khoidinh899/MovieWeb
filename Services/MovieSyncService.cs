// File: Services/MovieSyncService.cs (FIXED - CHỈ LƯU SERVER THẬT TỪ API)
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
using MovieWeb.Jobs;

namespace MovieWeb.Services
{
    public interface IMovieSyncService
    {
        Task SyncMoviesFromApiToDbAsync(List<ApiMovie> apiMovies);
        Task SyncMoviesFromApiToDbAsync(List<ApiMovie> apiMovies, int minYear);
        Task BackfillAllEpisodesAsync();
        Task BackfillSingleMoviesAsync();
        Task SyncMovieFromApiBySlug(string apiSlug, int movieId);
    }

    public class MovieSyncService : IMovieSyncService
    {
        private readonly MovieWebDbContext _context;
        private readonly IOPhimService _oPhimService;
        private readonly ILogger<MovieSyncService> _logger;
        private readonly ICategorySyncService _categorySyncService;
        private readonly ICountrySyncService _countrySyncService;
        private readonly IActorSyncService _actorSyncService;
        private readonly IDirectorSyncService _directorSyncService;

        public MovieSyncService(
            MovieWebDbContext context,
            IOPhimService oPhimService,
            ILogger<MovieSyncService> logger,
            ICategorySyncService categorySyncService,
            ICountrySyncService countrySyncService,
            IActorSyncService actorSyncService,
            IDirectorSyncService directorSyncService)
        {
            _context = context;
            _oPhimService = oPhimService;
            _logger = logger;
            _categorySyncService = categorySyncService;
            _countrySyncService = countrySyncService;
            _actorSyncService = actorSyncService;
            _directorSyncService = directorSyncService;
        }
        public async Task SyncMovieFromApiBySlug(string apiSlug, int movieId)
        {
            _logger.LogInformation("[Hangfire Job] Bắt đầu sync tập phim cho MovieID: {MovieId}, ApiSlug: {ApiSlug}", movieId, apiSlug);

            try
            {
                // 1. Lấy chi tiết phim từ API
                var apiResponse = await _oPhimService.GetMovieDetailAsync(apiSlug);
                var apiItem = apiResponse?.Item;

                // Kiểm tra API có trả về dữ liệu và có tập phim không
                if (apiItem == null || apiItem.Episodes == null || !apiItem.Episodes.Any())
                {
                    _logger.LogWarning("[Hangfire Job] ❌ Không tìm thấy phim hoặc không có tập phim trên API. Slug: {ApiSlug}, MovieID: {MovieId}", apiSlug, movieId);
                    return;
                }

                // 2. Lấy phim từ DB để xóa tập cũ và cập nhật
                var movieInDb = await _context.Movies
                                    .Include(m => m.Episodes) // Phải Include Episodes để xóa
                                    .FirstOrDefaultAsync(m => m.MovieId == movieId);

                if (movieInDb == null)
                {
                    _logger.LogError("[Hangfire Job] ❌ Không tìm thấy Movie trong DB với ID: {MovieId}. Không thể sync tập.", movieId);
                    return;
                }

                // 3. Xóa tất cả tập phim cũ (để đảm bảo sync lại sạch sẽ)
                if (movieInDb.Episodes.Any())
                {
                    _logger.LogInformation("[Hangfire Job] 🧹 Xóa {EpisodeCount} tập phim cũ của MovieID: {MovieId}", movieInDb.Episodes.Count, movieId);
                    _context.Episodes.RemoveRange(movieInDb.Episodes);
                    // Không cần movieInDb.Episodes.Clear() vì RemoveRange đã theo dõi thay đổi
                }

                // Dùng HashSet để chống trùng lặp (giống hệt hàm Backfill )
                var addedEpisodeKeys = new HashSet<string>();
                int addedCount = 0;
                string firstValidLink = null; // Dùng để cập nhật TrailerUrl cho phim

                // 4. Lặp qua server và tập phim từ API
                foreach (var server in apiItem.Episodes)
                {
                    string serverName = server.ServerName?.Trim() ?? "Vietsub";
                    foreach (var episodeData in server.ServerData)
                    {
                        string linkM3u8 = episodeData.LinkM3u8;

                        // Bỏ qua nếu không có link M3U8 hợp lệ
                        if (string.IsNullOrEmpty(linkM3u8))
                        {
                            continue;
                        }

                        // Lấy link đầu tiên làm TrailerUrl nếu chưa có
                        if (firstValidLink == null)
                        {
                            firstValidLink = linkM3u8;
                        }

                        // Tạo key duy nhất (slug + server) để chống trùng
                        string uniqueKey = $"{episodeData.Slug}|{serverName}";
                        if (addedEpisodeKeys.Add(uniqueKey))
                        {
                            // 5. Tạo và Thêm tập mới vào DB
                            var newDbEpisode = new DbEpisode
                            {
                                MovieId = movieId, // 👈 QUAN TRỌNG: Link với phim đã tạo
                                ServerName = serverName,
                                EpisodeName = episodeData.Name,
                                Slug = episodeData.Slug,
                                LinkM3u8 = linkM3u8
                                // Ông có thể thêm CreatedAt/UpdatedAt nếu bảng Episodes có
                            };
                            _context.Episodes.Add(newDbEpisode); // Thêm vào context
                            addedCount++;
                        }
                    }
                }

                if (addedCount == 0)
                {
                    _logger.LogWarning("[Hangfire Job] ⚠️ Không tìm thấy tập phim nào có LinkM3u8 hợp lệ cho MovieID: {MovieId}", movieId);
                }

                // 6. Cập nhật lại thông tin cho Phim (Bảng Movies)
                movieInDb.EpisodeCurrent = apiItem.EpisodeCurrent;
                movieInDb.EpisodeTotal = apiItem.EpisodeTotal;
                movieInDb.Status = apiItem.Status;
                movieInDb.UpdatedAt = DateTime.Now;

                // Cập nhật TrailerUrl (link tập 1) nếu nó chưa có
                if (string.IsNullOrEmpty(movieInDb.TrailerUrl) && firstValidLink != null)
                {
                    movieInDb.TrailerUrl = firstValidLink;
                }

                _context.Movies.Update(movieInDb); // Đánh dấu phim là đã cập nhật

                // 7. Lưu tất cả thay đổi (xóa tập cũ, thêm tập mới, cập nhật phim)
                await _context.SaveChangesAsync();
                
                // ✅ Làm mới sitemap khi có phim mới
                SitemapCacheRefreshJob.TriggerImmediately();

                _logger.LogInformation("[Hangfire Job] ✅ THÀNH CÔNG: Sync cho MovieID: {MovieId}. Đã thêm {AddedCount} tập. Cập nhật: {MovieName}",
                    movieId, addedCount, movieInDb.Name);

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Hangfire Job] ❌ THẤT BẠI: Sync tập phim cho MovieID: {MovieId}", movieId);
                // Ném lỗi lại để Hangfire biết và retry
                throw;
            }
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

            var movieYears = apiMovies?.GroupBy(m => m.Year).OrderByDescending(g => g.Key);
            if (movieYears != null)
            {
                foreach (var yearGroup in movieYears.Take(10))
                {
                    _logger.LogInformation($"   📊 Năm {yearGroup.Key}: {yearGroup.Count()} phim - {string.Join(", ", yearGroup.Take(3).Select(m => m.Name))}");
                }
            }

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

                        // === SYNC CATEGORIES, COUNTRIES, ACTORS, DIRECTORS TRƯỚC ===
                        _logger.LogInformation($"🔄 Bắt đầu sync metadata cho phim: {apiItem.Name}");

                        var syncedCategories = await _categorySyncService.SyncCategoriesAsync(apiItem.Category ?? new List<MovieWeb.Models.API.Category>());
                        var syncedCountries = await _countrySyncService.SyncCountriesAsync(apiItem.Country ?? new List<MovieWeb.Models.API.Country>());
                        var syncedActors = await _actorSyncService.SyncActorsAsync(apiItem.Actor);
                        var syncedDirectors = await _directorSyncService.SyncDirectorsAsync(apiItem.Director);

                        _logger.LogInformation($"✅ Đã sync metadata: {syncedCategories.Count} categories, {syncedCountries.Count} countries, {syncedActors.Count} actors, {syncedDirectors.Count} directors");

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
                            Trailer = apiItem.TrailerUrl,
                        };

                        dbMovie.Categories = syncedCategories;
                        dbMovie.Countries = syncedCountries;
                        dbMovie.Actors = syncedActors;
                        dbMovie.Directors = syncedDirectors;

                        // === LOGIC XỬ LÝ LINK XEM PHIM (M3U8) ===
                        if (apiItem.Type == "single")
                        {
                            // ✅ CHỈ LƯU VÀO TrailerUrl, KHÔNG TẠO EPISODE
                            var firstServer = apiItem.Episodes?.FirstOrDefault();
                            var firstEpisode = firstServer?.ServerData?.FirstOrDefault();

                            if (firstEpisode != null && !string.IsNullOrEmpty(firstEpisode.LinkM3u8))
                            {
                                dbMovie.TrailerUrl = firstEpisode.LinkM3u8;
                                _logger.LogInformation($"✅ Phim lẻ '{dbMovie.Name}' - Server: {firstServer.ServerName}");
                            }
                            else
                            {
                                _logger.LogWarning($"⚠️  Phim lẻ '{apiItem.Slug}' không có link xem hợp lệ.");
                            }
                        }
                        else if (apiItem.Type == "series" || apiItem.Type == "hoathinh")
                        {
                            if (apiItem.Episodes == null || !apiItem.Episodes.Any())
                            {
                                _logger.LogWarning($"⚠️  '{apiItem.Slug}' không có episodes. Bỏ qua.");
                                skippedCount++;
                                continue;
                            }

                            bool hasValidEpisodes = false;
                            int episodeCount = 0;

                            // ✅ HASHSET CHỐNG TRÙNG LẶP THEO SLUG + SERVER
                            var addedEpisodeKeys = new HashSet<string>();

                            foreach (var server in apiItem.Episodes)
                            {
                                string serverName = server.ServerName?.Trim() ?? "Vietsub";

                                _logger.LogInformation($"   🖥️  Server: {serverName}");

                                foreach (var episodeData in server.ServerData)
                                {
                                    episodeCount++;
                                    string linkM3u8 = episodeData.LinkM3u8;

                                    _logger.LogInformation($"      📹 Episode: {episodeData.Name}, LinkM3u8: {(string.IsNullOrEmpty(linkM3u8) ? "NULL ❌" : "OK ✓")}");

                                    if (string.IsNullOrEmpty(linkM3u8))
                                    {
                                        _logger.LogWarning($"      ⚠️  Episode '{episodeData.Slug}' không có LinkM3u8");
                                        continue;
                                    }

                                    // ✅ TẠO KEY DUY NHẤT: slug + serverName
                                    string uniqueKey = $"{episodeData.Slug}|{serverName}";

                                    if (addedEpisodeKeys.Add(uniqueKey))
                                    {
                                        hasValidEpisodes = true;

                                        if (string.IsNullOrEmpty(dbMovie.TrailerUrl))
                                        {
                                            dbMovie.TrailerUrl = linkM3u8;
                                            _logger.LogInformation($"      ✅ Đã gán TrailerUrl: {linkM3u8}");
                                        }

                                        // ✅ CHỈ LƯU SERVER THẬT TỪ API (KHÔNG TẠO FAKE)
                                        dbMovie.Episodes.Add(new DbEpisode
                                        {
                                            ServerName = serverName,
                                            EpisodeName = episodeData.Name,
                                            Slug = episodeData.Slug,
                                            LinkM3u8 = linkM3u8
                                        });

                                        _logger.LogInformation($"      ✅ Đã thêm: Server={serverName}, Tập={episodeData.Name}");
                                    }
                                    else
                                    {
                                        _logger.LogInformation($"      ⏭️  Bỏ qua tập trùng: {episodeData.Name} (Server: {serverName})");
                                    }
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
                        if (movieToUpdate != null)
                        {
                            if (movieToUpdate.EpisodeCurrent != apiMovie.EpisodeCurrent)
                            {
                                movieToUpdate.EpisodeCurrent = apiMovie.EpisodeCurrent;
                                movieToUpdate.UpdatedAt = DateTime.Now;
                                _logger.LogInformation($"📝 Cập nhật tập phim: {movieToUpdate.Name}");
                            }

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

                                            if (string.IsNullOrEmpty(linkM3u8)) continue;

                                            movieToUpdate.TrailerUrl = linkM3u8;
                                            movieToUpdate.UpdatedAt = DateTime.Now;
                                            _logger.LogInformation($"✅ Đã gán TrailerUrl (tập 1): {linkM3u8}");
                                            break;
                                        }
                                        if (!string.IsNullOrEmpty(movieToUpdate.TrailerUrl)) break;
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
                        SitemapCacheRefreshJob.TriggerImmediately();
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
                SitemapCacheRefreshJob.TriggerImmediately();
                _logger.LogInformation("💾 Đã lưu lô phim cuối cùng vào DB.");
            }

            _logger.LogInformation($"🎬 === HOÀN TẤT === Thêm mới: {addedCount}, Bỏ qua: {skippedCount}, Tổng xử lý: {processedCount}");
        }

        // =================================================================
        // HÀM SỬA LỖI TẬP PHIM CHO PHIM BỘ/HOẠT HÌNH (CHỈ CHẠY THỦ CÔNG)
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

                    if (movie.Episodes.Any())
                    {
                        _context.Episodes.RemoveRange(movie.Episodes);
                        movie.Episodes.Clear();
                    }

                    // ✅ HASHSET CHỐNG TRÙNG LẶP THEO SLUG + SERVER
                    var addedEpisodeKeys = new HashSet<string>();

                    foreach (var server in apiEpisodes)
                    {
                        string serverName = server.ServerName?.Trim() ?? "Vietsub";

                        foreach (var episodeData in server.ServerData)
                        {
                            string linkM3u8 = episodeData.LinkM3u8;
                            if (string.IsNullOrEmpty(linkM3u8)) continue;

                            if (string.IsNullOrEmpty(movie.TrailerUrl))
                            {
                                movie.TrailerUrl = linkM3u8;
                            }

                            // ✅ TẠO KEY DUY NHẤT: slug + serverName
                            string uniqueKey = $"{episodeData.Slug}|{serverName}";

                            if (addedEpisodeKeys.Add(uniqueKey))
                            {
                                movie.Episodes.Add(new DbEpisode
                                {
                                    ServerName = serverName,
                                    EpisodeName = episodeData.Name,
                                    Slug = episodeData.Slug,
                                    LinkM3u8 = linkM3u8
                                });
                            }
                        }
                    }

                    movie.UpdatedAt = DateTime.Now;
                    updatedMovieCount++;
                    _logger.LogInformation($"✅ Đã xử lý: {movie.Name} - {movie.Episodes.Count} episodes");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Lỗi khi xử lý slug '{movie.Slug}'.");
                }

                if (processedCount % 50 == 0 && _context.ChangeTracker.HasChanges())
                {
                    await _context.SaveChangesAsync();
                    SitemapCacheRefreshJob.TriggerImmediately();
                }
            }

            if (_context.ChangeTracker.HasChanges())
            {
                await _context.SaveChangesAsync();
                SitemapCacheRefreshJob.TriggerImmediately();
            }

            _logger.LogInformation($"*** HOÀN TẤT: Đã cập nhật tập phim cho {updatedMovieCount} phim. ***");
        }

        // =================================================================
        // HÀM SỬA LỖI CHO PHIM LẺ - CHỈ LƯU VÀO BẢNG MOVIES
        // =================================================================
        public async Task BackfillSingleMoviesAsync()
        {
            _logger.LogInformation(">>> Bắt đầu tác vụ rà soát và cập nhật link xem cho phim lẻ.");

            var moviesToProcess = await _context.Movies
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

                    // ✅ CHỈ LẤY LINK TỪ SERVER ĐẦU TIÊN
                    var firstServer = apiItem.Episodes.FirstOrDefault();
                    var firstEpisode = firstServer?.ServerData?.FirstOrDefault();

                    if (firstEpisode == null || string.IsNullOrEmpty(firstEpisode.LinkM3u8))
                    {
                        _logger.LogWarning($"⚠️  Phim lẻ '{movie.Slug}' không có LinkM3u8 hợp lệ.");
                        continue;
                    }

                    // ✅ CHỈ CẬP NHẬT BẢNG MOVIES (TrailerUrl)
                    if (string.IsNullOrEmpty(movie.TrailerUrl) || movie.TrailerUrl != firstEpisode.LinkM3u8)
                    {
                        movie.TrailerUrl = firstEpisode.LinkM3u8;
                        movie.UpdatedAt = DateTime.Now;
                        updatedMovieCount++;

                        _logger.LogInformation($"✅ Đã cập nhật link xem cho phim lẻ '{movie.Name}'");
                        _logger.LogInformation($"   Server: {firstServer.ServerName}, Link: {firstEpisode.LinkM3u8}");
                    }
                    else
                    {
                        _logger.LogInformation($"⏭️  Phim lẻ '{movie.Name}' đã có link xem, bỏ qua.");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Lỗi khi xử lý slug '{movie.Slug}'.");
                }

                // Lưu mỗi 50 phim
                if (processedCount % 50 == 0 && _context.ChangeTracker.HasChanges())
                {
                    await _context.SaveChangesAsync();
                    SitemapCacheRefreshJob.TriggerImmediately();
                    _logger.LogInformation($"💾 Đã lưu {processedCount}/{moviesToProcess.Count} phim lẻ.");
                }
            }

            // Lưu phần còn lại
            if (_context.ChangeTracker.HasChanges())
            {
                await _context.SaveChangesAsync();
                SitemapCacheRefreshJob.TriggerImmediately();
            }

            _logger.LogInformation($"*** HOÀN TẤT: Đã cập nhật link xem cho {updatedMovieCount}/{moviesToProcess.Count} phim lẻ. ***");
        }
    }
}
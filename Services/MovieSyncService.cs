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
        Task BackfillFromVSMovAsync(int fromPage = 1, int toPage = 10);
        Task SyncMoviesFromVSMovApiAsync(int startPage = 1, int endPage = 10);
        Task<DbMovie?> SyncSingleMovieFromVSMovBySlugAsync(string slug);
        Task<(DbMovie? Movie, bool WasUpdatedOrAdded)> SyncSingleMovieFromVSMovBySlugWithStatusAsync(string slug);
    }

    public class MovieSyncService : IMovieSyncService
    {
        private readonly MovieWebDbContext _context;
        private readonly IOPhimService _oPhimService;
        private readonly IVSMovService _vsMovService;
        private readonly ILogger<MovieSyncService> _logger;
        private readonly ICategorySyncService _categorySyncService;
        private readonly ICountrySyncService _countrySyncService;
        private readonly IActorSyncService _actorSyncService;
        private readonly IDirectorSyncService _directorSyncService;

        public MovieSyncService(
            MovieWebDbContext context,
            IOPhimService oPhimService,
            IVSMovService vsMovService,
            ILogger<MovieSyncService> logger,
            ICategorySyncService categorySyncService,
            ICountrySyncService countrySyncService,
            IActorSyncService actorSyncService,
            IDirectorSyncService directorSyncService)
        {
            _context = context;
            _oPhimService = oPhimService;
            _vsMovService = vsMovService;
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
                            // Nếu số tập hiển thị trên API khác số tập hiện tại của DB (Có tập mới)
                            if (movieToUpdate.EpisodeCurrent != apiMovie.EpisodeCurrent)
                            {
                                _logger.LogInformation($"📝 Phát hiện phim '{movieToUpdate.Name}' có tập mới ({movieToUpdate.EpisodeCurrent} -> {apiMovie.EpisodeCurrent}). Đang lấy chi tiết để đồng bộ...");
                                
                                var apiResponse = await _oPhimService.GetMovieDetailAsync(apiMovie.Slug);
                                var apiItem = apiResponse?.Item;
                                
                                if (apiItem != null)
                                {
                                    movieToUpdate.EpisodeCurrent = apiItem.EpisodeCurrent;
                                    movieToUpdate.EpisodeTotal = apiItem.EpisodeTotal;
                                    movieToUpdate.Status = apiItem.Status;
                                    movieToUpdate.Quality = apiItem.Quality;
                                    movieToUpdate.Language = apiItem.Lang;
                                    movieToUpdate.UpdatedAt = DateTime.Now;

                                    if (apiItem.Type == "single")
                                    {
                                        var firstServer = apiItem.Episodes?.FirstOrDefault();
                                        var firstEpisode = firstServer?.ServerData?.FirstOrDefault();
                                        if (firstEpisode != null && !string.IsNullOrEmpty(firstEpisode.LinkM3u8))
                                        {
                                            movieToUpdate.TrailerUrl = firstEpisode.LinkM3u8;
                                        }
                                    }
                                    else if (apiItem.Type == "series" || apiItem.Type == "hoathinh")
                                    {
                                        if (apiItem.Episodes != null)
                                        {
                                            var existingEpisodesMap = movieToUpdate.Episodes
                                                .ToDictionary(e => $"{e.Slug}|{e.ServerName?.Trim() ?? "Vietsub"}", e => e);

                                            foreach (var server in apiItem.Episodes)
                                            {
                                                string serverName = server.ServerName?.Trim() ?? "Vietsub";
                                                foreach (var episodeData in server.ServerData)
                                                {
                                                    string linkM3u8 = episodeData.LinkM3u8;
                                                    if (string.IsNullOrEmpty(linkM3u8)) continue;

                                                    string uniqueKey = $"{episodeData.Slug}|{serverName}";
                                                    if (existingEpisodesMap.TryGetValue(uniqueKey, out var existingEp))
                                                    {
                                                        // Nếu tập đã có nhưng link m3u8 thay đổi -> cập nhật link mới
                                                        if (existingEp.LinkM3u8 != linkM3u8)
                                                        {
                                                            existingEp.LinkM3u8 = linkM3u8;
                                                            _logger.LogInformation($"      🔄 Cập nhật link tập {episodeData.Name} (Server: {serverName})");
                                                        }
                                                    }
                                                    else
                                                    {
                                                        // Chưa có tập này -> Thêm mới tập phim
                                                        var newEp = new DbEpisode
                                                        {
                                                            ServerName = serverName,
                                                            EpisodeName = episodeData.Name,
                                                            Slug = episodeData.Slug,
                                                            LinkM3u8 = linkM3u8
                                                        };
                                                        movieToUpdate.Episodes.Add(newEp);
                                                        _logger.LogInformation($"      ✅ Thêm tập mới: Server={serverName}, Tập={episodeData.Name}");
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                            else if ((movieToUpdate.Type == "series" || movieToUpdate.Type == "hoathinh") && string.IsNullOrEmpty(movieToUpdate.TrailerUrl))
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

        public async Task SyncMoviesFromVSMovApiAsync(int startPage = 1, int endPage = 10)
        {
            await BackfillFromVSMovAsync(startPage, endPage);
        }

        // =================================================================
        // HÀM ĐỒNG BỘ TỪ NGUỒN VSMOV (DỰ PHÒNG THAY THẾ OPHIM)
        // =================================================================
        public async Task BackfillFromVSMovAsync(int fromPage = 1, int toPage = 10)
        {
            _logger.LogWarning($"🔄🔄🔄 [VSMov] BẮT ĐẦU ĐỒNG BỘ TỪ VSMOV - Trang {fromPage} đến {toPage}");

            int totalUpdated = 0;
            int totalAdded = 0;
            int totalSkipped = 0;

            for (int page = fromPage; page <= toPage; page++)
            {
                try
                {
                    _logger.LogInformation($"[VSMov] 📄 Đang cào trang {page}...");
                    var listResponse = await _vsMovService.GetLatestMoviesAsync(page);

                    if (listResponse?.Items == null || !listResponse.Items.Any())
                    {
                        _logger.LogWarning($"[VSMov] ⚠️ Trang {page} không có phim hoặc API lỗi. Dừng.");
                        break;
                    }

                    _logger.LogInformation($"[VSMov] ✅ Trang {page}: {listResponse.Items.Count} phim");

                    foreach (var vsItem in listResponse.Items)
                    {
                        try
                        {
                            if (string.IsNullOrEmpty(vsItem.Slug))
                            {
                                totalSkipped++;
                                continue;
                            }

                            // 1. Tìm phim trong DB theo slug
                            var movieInDb = await _context.Movies
                                .Include(m => m.Episodes)
                                .FirstOrDefaultAsync(m => m.Slug == vsItem.Slug);

                            // 2. Lấy chi tiết phim từ VSMov API
                            var detailResponse = await _vsMovService.GetMovieDetailAsync(vsItem.Slug);
                            var vsDetail = detailResponse?.Item;

                            if (detailResponse?.Episodes == null || !detailResponse.Episodes.Any())
                            {
                                _logger.LogWarning($"[VSMov] ⚠️ Phim '{vsItem.Slug}' không có episodes từ VSMov. Bỏ qua.");
                                totalSkipped++;
                                continue;
                            }

                            if (movieInDb != null)
                            {
                                // === PHIM ĐÃ CÓ TRONG DB: CẬP NHẬT LINK TỪ VSMOV ===
                                _logger.LogInformation($"[VSMov] 🔄 Cập nhật phim đã có: '{movieInDb.Name}' (ID: {movieInDb.MovieId})");

                                // Xóa tất cả episode cũ
                                if (movieInDb.Episodes.Any())
                                {
                                    _context.Episodes.RemoveRange(movieInDb.Episodes);
                                    movieInDb.Episodes.Clear();
                                }

                                var addedEpisodeKeys = new HashSet<string>();
                                string firstValidLink = null;

                                foreach (var server in detailResponse.Episodes)
                                {
                                    string serverName = server.ServerName?.Trim() ?? "VSMov";

                                    if (server.ServerData == null) continue;

                                    foreach (var epData in server.ServerData)
                                    {
                                        string linkEmbed = epData.LinkEmbed;
                                        if (string.IsNullOrEmpty(linkEmbed)) continue;

                                        if (firstValidLink == null) firstValidLink = linkEmbed;

                                        string uniqueKey = $"{epData.Slug}|{serverName}";
                                        if (addedEpisodeKeys.Add(uniqueKey))
                                        {
                                            movieInDb.Episodes.Add(new DbEpisode
                                            {
                                                MovieId = movieInDb.MovieId,
                                                ServerName = serverName,
                                                EpisodeName = epData.Name ?? "1",
                                                Slug = epData.Slug ?? "tap-1",
                                                LinkM3u8 = linkEmbed // Lưu link embed vào cột LinkM3u8
                                            });
                                        }
                                    }
                                }

                                // Cập nhật metadata & Poster/Thumb URL từ VSMov
                                if (!string.IsNullOrEmpty(vsDetail.PosterUrl) && vsDetail.PosterUrl.StartsWith("http"))
                                    movieInDb.PosterUrl = vsDetail.PosterUrl;
                                else if (!string.IsNullOrEmpty(vsItem.PosterUrl) && vsItem.PosterUrl.StartsWith("http"))
                                    movieInDb.PosterUrl = vsItem.PosterUrl;

                                if (!string.IsNullOrEmpty(vsDetail.ThumbUrl) && vsDetail.ThumbUrl.StartsWith("http"))
                                    movieInDb.ThumbUrl = vsDetail.ThumbUrl;
                                else if (!string.IsNullOrEmpty(vsItem.ThumbUrl) && vsItem.ThumbUrl.StartsWith("http"))
                                    movieInDb.ThumbUrl = vsItem.ThumbUrl;

                                if (!string.IsNullOrEmpty(vsDetail.EpisodeCurrent))
                                    movieInDb.EpisodeCurrent = vsDetail.EpisodeCurrent;
                                if (!string.IsNullOrEmpty(vsDetail.EpisodeTotal))
                                    movieInDb.EpisodeTotal = vsDetail.EpisodeTotal;
                                if (!string.IsNullOrEmpty(vsDetail.Status))
                                    movieInDb.Status = vsDetail.Status;
                                movieInDb.UpdatedAt = DateTime.Now;

                                totalUpdated++;
                                _logger.LogInformation($"[VSMov] ✅ Đã cập nhật: '{movieInDb.Name}' - {movieInDb.Episodes.Count} tập mới từ VSMov");
                            }
                            else
                            {
                                // === PHIM CHƯA CÓ TRONG DB: TẠO MỚI ===
                                _logger.LogInformation($"[VSMov] ➕ Tạo mới phim: '{vsDetail.Name}'");

                                var newMovie = new DbMovie
                                {
                                    ApiId = vsItem.Id.ToString(),
                                    Slug = vsDetail.Slug ?? vsItem.Slug,
                                    Name = vsDetail.Name ?? vsItem.Name,
                                    OriginalName = vsDetail.OriginName ?? vsItem.OriginName,
                                    Type = vsDetail.Type ?? "series",
                                    Status = vsDetail.Status ?? "ongoing",
                                    PosterUrl = vsDetail.PosterUrl ?? vsItem.PosterUrl,
                                    ThumbUrl = vsDetail.ThumbUrl ?? vsItem.ThumbUrl,
                                    Time = vsDetail.Time,
                                    EpisodeCurrent = vsDetail.EpisodeCurrent,
                                    EpisodeTotal = vsDetail.EpisodeTotal,
                                    Quality = vsDetail.Quality ?? "HD",
                                    Language = vsDetail.Lang ?? "Vietsub",
                                    Year = vsDetail.Year > 0 ? vsDetail.Year : (vsItem.Year > 0 ? vsItem.Year : 2024),
                                    IsActive = true,
                                    CreatedAt = DateTime.Now,
                                    UpdatedAt = DateTime.Now,
                                    IsBanner = false,
                                    Description = vsDetail.Content,
                                };

                                // Sync categories & countries nếu có
                                if (vsDetail.Category != null && vsDetail.Category.Any())
                                {
                                    var apiCategories = vsDetail.Category.Select(c => new MovieWeb.Models.API.Category
                                    {
                                        Name = c.Name,
                                        Slug = c.Slug
                                    }).ToList();
                                    newMovie.Categories = await _categorySyncService.SyncCategoriesAsync(apiCategories);
                                }

                                if (vsDetail.Country != null && vsDetail.Country.Any())
                                {
                                    var apiCountries = vsDetail.Country.Select(c => new MovieWeb.Models.API.Country
                                    {
                                        Name = c.Name,
                                        Slug = c.Slug
                                    }).ToList();
                                    newMovie.Countries = await _countrySyncService.SyncCountriesAsync(apiCountries);
                                }

                                if (vsDetail.Actor != null && vsDetail.Actor.Any())
                                {
                                    newMovie.Actors = await _actorSyncService.SyncActorsAsync(vsDetail.Actor);
                                }

                                if (vsDetail.Director != null && vsDetail.Director.Any())
                                {
                                    newMovie.Directors = await _directorSyncService.SyncDirectorsAsync(vsDetail.Director);
                                }

                                // Thêm episodes
                                var addedKeys = new HashSet<string>();
                                foreach (var server in detailResponse.Episodes)
                                {
                                    string serverName = server.ServerName?.Trim() ?? "VSMov";
                                    if (server.ServerData == null) continue;

                                    foreach (var epData in server.ServerData)
                                    {
                                        string linkEmbed = epData.LinkEmbed;
                                        if (string.IsNullOrEmpty(linkEmbed)) continue;

                                        if (string.IsNullOrEmpty(newMovie.TrailerUrl))
                                            newMovie.TrailerUrl = linkEmbed;

                                        string uniqueKey = $"{epData.Slug}|{serverName}";
                                        if (addedKeys.Add(uniqueKey))
                                        {
                                            newMovie.Episodes.Add(new DbEpisode
                                            {
                                                ServerName = serverName,
                                                EpisodeName = epData.Name ?? "1",
                                                Slug = epData.Slug ?? "tap-1",
                                                LinkM3u8 = linkEmbed
                                            });
                                        }
                                    }
                                }

                                if (newMovie.Episodes.Any())
                                {
                                    _context.Movies.Add(newMovie);
                                    totalAdded++;
                                    _logger.LogInformation($"[VSMov] ✅ Đã tạo mới: '{newMovie.Name}' - {newMovie.Episodes.Count} tập");
                                }
                                else
                                {
                                    totalSkipped++;
                                    _logger.LogWarning($"[VSMov] ⚠️ Phim '{vsDetail.Name}' không có tập nào với link hợp lệ. Bỏ qua.");
                                }
                            }

                            // Delay nhẹ để tránh spam API
                            await Task.Delay(200);
                        }
                        catch (Exception exMovie)
                        {
                            _logger.LogError(exMovie, $"[VSMov] ❌ Lỗi khi xử lý phim '{vsItem.Slug}'");
                            totalSkipped++;
                        }
                    }

                    // Lưu mỗi trang
                    if (_context.ChangeTracker.HasChanges())
                    {
                        await _context.SaveChangesAsync();
                        SitemapCacheRefreshJob.TriggerImmediately();
                        _logger.LogInformation($"[VSMov] 💾 Đã lưu dữ liệu trang {page} vào DB.");
                    }

                    // Delay giữa các trang
                    await Task.Delay(500);
                }
                catch (Exception exPage)
                {
                    _logger.LogError(exPage, $"[VSMov] ❌ Lỗi khi cào trang {page}. Bỏ qua.");
                }
            }

            // Lưu phần còn lại
            if (_context.ChangeTracker.HasChanges())
            {
                await _context.SaveChangesAsync();
                SitemapCacheRefreshJob.TriggerImmediately();
            }

            _logger.LogWarning($"🔄🔄🔄 [VSMov] HOÀN TẤT: Cập nhật={totalUpdated}, Thêm mới={totalAdded}, Bỏ qua={totalSkipped}");
        }

        public async Task<DbMovie?> SyncSingleMovieFromVSMovBySlugAsync(string slug)
        {
            var (movie, _) = await SyncSingleMovieFromVSMovBySlugWithStatusAsync(slug);
            return movie;
        }

        public async Task<(DbMovie? Movie, bool WasUpdatedOrAdded)> SyncSingleMovieFromVSMovBySlugWithStatusAsync(string slug)
        {
            _logger.LogInformation($"[VSMov Sync] Bắt đầu đồng bộ phim slug '{slug}'...");

            var detailResponse = await _vsMovService.GetMovieDetailAsync(slug);
            if (detailResponse?.Item == null)
            {
                _logger.LogWarning($"[VSMov Sync] ❌ Không tìm thấy thông tin chi tiết phim cho slug: {slug}");
                return (null, false);
            }

            var vsDetail = detailResponse.Item;
            bool isNewMovie = false;
            bool wasEpisodesModified = false;

            var movieInDb = await _context.Movies
                .Include(m => m.Episodes)
                .Include(m => m.Categories)
                .Include(m => m.Countries)
                .FirstOrDefaultAsync(m => m.Slug == slug);

            if (movieInDb == null)
            {
                isNewMovie = true;
                movieInDb = new DbMovie
                {
                    ApiId = vsDetail.Id.ToString(),
                    Slug = vsDetail.Slug ?? slug,
                    Name = vsDetail.Name,
                    OriginalName = vsDetail.OriginName,
                    Type = vsDetail.Type ?? "series",
                    Status = vsDetail.Status ?? "ongoing",
                    PosterUrl = vsDetail.PosterUrl,
                    ThumbUrl = vsDetail.ThumbUrl,
                    Time = vsDetail.Time,
                    EpisodeCurrent = vsDetail.EpisodeCurrent,
                    EpisodeTotal = vsDetail.EpisodeTotal,
                    Quality = vsDetail.Quality ?? "HD",
                    Language = vsDetail.Lang ?? "Vietsub",
                    Year = vsDetail.Year > 0 ? vsDetail.Year : 2024,
                    IsActive = true,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now,
                    Description = vsDetail.Content
                };

                if (vsDetail.Category != null && vsDetail.Category.Any())
                {
                    var apiCategories = vsDetail.Category.Select(c => new MovieWeb.Models.API.Category { Name = c.Name, Slug = c.Slug }).ToList();
                    movieInDb.Categories = await _categorySyncService.SyncCategoriesAsync(apiCategories);
                }

                if (vsDetail.Country != null && vsDetail.Country.Any())
                {
                    var apiCountries = vsDetail.Country.Select(c => new MovieWeb.Models.API.Country { Name = c.Name, Slug = c.Slug }).ToList();
                    movieInDb.Countries = await _countrySyncService.SyncCountriesAsync(apiCountries);
                }

                if (vsDetail.Actor != null && vsDetail.Actor.Any())
                {
                    movieInDb.Actors = await _actorSyncService.SyncActorsAsync(vsDetail.Actor);
                }

                if (vsDetail.Director != null && vsDetail.Director.Any())
                {
                    movieInDb.Directors = await _directorSyncService.SyncDirectorsAsync(vsDetail.Director);
                }

                _context.Movies.Add(movieInDb);
            }
            else
            {
                if (!string.Equals(movieInDb.EpisodeCurrent, vsDetail.EpisodeCurrent, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(movieInDb.Status, vsDetail.Status, StringComparison.OrdinalIgnoreCase))
                {
                    wasEpisodesModified = true;
                }

                if (!string.IsNullOrEmpty(vsDetail.PosterUrl) && vsDetail.PosterUrl.StartsWith("http"))
                    movieInDb.PosterUrl = vsDetail.PosterUrl;
                if (!string.IsNullOrEmpty(vsDetail.ThumbUrl) && vsDetail.ThumbUrl.StartsWith("http"))
                    movieInDb.ThumbUrl = vsDetail.ThumbUrl;

                movieInDb.EpisodeCurrent = vsDetail.EpisodeCurrent;
                movieInDb.EpisodeTotal = vsDetail.EpisodeTotal;
                movieInDb.Status = vsDetail.Status;
                movieInDb.UpdatedAt = DateTime.Now;
            }

            var existingEpisodesCount = movieInDb.Episodes.Count;

            if (detailResponse.Episodes != null)
            {
                var existingKeys = new HashSet<string>(
                    movieInDb.Episodes.Select(e => $"{e.Slug}|{e.ServerName}")
                );

                foreach (var server in detailResponse.Episodes)
                {
                    string serverName = server.ServerName?.Trim() ?? "VSMov";
                    if (server.ServerData == null) continue;

                    foreach (var epData in server.ServerData)
                    {
                        string linkEmbed = epData.LinkEmbed;
                        if (string.IsNullOrEmpty(linkEmbed)) continue;

                        string uniqueKey = $"{epData.Slug}|{serverName}";
                        if (existingKeys.Add(uniqueKey))
                        {
                            wasEpisodesModified = true;
                            movieInDb.Episodes.Add(new DbEpisode
                            {
                                MovieId = movieInDb.MovieId,
                                ServerName = serverName,
                                EpisodeName = epData.Name ?? "1",
                                Slug = epData.Slug ?? "tap-1",
                                LinkM3u8 = linkEmbed
                            });
                        }
                    }
                }
            }

            bool wasUpdatedOrAdded = isNewMovie || wasEpisodesModified;

            if (wasUpdatedOrAdded)
            {
                await _context.SaveChangesAsync();
                SitemapCacheRefreshJob.TriggerImmediately();
                _logger.LogInformation($"[VSMov Sync] ✅ Đã lưu/cập nhật phim '{movieInDb.Name}' với {movieInDb.Episodes.Count} tập!");
            }
            else
            {
                _logger.LogInformation($"[VSMov Sync] ℹ️ Phim '{movieInDb.Name}' đã tồn tại đầy đủ, không có thay đổi.");
            }

            return (movieInDb, wasUpdatedOrAdded);
        }
    }
}
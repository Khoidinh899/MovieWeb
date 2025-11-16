using Microsoft.EntityFrameworkCore;
using MovieWeb.Data;
using MovieWeb.Models.DTOs;
using MovieWeb.Models.Entities;
using MovieWeb.Services.Interfaces;
using System.Collections.Concurrent;
using System.Threading;

namespace MovieWeb.Services
{
    public class WatchHistoryService : IWatchHistoryService
    {
        private readonly MovieWebDbContext _context;
        private readonly ILogger<WatchHistoryService> _logger;

        // Dùng để "khóa" các request, tránh lỗi UNIQUE KEY (Race Condition)
        private static readonly ConcurrentDictionary<string, SemaphoreSlim> _historyLocks
            = new ConcurrentDictionary<string, SemaphoreSlim>();

        public WatchHistoryService(MovieWebDbContext context, ILogger<WatchHistoryService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<WatchHistoryListDto> GetUserHistoryAsync(int userId, int page, int pageSize)
        {
            try
            {
                var query = _context.WatchHistories
                    .Include(w => w.Movie)
                    .Where(w => w.UserId == userId)
                    .OrderByDescending(w => w.LastWatchedAt);

                var totalCount = await query.CountAsync();
                var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

                var histories = await query
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(w => new WatchHistoryMovieDto
                    {
                        HistoryId = w.HistoryId,
                        MovieId = w.MovieId,
                        Slug = w.Movie.Slug,
                        Name = w.Movie.Name,
                        OriginalName = w.Movie.OriginalName,
                        PosterUrl = w.Movie.PosterUrl,
                        Type = w.Movie.Type,
                        Status = w.Movie.Status,
                        EpisodeCurrent = w.Movie.EpisodeCurrent,
                        EpisodeTotal = w.Movie.EpisodeTotal,
                        Quality = w.Movie.Quality,
                        Year = w.Movie.Year,
                        EpisodeNumber = w.EpisodeNumber,
                        WatchedDuration = w.WatchedDuration ?? 0,
                        TotalDuration = w.TotalDuration ?? 0,
                        ProgressPercentage = w.TotalDuration > 0
                            ? (int)((w.WatchedDuration ?? 0) * 100.0 / w.TotalDuration.Value)
                            : 0,
                        IsCompleted = w.IsCompleted ?? false,
                        LastWatchedAt = w.LastWatchedAt ?? DateTime.Now,
                        Rating = w.Movie.Rating ?? 0,
                        ViewCount = w.Movie.ViewCount ?? 0
                    })
                    .ToListAsync();

                return new WatchHistoryListDto
                {
                    Movies = histories,
                    TotalCount = totalCount,
                    CurrentPage = page,
                    PageSize = pageSize,
                    TotalPages = totalPages
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user history");
                throw;
            }
        }

        public async Task<bool> SaveWatchHistoryAsync(int userId, SaveWatchHistoryDto dto)
        {
            var lockKey = $"{userId}-{dto.MovieId}-{dto.EpisodeNumber ?? -1}";
            var semaphore = _historyLocks.GetOrAdd(lockKey, _ => new SemaphoreSlim(1, 1));

            await semaphore.WaitAsync();

            try
            {
                var history = await _context.WatchHistories
                    .FirstOrDefaultAsync(w =>
                        w.UserId == userId &&
                        w.MovieId == dto.MovieId &&
                        w.EpisodeNumber == dto.EpisodeNumber);

                if (history != null)
                {
                    // Update existing entry
                    history.WatchedDuration = dto.WatchedDuration;
                    history.TotalDuration = dto.TotalDuration;
                    history.IsCompleted = dto.IsCompleted;
                    history.LastWatchedAt = DateTime.Now;
                }
                else
                {
                    // Xóa lịch sử tập khác trước khi thêm mới
                    var oldHistories = await _context.WatchHistories
                        .Where(w =>
                            w.UserId == userId &&
                            w.MovieId == dto.MovieId &&
                            w.EpisodeNumber != dto.EpisodeNumber)
                        .ToListAsync();

                    if (oldHistories.Any())
                    {
                        _context.WatchHistories.RemoveRange(oldHistories);
                        _logger.LogInformation($"Đã xóa {oldHistories.Count} lịch sử cũ của phim {dto.MovieId} cho user {userId}.");
                    }

                    // Create new history entry
                    var newHistory = new WatchHistory
                    {
                        UserId = userId,
                        MovieId = dto.MovieId,
                        EpisodeNumber = dto.EpisodeNumber,
                        WatchedDuration = dto.WatchedDuration,
                        TotalDuration = dto.TotalDuration,
                        IsCompleted = dto.IsCompleted,
                        LastWatchedAt = DateTime.Now
                    };

                    _context.WatchHistories.Add(newHistory);
                }

                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lưu lịch sử cho key {LockKey}", lockKey);
                return false;
            }
            finally
            {
                semaphore.Release();
            }
        }

        public async Task<bool> RemoveHistoryAsync(int userId, int historyId)
        {
            try
            {
                var history = await _context.WatchHistories
                    .FirstOrDefaultAsync(w => w.HistoryId == historyId && w.UserId == userId);

                if (history == null)
                    return false;

                _context.WatchHistories.Remove(history);
                await _context.SaveChangesAsync();

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing history");
                return false;
            }
        }

        public async Task<bool> RemoveHistoryByMovieAsync(int userId, int movieId)
{
    try
    {
        var histories = await _context.WatchHistories
            .Where(w => w.UserId == userId && w.MovieId == movieId)
            .ToListAsync();

        if (!histories.Any())
            return false;

        _context.WatchHistories.RemoveRange(histories);
        await _context.SaveChangesAsync();

        _logger.LogInformation($"Removed {histories.Count} history entries for movie {movieId}, user {userId}");
        return true;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error removing history by movie");
        return false;
    }
}

// ⭐ THÊM METHOD: Xóa toàn bộ lịch sử
public async Task<bool> ClearAllHistoryAsync(int userId)
{
    try
    {
        var histories = await _context.WatchHistories
            .Where(w => w.UserId == userId)
            .ToListAsync();

        if (!histories.Any())
            return true;

        _context.WatchHistories.RemoveRange(histories);
        await _context.SaveChangesAsync();

        _logger.LogInformation($"Cleared all history for user {userId} ({histories.Count} entries)");
        return true;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error clearing all history");
        return false;
    }
}
        public async Task<ResumePlaybackDto> GetResumeInfoAsync(int userId, int movieId, int? episodeNumber)
        {
            try
            {
                var history = await _context.WatchHistories
                    .Where(w => w.UserId == userId && w.MovieId == movieId)
                    .Where(w => episodeNumber == null || w.EpisodeNumber == episodeNumber)
                    .OrderByDescending(w => w.LastWatchedAt)
                    .FirstOrDefaultAsync();

                if (history == null)
                    return new ResumePlaybackDto { HasHistory = false };

                return new ResumePlaybackDto
                {
                    HasHistory = true,
                    EpisodeNumber = history.EpisodeNumber,
                    WatchedDuration = history.WatchedDuration ?? 0,
                    TotalDuration = history.TotalDuration ?? 0,
                    ProgressPercentage = history.TotalDuration > 0
                        ? (int)((history.WatchedDuration ?? 0) * 100.0 / history.TotalDuration.Value)
                        : 0,
                    LastWatchedAt = history.LastWatchedAt
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting resume info");
                return new ResumePlaybackDto { HasHistory = false };
            }
        }
    }
}

using MovieWeb.Models.DTOs;

namespace MovieWeb.Services.Interfaces
{
    public interface IWatchHistoryService
    {
        Task<WatchHistoryListDto> GetUserHistoryAsync(int userId, int page, int pageSize);
        Task<bool> SaveWatchHistoryAsync(int userId, SaveWatchHistoryDto dto);
        Task<bool> RemoveHistoryAsync(int userId, int historyId);
        Task<ResumePlaybackDto> GetResumeInfoAsync(int userId, int movieId, int? episodeNumber);
        Task<bool> RemoveHistoryByMovieAsync(int userId, int movieId);
        Task<bool> ClearAllHistoryAsync(int userId);
    }
}
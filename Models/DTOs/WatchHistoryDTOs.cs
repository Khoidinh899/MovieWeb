namespace MovieWeb.Models.DTOs
{
    // DTO để lưu/cập nhật lịch sử xem
    public class SaveWatchHistoryDto
    {
        public int MovieId { get; set; }
        public int? EpisodeNumber { get; set; }
        public int WatchedDuration { get; set; } // Giây
        public int TotalDuration { get; set; } // Giây
        public bool IsCompleted { get; set; }
    }

    // DTO trả về lịch sử xem phim
    public class WatchHistoryMovieDto
    {
        public int HistoryId { get; set; }
        public int MovieId { get; set; }
        public string Slug { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? OriginalName { get; set; }
        public string? PosterUrl { get; set; }
        public string? Type { get; set; }
        public string? Status { get; set; }
        public string? EpisodeCurrent { get; set; }
        public string? EpisodeTotal { get; set; }
        public string? Quality { get; set; }
        public int? Year { get; set; }
        public int? EpisodeNumber { get; set; }
        public int WatchedDuration { get; set; }
        public int TotalDuration { get; set; }
        public int ProgressPercentage { get; set; } // % đã xem
        public bool IsCompleted { get; set; }
        public DateTime LastWatchedAt { get; set; }
        public decimal Rating { get; set; }
        public int ViewCount { get; set; }
    }

    // DTO phân trang lịch sử
    public class WatchHistoryListDto
    {
        public List<WatchHistoryMovieDto> Movies { get; set; } = new();
        public int TotalCount { get; set; }
        public int CurrentPage { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
    }

    // DTO resume playback (lấy vị trí xem gần nhất)
    public class ResumePlaybackDto
    {
        public bool HasHistory { get; set; }
        public int? EpisodeNumber { get; set; }
        public int WatchedDuration { get; set; }
        public int TotalDuration { get; set; }
        public int ProgressPercentage { get; set; }
        public DateTime? LastWatchedAt { get; set; }
    }
}
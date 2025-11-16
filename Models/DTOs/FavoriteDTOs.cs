namespace MovieWeb.Models.DTOs
{
    // DTO để thêm phim vào yêu thích
    public class AddFavoriteDto
    {
        public int MovieId { get; set; }
    }

    // DTO trả về danh sách phim yêu thích
    public class FavoriteMovieDto
    {
        public int FavoriteId { get; set; }
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
        public DateTime CreatedAt { get; set; }
        public decimal Rating { get; set; }
        public int ViewCount { get; set; }
    }

    // DTO phân trang
    public class FavoriteListDto
    {
        public List<FavoriteMovieDto> Movies { get; set; } = new();
        public int TotalCount { get; set; }
        public int CurrentPage { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
    }

    // DTO kiểm tra phim đã favorite chưa
    public class CheckFavoriteDto
    {
        public bool IsFavorited { get; set; }
        public int? FavoriteId { get; set; }
    }
}
// Models/DTOs/CreateMovieDto.cs
using System.ComponentModel.DataAnnotations;

namespace MovieWeb.Models.DTOs
{
    public class CreateMovieDto
    {
        [Required(ErrorMessage = "Slug là bắt buộc")]
        [StringLength(255)]
        public string Slug { get; set; }

        [Required(ErrorMessage = "Tên phim là bắt buộc")]
        [StringLength(500)]
        public string Name { get; set; }

        [StringLength(500)]
        public string? OriginalName { get; set; }

        public string? Content { get; set; }

        [StringLength(50)]
        public string? Type { get; set; } = "single"; // single, series, hoathinh, tvshows

        [StringLength(50)]
        public string? Status { get; set; } = "completed"; // completed, ongoing, trailer

        [StringLength(500)]
        public string? PosterUrl { get; set; } // ❌ không Required

        [StringLength(500)]
        public string? ThumbUrl { get; set; } // ❌ không Required

        [StringLength(500)]
        public string? TrailerUrl { get; set; }

        [StringLength(50)]
        public string? Time { get; set; }

        [StringLength(50)]
        public string? EpisodeCurrent { get; set; }

        [StringLength(50)]
        public string? EpisodeTotal { get; set; }

        [StringLength(50)]
        public string? Quality { get; set; } = "HD";

        [StringLength(50)]
        public string? Language { get; set; } = "Vietsub";

        [Range(1900, 2100)]
        public int? Year { get; set; }

        public bool IsRecommended { get; set; } = false;

        public bool IsActive { get; set; } = true;

        // Các trường liên kết (comma-separated IDs hoặc names)
        public string? CategoryIds { get; set; } // "1,2,3"
        public string? CountryIds { get; set; }
        public string? ActorNames { get; set; } // "Actor 1,Actor 2"
        public string? DirectorNames { get; set; }
    }

    public class UpdateMovieDto
    {
        [Required]
        public int MovieId { get; set; }

        [Required(ErrorMessage = "Slug là bắt buộc")]
        [StringLength(255)]
        public string Slug { get; set; }

        [Required(ErrorMessage = "Tên phim là bắt buộc")]
        [StringLength(500)]
        public string Name { get; set; }

        [StringLength(500)]
        public string? OriginalName { get; set; }

        public string? Content { get; set; }

        [StringLength(50)]
        public string? Type { get; set; }

        [StringLength(50)]
        public string? Status { get; set; }

        [StringLength(500)]
        public string? PosterUrl { get; set; } // ❌ không Required

        [StringLength(500)]
        public string? ThumbUrl { get; set; } // ❌ không Required

        [StringLength(500)]
        public string? TrailerUrl { get; set; }

        [StringLength(50)]
        public string? Time { get; set; }

        [StringLength(50)]
        public string? EpisodeCurrent { get; set; }

        [StringLength(50)]
        public string? EpisodeTotal { get; set; }

        [StringLength(50)]
        public string? Quality { get; set; }

        [StringLength(50)]
        public string? Language { get; set; }

        [Range(1900, 2100)]
        public int? Year { get; set; }

        public bool IsRecommended { get; set; }

        public bool IsActive { get; set; }

        public string? CategoryIds { get; set; }
        public string? CountryIds { get; set; }
        public string? ActorNames { get; set; }
        public string? DirectorNames { get; set; }
    }

    public class AdminMovieListDto
    {
        public int MovieId { get; set; }
        public string Slug { get; set; }
        public string Name { get; set; }
        public string? OriginalName { get; set; }
        public string? PosterUrl { get; set; }
        public string? Type { get; set; }
        public string? Status { get; set; }
        public string? Quality { get; set; }
        public int? Year { get; set; }
        public int ViewCount { get; set; }
        public decimal Rating { get; set; }
        public int RatingCount { get; set; }
        public bool IsRecommended { get; set; }
        public bool IsBanner { get; set; }

        public bool IsActive { get; set; }
        public bool IsManual { get; set; } // ApiId == null
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public int TotalComments { get; set; }
        public int TotalFavorites { get; set; }

        public List<string> Categories { get; set; } = new();
        public List<string> Countries { get; set; } = new();
    }
}

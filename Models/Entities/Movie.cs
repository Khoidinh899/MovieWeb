using System;
using System.Collections.Generic;

namespace MovieWeb.Models.Entities;

public partial class Movie
{
    public int MovieId { get; set; }

    public string? ApiId { get; set; }

    public string Slug { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string? OriginalName { get; set; }

    public string? Content { get; set; }

    public string? Type { get; set; }

    public string? Status { get; set; }

    public string? PosterUrl { get; set; }

    public string? ThumbUrl { get; set; }

    public string? TrailerUrl { get; set; }

    public string? Time { get; set; }

    public string? EpisodeCurrent { get; set; }

    public string? EpisodeTotal { get; set; }

    public string? Quality { get; set; }

    public string? Language { get; set; }

    public int? Year { get; set; }

    public int? ViewCount { get; set; }

    public decimal? Rating { get; set; }

    public int? RatingCount { get; set; }

    public bool? IsCopyright { get; set; }

    public bool? IsRecommended { get; set; }

    public bool? IsActive { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual ICollection<Comment> Comments { get; set; } = new List<Comment>();

    public virtual ICollection<Favorite> Favorites { get; set; } = new List<Favorite>();

    public virtual ICollection<Rating> Ratings { get; set; } = new List<Rating>();

    public virtual ICollection<WatchHistory> WatchHistories { get; set; } = new List<WatchHistory>();

    public virtual ICollection<Actor> Actors { get; set; } = new List<Actor>();

    public virtual ICollection<Category> Categories { get; set; } = new List<Category>();

    public virtual ICollection<Country> Countries { get; set; } = new List<Country>();

    public virtual ICollection<Director> Directors { get; set; } = new List<Director>();
}

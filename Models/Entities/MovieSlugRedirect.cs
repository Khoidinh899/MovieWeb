using System;

namespace MovieWeb.Models.Entities;

/// <summary>Slug (URL) cũ của phim sau khi admin đổi đường dẫn: /phim/{OldSlug} được 301 sang /phim/{Movie.Slug}.</summary>
public class MovieSlugRedirect
{
    public string OldSlug { get; set; } = null!;

    public int MovieId { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Movie Movie { get; set; } = null!;
}

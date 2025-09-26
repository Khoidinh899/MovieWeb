using System;
using System.Collections.Generic;

namespace MovieWeb.Models.Entities;

public partial class Director
{
    public int DirectorId { get; set; }

    public string Name { get; set; } = null!;

    public string Slug { get; set; } = null!;

    public string? Avatar { get; set; }

    public string? Biography { get; set; }

    public bool? IsActive { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual ICollection<Movie> Movies { get; set; } = new List<Movie>();
}

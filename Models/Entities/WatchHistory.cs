using System;
using System.Collections.Generic;

namespace MovieWeb.Models.Entities;

public partial class WatchHistory
{
    public int HistoryId { get; set; }

    public int UserId { get; set; }

    public int MovieId { get; set; }

    public int? EpisodeNumber { get; set; }

    public int? WatchedDuration { get; set; }

    public int? TotalDuration { get; set; }

    public bool? IsCompleted { get; set; }

    public DateTime? LastWatchedAt { get; set; }

    public virtual Movie Movie { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}

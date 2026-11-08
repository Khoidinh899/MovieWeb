using System;
using System.Collections.Generic;

namespace MovieWeb.Models.Entities;

/// <summary>History of admin-sent notification broadcasts.</summary>
public class AdminBroadcast
{
    public int Id { get; set; }

    public string Title { get; set; } = null!;

    public string Content { get; set; } = null!;

    public string? StyleJson { get; set; }

    public string? Url { get; set; }

    /// <summary>"all" or "selected".</summary>
    public string TargetType { get; set; } = "all";

    public int RecipientCount { get; set; }

    public bool SendPush { get; set; }

    /// <summary>"queued", "sending", "done", "failed".</summary>
    public string Status { get; set; } = "queued";

    public int? CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();
}

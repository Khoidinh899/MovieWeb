using System;

namespace MovieWeb.Models.Entities;

/// <summary>Reusable notification design created by admins.</summary>
public class NotificationTemplate
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public string Title { get; set; } = null!;

    public string Content { get; set; } = null!;

    /// <summary>Validated JSON: icon, accent, imageUrl, buttonText, url, badge.</summary>
    public string? StyleJson { get; set; }

    public int? CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}

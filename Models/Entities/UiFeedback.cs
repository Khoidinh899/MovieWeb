using System;

namespace MovieWeb.Models.Entities;

/// <summary>One survey vote per member, editable.</summary>
public class UiFeedback
{
    public int UserId { get; set; }

    /// <summary>"new", "old" or "same".</summary>
    public string Choice { get; set; } = null!;

    /// <summary>Plain text, encoded on output.</summary>
    public string? Comment { get; set; }

    /// <summary>UI the member was using when voting ("v1"/"v2").</summary>
    public string UiAtVote { get; set; } = "v1";

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual User User { get; set; } = null!;
}

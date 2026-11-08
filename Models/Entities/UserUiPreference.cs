using System;

namespace MovieWeb.Models.Entities;

/// <summary>Created only when a member explicitly picks a UI. No row = use the default for the current rollout mode.</summary>
public class UserUiPreference
{
    public int UserId { get; set; }

    /// <summary>"v1" or "v2".</summary>
    public string Ui { get; set; } = "v1";

    public int SwitchCount { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual User User { get; set; } = null!;
}

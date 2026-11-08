using System;

namespace MovieWeb.Models.DTOs;

/// <summary>
/// Controls the UI rollout mode: "off" | "members" | "all".
/// Stored in SiteSettings table under key "ui.rollout".
/// </summary>
public class UiRolloutSetting
{
    public string Mode { get; set; } = "off";
    public DateTime? UpdatedAt { get; set; }
    public int? UpdatedBy { get; set; }
}

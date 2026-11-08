using System;

namespace MovieWeb.Models.Entities;

/// <summary>Key-value settings stored in DB (survives container redeploys, unlike App_Data).</summary>
public class SiteSetting
{
    public string Key { get; set; } = null!;

    public string? Value { get; set; }

    public DateTime UpdatedAt { get; set; }
}

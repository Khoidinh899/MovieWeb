using System.Text.RegularExpressions;

namespace MovieWeb.Helpers;

/// <summary>
/// Chuỗi tiến độ tập cho giao diện V2: "Hoàn tất 12/12", "Tập 8/12", "Full"... Hàm thuần, chỉ đọc EpisodeCurrent/EpisodeTotal đã có.
/// </summary>
public static class EpisodeStatus
{
    public static string? Format(string? current, string? total)
    {
        var cur = current?.Trim();
        var tot = total?.Trim();
        if (string.IsNullOrEmpty(cur)) return string.IsNullOrEmpty(tot) ? null : $"{tot} tập";
        if (cur.Contains('/')) return cur;

        var totalNumber = tot is not null && Regex.IsMatch(tot, @"^\d+$") ? tot : null;
        var done = cur.Contains("Hoàn tất", StringComparison.OrdinalIgnoreCase);
        var full = cur.Contains("Full", StringComparison.OrdinalIgnoreCase);

        if (done) return totalNumber is not null && totalNumber != "1" ? $"Hoàn tất {totalNumber}/{totalNumber}" : cur;
        if (full) return cur;

        var m = Regex.Match(cur, @"\d+");
        if (m.Success && totalNumber is not null && totalNumber != "1") return $"Tập {m.Value}/{totalNumber}";
        return cur;
    }
}

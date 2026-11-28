using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using MovieWeb.Models.Entities;

namespace MovieWeb.Helpers;

/// <summary>Bình luận rút gọn để server in sẵn vào HTML (Google đọc được nội dung do người xem viết). Chỉ chứa tên công khai.</summary>
public sealed record MovieCommentSnippet(string UserName, string Content, DateTime? CreatedAt, int? Rating);

/// <summary>Nội dung SEO riêng của từng trang phim, ghép từ dữ liệu thật (không chèn câu chung chung khi thiếu dữ liệu).</summary>
public sealed class MovieSeoContent
{
    public string Intro { get; init; } = "";
    public List<(string Question, string Answer)> Faq { get; init; } = [];
    public string? FaqSchemaJson { get; init; }
    public List<Movie> OtherParts { get; init; } = [];
    public string? SeriesBaseName { get; init; }
    public List<Movie> SameActorMovies { get; init; } = [];
    public string? SameActorNames { get; init; }
    public List<MovieCommentSnippet> RecentComments { get; init; } = [];
}

public static partial class MovieSeoContentBuilder
{
    /// <summary>Tên gốc của loạt phim nhiều phần: "Gương đen - Phần 2" → "Gương đen". Null nếu không phải phim nhiều phần.</summary>
    public static string? SeriesBaseName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        var m = PartSuffix().Match(name);
        if (!m.Success) return null;
        var baseName = m.Groups["base"].Value.Trim().TrimEnd('-', ':', '–', ' ');
        return baseName.Length >= 2 ? baseName : null;
    }

    public static int PartNumber(string? name)
    {
        var m = PartSuffix().Match(name ?? "");
        return m.Success && int.TryParse(m.Groups["num"].Value, out var n) ? n : 0;
    }

    /// <summary>Các bản ngôn ngữ thật có trên trang (theo tên server phát).</summary>
    public static List<string> AvailableLanguages(IEnumerable<string> serverNames)
    {
        var names = serverNames.Select(s => s.ToLowerInvariant()).ToList();
        var result = new List<string>();
        if (names.Any(n => n.Contains("vietsub"))) result.Add("Vietsub");
        if (names.Any(n => n.Contains("thuyết minh") || n.Contains("thuyet minh"))) result.Add("Thuyết minh");
        if (names.Any(n => n.Contains("lồng tiếng") || n.Contains("long tieng"))) result.Add("Lồng tiếng");
        return result;
    }

    public static MovieSeoContent Build(
        Movie movie,
        int availableEpisodeCount,
        IEnumerable<string> serverNames,
        List<Movie> otherParts,
        List<Movie> sameActorMovies,
        List<MovieCommentSnippet> recentComments,
        string canonicalUrl)
    {
        var name = movie.Name.Trim();
        var original = !string.IsNullOrWhiteSpace(movie.OriginalName) &&
                       !movie.OriginalName.Trim().Equals(name, StringComparison.OrdinalIgnoreCase)
            ? movie.OriginalName.Trim() : null;
        var countries = movie.Countries.Select(c => c.Name).Where(n => !string.IsNullOrWhiteSpace(n)).Take(2).ToList();
        var categories = movie.Categories.Select(c => c.Name).Where(n => !string.IsNullOrWhiteSpace(n)).Take(4).ToList();
        var actors = movie.Actors.Select(a => a.Name).Where(n => !string.IsNullOrWhiteSpace(n)).Take(5).ToList();
        var directors = movie.Directors.Select(d => d.Name).Where(n => !string.IsNullOrWhiteSpace(n)).Take(2).ToList();
        var languages = AvailableLanguages(serverNames);
        var type = movie.Type?.ToLowerInvariant();
        var isSingle = type == "single";
        var isAnimation = type is "hoathinh" or "animation";
        var completed = string.Equals(movie.Status, "completed", StringComparison.OrdinalIgnoreCase);
        var isTrailer = string.Equals(movie.Status, "trailer", StringComparison.OrdinalIgnoreCase);
        var duration = FormatDuration(movie.Time);
        // Tổng số tập: ưu tiên "Hoàn Tất (10/10)" trong EpisodeCurrent (cột EpisodeTotal của nguồn hay lệch), rồi mới tới EpisodeTotal.
        var progress = EpisodeProgress().Match(movie.EpisodeCurrent ?? "");
        int totalEpisodes = progress.Success && int.TryParse(progress.Groups[2].Value, out var t1) ? t1
            : int.TryParse(movie.EpisodeTotal, out var t2) ? t2 : 0;
        if (completed && availableEpisodeCount > totalEpisodes) totalEpisodes = availableEpisodeCount;
        var kind = isSingle ? "phim lẻ" : isAnimation ? "phim hoạt hình" : type == "tvshows" ? "chương trình truyền hình" : "phim bộ";
        var baseName = SeriesBaseName(name);

        // ---- Đoạn giới thiệu (chỉ câu có dữ liệu) ----
        var intro = new List<string>();
        var first = $"{name}{(original != null ? $" ({original})" : "")} là {kind}";
        if (countries.Count > 0) first += $" {string.Join(" - ", countries)}";
        if (movie.Year is > 1900) first += $" ra mắt năm {movie.Year}";
        if (categories.Count > 0) first += $", thuộc thể loại {JoinVi(categories).ToLower(CultureInfo.GetCultureInfo("vi-VN"))}";
        intro.Add(first + ".");
        if (directors.Count > 0 && actors.Count > 0)
            intro.Add($"Phim do {JoinVi(directors)} đạo diễn, với sự tham gia của {JoinVi(actors)}.");
        else if (actors.Count > 0)
            intro.Add($"Phim có sự tham gia của {JoinVi(actors)}.");
        else if (directors.Count > 0)
            intro.Add($"Phim do {JoinVi(directors)} đạo diễn.");

        var status = EpisodeStatusSentence(isSingle, isTrailer, completed, availableEpisodeCount, totalEpisodes, duration);
        if (status != null) intro.Add(status);
        if (languages.Count > 0)
            intro.Add($"Trên MoonPhim, phim có bản {JoinVi(languages)}{(string.IsNullOrWhiteSpace(movie.Quality) ? "" : $", chất lượng {movie.Quality}")}.");
        if (otherParts.Count > 0 && baseName != null)
            intro.Add($"Đây là một phần trong loạt phim {baseName}; MoonPhim hiện có {otherParts.Count + 1} phần.");

        // ---- Hỏi đáp (chỉ câu trả lời có dữ liệu thật) ----
        var faq = new List<(string, string)>();
        if (!isTrailer)
        {
            if (isSingle)
            {
                if (duration != null)
                    faq.Add(($"Phim {name} dài bao nhiêu phút?", $"{name} là phim lẻ, thời lượng khoảng {duration}."));
            }
            else if (completed && (totalEpisodes > 0 || availableEpisodeCount > 0))
            {
                var n = totalEpisodes > 0 ? totalEpisodes : availableEpisodeCount;
                faq.Add(($"Phim {name} có bao nhiêu tập?", $"{name} đã hoàn tất với {n} tập{(duration != null ? $", mỗi tập khoảng {duration}" : "")}."));
            }
            else if (availableEpisodeCount > 0)
            {
                faq.Add(($"Phim {name} có bao nhiêu tập, đã hoàn tất chưa?",
                    $"{name} đang cập nhật, hiện có {availableEpisodeCount} tập{(totalEpisodes > availableEpisodeCount ? $" trên tổng số {totalEpisodes} tập dự kiến" : "")}. Tập mới được cập nhật ngay khi phát sóng."));
            }

            if (languages.Count > 0)
            {
                var hasDub = languages.Contains("Thuyết minh") || languages.Contains("Lồng tiếng");
                faq.Add(($"Phim {name} có thuyết minh, lồng tiếng không?", hasDub
                    ? $"Có. {name} trên MoonPhim có các bản {JoinVi(languages)}, bạn chọn ở mục nguồn phát."
                    : $"Hiện {name} có bản Vietsub. Bản thuyết minh hoặc lồng tiếng sẽ được bổ sung khi có nguồn."));
            }
        }

        if (countries.Count > 0 || movie.Year is > 1900)
        {
            var parts = new List<string>();
            if (countries.Count > 0) parts.Add($"là phim {string.Join(" - ", countries)}");
            if (movie.Year is > 1900) parts.Add($"ra mắt năm {movie.Year}");
            faq.Add(($"{name} là phim nước nào, ra mắt năm nào?", $"{name} {string.Join(", ", parts)}."));
        }

        if (actors.Count > 0)
            faq.Add(($"Ai đóng phim {name}?", $"Diễn viên chính gồm {JoinVi(actors)}{(directors.Count > 0 ? $"; đạo diễn {JoinVi(directors)}" : "")}."));

        if (otherParts.Count > 0 && baseName != null)
        {
            var all = otherParts.Append(movie).OrderBy(m => PartNumber(m.Name)).Select(m => m.Name.Trim()).ToList();
            faq.Add(($"{baseName} có mấy phần?", $"MoonPhim hiện có {all.Count} phần: {JoinVi(all)}."));
        }

        string? faqSchema = null;
        if (faq.Count >= 2)
        {
            faqSchema = JsonSerializer.Serialize(new Dictionary<string, object>
            {
                ["@context"] = "https://schema.org",
                ["@type"] = "FAQPage",
                ["@id"] = canonicalUrl + "#faq",
                ["mainEntity"] = faq.Select(f => new Dictionary<string, object>
                {
                    ["@type"] = "Question",
                    ["name"] = f.Item1,
                    ["acceptedAnswer"] = new Dictionary<string, object> { ["@type"] = "Answer", ["text"] = f.Item2 }
                }).ToList()
            }, new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping })
            // Chặn đóng thẻ <script> sớm nếu tên phim chứa "</"
            .Replace("</", "<\\/");
        }

        return new MovieSeoContent
        {
            Intro = string.Join(" ", intro),
            Faq = faq,
            FaqSchemaJson = faqSchema,
            OtherParts = otherParts.OrderBy(m => PartNumber(m.Name)).ToList(),
            SeriesBaseName = baseName,
            SameActorMovies = sameActorMovies,
            SameActorNames = actors.Count > 0 ? JoinVi(actors.Take(2).ToList()) : null,
            RecentComments = recentComments
        };
    }

    private static string? EpisodeStatusSentence(bool isSingle, bool isTrailer, bool completed, int available, int total, string? duration)
    {
        if (isTrailer) return "Phim chưa phát hành, hiện mới có trailer; MoonPhim sẽ cập nhật ngay khi có bản chiếu.";
        if (isSingle) return duration != null ? $"Phim có thời lượng khoảng {duration}." : null;
        if (completed && (total > 0 || available > 0))
            return $"Phim đã hoàn tất với {(total > 0 ? total : available)} tập{(duration != null ? $", mỗi tập khoảng {duration}" : "")}.";
        if (available > 0)
            return $"Phim đang phát sóng, hiện có {available} tập{(total > available ? $"/{total} tập" : "")}.";
        return null;
    }

    private static string? FormatDuration(string? time)
    {
        if (string.IsNullOrWhiteSpace(time)) return null;
        var t = WebUtility.HtmlDecode(time).Trim();
        if (int.TryParse(t, out var minutes)) return minutes is > 0 and < 1000 ? $"{minutes} phút" : null;
        t = t.Replace("/tập", "").Replace("/ tập", "").Trim();
        return Regex.IsMatch(t, @"\d") && t.Length <= 30 ? t : null;
    }

    private static string JoinVi(IReadOnlyList<string> items) => items.Count switch
    {
        0 => "",
        1 => items[0],
        _ => string.Join(", ", items.Take(items.Count - 1)) + " và " + items[^1]
    };

    [GeneratedRegex(@"\((\d+)\s*/\s*(\d+)\)")]
    private static partial Regex EpisodeProgress();

    [GeneratedRegex(@"^(?<base>.+?)\s*[-:–(]?\s*\(?\s*(?:Phần|Season|Mùa)\s*(?<num>\d+)\s*\)?\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex PartSuffix();
}

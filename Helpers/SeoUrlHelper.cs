using System.Text.RegularExpressions;

namespace MovieWeb.Helpers;

public static partial class SeoUrlHelper
{
    /// <summary>
    /// Chuẩn hóa slug tập phim: loại bỏ tiền tố "tap-" nếu đã có để không bị lặp "tap-tap-1".
    /// </summary>
    public static string NormalizeEpisodeSlug(string? slug)
    {
        if (string.IsNullOrWhiteSpace(slug)) return "1";
        slug = slug.Trim();
        if (slug.StartsWith("tap-", StringComparison.OrdinalIgnoreCase))
        {
            slug = slug[4..];
        }
        return string.IsNullOrWhiteSpace(slug) ? "1" : slug.ToLowerInvariant();
    }

    /// <summary>
    /// Sinh URL chuẩn cho trang thông tin phim: /phim/{slug}.
    /// Giữ nguyên URL Google đã index (slug phim là duy nhất); movieId giữ trong chữ ký để không đổi nơi gọi.
    /// </summary>
    public static string MovieUrl(string movieSlug, int movieId)
    {
        var cleanSlug = (movieSlug ?? "phim").Trim().Trim('/');
        return $"/phim/{cleanSlug}";
    }

    /// <summary>
    /// Sinh URL chuẩn cho trang xem tập phim: /phim/{slug}/tap-{episode_slug}-{minEpisodeId}
    /// </summary>
    public static string EpisodeUrl(string movieSlug, string episodeSlug, int minEpisodeId)
    {
        var cleanMovie = (movieSlug ?? "phim").Trim().Trim('/');
        var cleanEp = NormalizeEpisodeSlug(episodeSlug);
        return $"/phim/{cleanMovie}/tap-{cleanEp}-{minEpisodeId}";
    }

    /// <summary>
    /// Phân tích segment phim xem là dạng mới /phim/{slug}-{id} hay dạng cũ /phim/{slug}.
    /// </summary>
    public static bool TryParseMovieSegment(string segment, out string slug, out int? id)
    {
        slug = segment;
        id = null;
        if (string.IsNullOrWhiteSpace(segment)) return false;

        var match = MovieSlugRegex().Match(segment);
        if (match.Success)
        {
            slug = match.Groups[1].Value;
            if (int.TryParse(match.Groups[2].Value, out var parsedId))
            {
                id = parsedId;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Phân tích segment tập xem là dạng mới tap-{episode_slug}-{episode_id} hay dạng cũ {episodeSlug}.
    /// </summary>
    public static bool TryParseEpisodeSegment(string segment, out string cleanSlug, out int? episodeId)
    {
        cleanSlug = segment;
        episodeId = null;
        if (string.IsNullOrWhiteSpace(segment)) return false;

        var match = EpisodeSlugRegex().Match(segment);
        if (match.Success)
        {
            cleanSlug = match.Groups[1].Value;
            if (int.TryParse(match.Groups[2].Value, out var parsedId))
            {
                episodeId = parsedId;
                return true;
            }
        }

        cleanSlug = NormalizeEpisodeSlug(segment);
        return false;
    }

    [GeneratedRegex(@"^(.+)-(\d+)$")]
    private static partial Regex MovieSlugRegex();

    [GeneratedRegex(@"^tap-(.+)-(\d+)$", RegexOptions.IgnoreCase)]
    private static partial Regex EpisodeSlugRegex();
}

using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;

namespace MovieWeb.Helpers;

public static partial class SeoMetadata
{
    public const string BaseUrl = "https://moonphim.me";

    public static string Canonical(HttpRequest request)
    {
        var path = request.Path.ToUriComponent();
        if (string.IsNullOrEmpty(path)) path = "/";
        if (path == "/phim-moi") path = "/the-loai/phim-moi-cap-nhat";
        if (path == "/the-loai/phim-moi") path = "/the-loai/phim-moi-cap-nhat";
        if (path == "/the-loai/phim-trending") path = "/phim-trending";
        var url = BaseUrl + path;
        // Episode and tracking parameters do not create a different movie page.
        if (request.Path.StartsWithSegments("/phim")) return url;
        foreach (var item in request.Query.OrderBy(q => q.Key, StringComparer.Ordinal))
        {
            if (IsTracking(item.Key)) continue;
            if (item.Key.Equals("page", StringComparison.OrdinalIgnoreCase)
                && int.TryParse(item.Value.ToString(), out var page))
            {
                if (page <= 1) continue;
                url = QueryHelpers.AddQueryString(url, "page", page.ToString(CultureInfo.InvariantCulture));
                continue;
            }
            foreach (var value in item.Value)
                url = QueryHelpers.AddQueryString(url, item.Key, value ?? "");
        }
        return url;
    }

    public static bool NoIndex(HttpRequest request)
    {
        var path = request.Path;
        if (new[] { "/tim-kiem", "/user", "/auth", "/account", "/admin", "/payment", "/hangfire", "/watchparty" }
            .Any(prefix => path.StartsWithSegments(prefix, StringComparison.OrdinalIgnoreCase))) return true;
        // Keep paginated catalogs indexable; arbitrary filter combinations are not landing pages.
        var catalog = path.StartsWithSegments("/the-loai") || path.StartsWithSegments("/quoc-gia");
        return catalog && request.Query.Any(q => !IsTracking(q.Key)
            && !q.Key.Equals("page", StringComparison.OrdinalIgnoreCase));
    }

    public static string MovieTitle(string name, string? originalName, int? year, string? language)
    {
        var original = !string.IsNullOrWhiteSpace(originalName)
            && !originalName.Equals(name, StringComparison.OrdinalIgnoreCase) ? $" - {originalName}" : "";
        return $"{name}{original}{(year.HasValue ? $" ({year})" : "")}{(string.IsNullOrWhiteSpace(language) ? "" : $" | {language}")} | MoonPhim";
    }

    public static string Description(string? description, string? content, string fallback)
    {
        var source = string.IsNullOrWhiteSpace(description) ? content : description;
        var text = WebUtility.HtmlDecode(Tags().Replace(source ?? "", " "));
        text = Spaces().Replace(text, " ").Trim();
        if (string.IsNullOrEmpty(text)) text = fallback;
        if (text.Length <= 180) return text;
        var end = text.LastIndexOf(' ', 177);
        return text[..(end > 100 ? end : 177)].TrimEnd() + "…";
    }

    public static bool IsTrailer(string? source) => Uri.TryCreate(source, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps && uri.IsDefaultPort && string.IsNullOrEmpty(uri.UserInfo)
        && new[] { "youtube.com", "www.youtube.com", "youtu.be", "vimeo.com", "www.vimeo.com" }
            .Contains(uri.Host, StringComparer.OrdinalIgnoreCase)
        && uri.AbsolutePath != "/";

    private static bool IsTracking(string key) => key.StartsWith("utm_", StringComparison.OrdinalIgnoreCase)
        || new[] { "gclid", "fbclid", "msclkid" }.Contains(key, StringComparer.OrdinalIgnoreCase);

    [GeneratedRegex(@"<[^>]*>")]
    private static partial Regex Tags();
    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();
}

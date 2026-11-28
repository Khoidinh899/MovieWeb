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
        // Một trang chỉ có một URL chuẩn: bỏ "/" cuối, gộp các route MVC mặc định về route công khai.
        if (path.Length > 1) path = path.TrimEnd('/');
        path = AliasPath(path) ?? path;
        // SEO v3: mỗi phim chỉ một URL chính tắc. Trang tập /phim/{slug}/tap-... (để xem phim) trỏ canonical về /phim/{slug}.
        if (path.StartsWith("/phim/", StringComparison.OrdinalIgnoreCase))
        {
            var slugEnd = path.IndexOf('/', "/phim/".Length);
            if (slugEnd > 0) path = path[..slugEnd];
            return BaseUrl + path;
        }
        var url = BaseUrl + path;
        // Chỉ tham số phân trang tạo trang riêng; mọi tham số khác (bộ lọc, sắp xếp, ?url=, tracking…) không phải URL chính tắc.
        var pageValue = request.Query["page"].ToString();
        if (int.TryParse(pageValue, out var page) && page > 1)
            url = QueryHelpers.AddQueryString(url, "page", page.ToString(CultureInfo.InvariantCulture));
        return url;
    }

    /// <summary>
    /// Đường dẫn MVC mặc định trùng nội dung với route công khai (vd /TrangChu = /trang-chu, /Home/Index = /).
    /// Trả về URL chuẩn, hoặc null nếu không phải alias.
    /// </summary>
    public static string? AliasPath(string path) => path.TrimEnd('/').ToLowerInvariant() switch
    {
        "/home" or "/home/index" => "/",
        "/trangchu" or "/trangchu/index" => "/trang-chu",
        "/phim-moi" or "/the-loai/phim-moi" => "/the-loai/phim-moi-cap-nhat",
        "/the-loai/phim-trending" => "/phim-trending",
        _ => null
    };

    public static bool NoIndex(HttpRequest request)
    {
        var path = request.Path;
        if (new[] { "/tim-kiem", "/user", "/auth", "/account", "/admin", "/payment", "/hangfire", "/watchparty", "/home/error" }
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

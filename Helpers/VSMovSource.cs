using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace MovieWeb.Helpers;

public static partial class VSMovSource
{
    public const string ImageBaseUrl = "https://vsmov.com/storage/images/";

    public static string NormalizeServer(string? name) =>
        Whitespace().Replace((name ?? "VSMov").Trim(), " ").ToLowerInvariant();

    public static Dictionary<string, string> ServerUrlKeys(IEnumerable<string> servers)
    {
        string Slug(string name)
        {
            var decomposed = NormalizeServer(name).Replace('đ', 'd').Normalize(NormalizationForm.FormD);
            var plain = new string(decomposed.Where(c => CharUnicodeInfo.GetUnicodeCategory(c)
                != UnicodeCategory.NonSpacingMark).ToArray());
            return Regex.Replace(plain, "[^a-z0-9]+", "-").Trim('-') is { Length: > 0 } key ? key : "server";
        }

        var names = servers.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        var result = names.ToDictionary(name => name, Slug, StringComparer.Ordinal);
        // Distinct labels may lose the same accents/punctuation; never map them to one server.
        var collisions = result.GroupBy(pair => pair.Value).Where(group => group.Count() > 1)
            .SelectMany(group => group.Select(pair => pair.Key)).ToList();
        foreach (var name in collisions)
            result[name] += "-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(name))).ToLowerInvariant();
        return result;
    }

    public static bool IsSupported(string? source) => GetPlaybackUrl(source) is not null;

    /// <summary>Server chứa link admin nhập tay trong form sửa phim. Đồng bộ VSMov không ghi đè/xóa server này.</summary>
    public const string ManualServerName = "MoonPhim HD";

    public static bool IsManualServer(string? serverName) =>
        NormalizeServer(serverName) == NormalizeServer(ManualServerName);

    /// <summary>Link admin nhập tay: chỉ nhận URL https tuyệt đối (m3u8, mp4 hoặc trang nhúng).</summary>
    public static bool IsSafeManualUrl(string? source) =>
        Uri.TryCreate(source, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps
        && string.IsNullOrEmpty(uri.UserInfo) && source!.Length <= 1000;

    /// <summary>Tập phát được: nguồn VSMov hợp lệ, hoặc link nhập tay trên server MoonPhim HD.</summary>
    public static bool IsPlayable(string? source, string? serverName) =>
        IsSupported(source) || (IsManualServer(serverName) && IsSafeManualUrl(source));

    /// <summary>URL đưa vào player cho một tập: nguồn VSMov đã kiểm tra, hoặc link nhập tay của server MoonPhim HD.</summary>
    public static string? GetEpisodePlaybackUrl(string? source, string? serverName) =>
        GetPlaybackUrl(source) ?? (IsManualServer(serverName) && IsSafeManualUrl(source) ? source!.Trim() : null);

    public static string? GetPlaybackUrl(string? source)
    {
        if (!Uri.TryCreate(source, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps
            || !uri.Host.EndsWith(".streamvsmov.com", StringComparison.OrdinalIgnoreCase)
            || !uri.IsDefaultPort || !string.IsNullOrEmpty(uri.UserInfo)) return null;

        if (uri.AbsolutePath.EndsWith(".m3u8", StringComparison.OrdinalIgnoreCase))
            return uri.AbsoluteUri;

        var parts = uri.AbsolutePath.Trim('/').Split('/');
        if (parts.Length != 2 || parts[0] != "video" || !Guid.TryParse(parts[1], out var videoId))
            return null;

        // Keep the supported embed URL. Its internal stream is not an integration API.
        return uri.AbsoluteUri;
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}

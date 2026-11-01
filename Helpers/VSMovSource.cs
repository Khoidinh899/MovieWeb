using System.Text.RegularExpressions;

namespace MovieWeb.Helpers;

public static partial class VSMovSource
{
    public const string ImageBaseUrl = "https://vsmov.com/storage/images/";

    public static string NormalizeServer(string? name) =>
        Whitespace().Replace((name ?? "VSMov").Trim(), " ").ToLowerInvariant();

    public static bool IsSupported(string? source) => GetPlaybackUrl(source) is not null;

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

using System;
using System.Linq;

namespace MovieWeb.Helpers
{
    public static class ImageHelper
    {
        // ✅ Domain ảnh của VSMov
        private static readonly string ApiImageBaseUrl = "https://vsmov.com/storage/images/";

        public static string GetPoster(string? posterFileName)
        {
            if (string.IsNullOrWhiteSpace(posterFileName))
                return "/images/default-poster.jpg";

            if (posterFileName.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                posterFileName.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return posterFileName;
            }

            return $"{ApiImageBaseUrl}{posterFileName.TrimStart('/')}";
        }

        /// <summary>
        /// Lấy URL đầy đủ cho thumbnail phim
        /// </summary>
        public static string GetThumb(string? thumbFileName)
        {
            if (string.IsNullOrWhiteSpace(thumbFileName))
                return "/images/default-poster.jpg";

            if (thumbFileName.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                thumbFileName.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return thumbFileName;
            }

            return $"{ApiImageBaseUrl}{thumbFileName.TrimStart('/')}";
        }

        // Nguồn VSMov: thumb_url (ThumbUrl) là ảnh DỌC, poster_url (PosterUrl) là ảnh NGANG (16:9).
        // V2 dùng hai hàm này thay vì đoán theo tên cột.

        /// <summary>Ảnh dọc 2:3 cho thẻ phim / poster.</summary>
        public static string Portrait(string? thumbUrl, string? posterUrl)
            => GetThumb(!string.IsNullOrWhiteSpace(thumbUrl) ? thumbUrl : posterUrl);

        /// <summary>Ảnh ngang 16:9 cho banner / nền.</summary>
        public static string Landscape(string? posterUrl, string? thumbUrl, string? backdrop = null)
            => GetPoster(!string.IsNullOrWhiteSpace(backdrop) ? backdrop
                : !string.IsNullOrWhiteSpace(posterUrl) ? posterUrl : thumbUrl);

        // ===== Trạm ảnh WebP nội bộ (ImageProxyController): /img/w{width}/{đường dẫn nguồn}.webp =====
        private static readonly System.Text.RegularExpressions.Regex ProxyablePath =
            new(@"^[A-Za-z0-9_\-./]+\.(jpg|jpeg|png|webp|gif)$", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Compiled);

        /// <summary>
        /// Đổi URL ảnh VSMov (https://vsmov.com/storage/images/...) sang ảnh WebP đã thu nhỏ của chính server (width: 200/400/800/1280).
        /// URL thuộc nguồn khác hoặc ký tự lạ giữ nguyên.
        /// </summary>
        public static string Webp(string? url, int width)
        {
            if (string.IsNullOrEmpty(url)) return url ?? "";
            if (!url.StartsWith(ApiImageBaseUrl, StringComparison.OrdinalIgnoreCase)) return url;
            var rest = url.Substring(ApiImageBaseUrl.Length);
            return ProxyablePath.IsMatch(rest) ? $"/img/w{width}/{rest}.webp" : url;
        }

        /// <summary>srcset cho các bề rộng cho trước, vd "/img/w200/a.jpg.webp 200w, /img/w400/a.jpg.webp 400w". Rỗng nếu URL không qua trạm ảnh.</summary>
        public static string WebpSrcSet(string? url, params int[] widths)
        {
            var parts = widths.Select(w => (Url: Webp(url, w), W: w)).Where(x => x.Url.StartsWith("/img/w", StringComparison.Ordinal));
            return string.Join(", ", parts.Select(x => $"{x.Url} {x.W}w"));
        }
    }
}


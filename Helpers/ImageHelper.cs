using System;

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
    }
}


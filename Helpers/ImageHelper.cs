using System;

namespace MovieWeb.Helpers
{
    public static class ImageHelper
    {
        // ✅ Domain ảnh mới của Ophim (đang hoạt động)
        private static readonly string ApiImageBaseUrl = "https://img.ophim.live/uploads/movies/";

        /// <summary>
        /// Lấy URL đầy đủ cho poster phim
        /// </summary>
        public static string GetPoster(string? posterFileName)
        {
            if (string.IsNullOrEmpty(posterFileName))
                return "/images/no-poster.png"; // Ảnh mặc định nếu thiếu

            if (posterFileName.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                return posterFileName; // Nếu là full URL thì trả về luôn

            // Ghép domain + tên file
            return $"{ApiImageBaseUrl}{posterFileName}";
        }

        /// <summary>
        /// Lấy URL đầy đủ cho thumbnail phim
        /// </summary>
        public static string GetThumb(string? thumbFileName)
        {
            if (string.IsNullOrEmpty(thumbFileName))
                return "/images/no-thumb.png"; // Ảnh mặc định nếu thiếu

            if (thumbFileName.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                return thumbFileName;

            return $"{ApiImageBaseUrl}{thumbFileName}";
        }
    }
}

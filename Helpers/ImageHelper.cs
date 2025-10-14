namespace MovieWeb.Helpers
{
    public static class ImageHelper
    {
        private static readonly string ApiImageBaseUrl = "https://img.ophim1.com/uploads/movies/";

        public static string GetPoster(string? posterFileName)
        {
            if (string.IsNullOrEmpty(posterFileName))
                return "/images/no-poster.png"; // fallback ảnh mặc định

            if (posterFileName.StartsWith("http"))
                return posterFileName; // nếu đã full URL thì dùng luôn

            // Ghép domain API + file name
            return $"{ApiImageBaseUrl}{posterFileName}";
        }
    }
}

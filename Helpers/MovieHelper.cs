using System.Text.RegularExpressions;

namespace MovieWeb.Helpers 
{
    public static class MovieHelper
    {
        /// <summary>
        /// Lấy mô tả phim với độ dài giới hạn
        /// </summary>
        /// <param name="content">Nội dung mô tả phim</param>
        /// <param name="maxLength">Độ dài tối đa (mặc định 150 ký tự)</param>
        /// <returns>Mô tả phim đã được cắt ngắn</returns>
        public static string GetDescription(string content, int maxLength = 150)
        {
            // Kiểm tra nếu content rỗng hoặc null
            if (string.IsNullOrEmpty(content))
                return "Phim hay đang hot...";
            
            // Loại bỏ các HTML tags nếu có (ví dụ: <p>, <br>, <strong>, etc.)
            string cleanContent = Regex.Replace(content, "<.*?>", string.Empty);
            
            // Loại bỏ khoảng trắng thừa
            cleanContent = Regex.Replace(cleanContent, @"\s+", " ").Trim();
            
            // Cắt ngắn nếu vượt quá độ dài cho phép
            if (cleanContent.Length > maxLength)
            {
                // Tìm vị trí khoảng trắng gần nhất để không cắt giữa từ
                int lastSpace = cleanContent.LastIndexOf(' ', maxLength);
                if (lastSpace > maxLength - 20) // Chỉ cắt tại khoảng trắng nếu không quá ngắn
                {
                    cleanContent = cleanContent.Substring(0, lastSpace);
                }
                else
                {
                    cleanContent = cleanContent.Substring(0, maxLength);
                }
                return cleanContent + "...";
            }
            
            return cleanContent;
        }
        
        /// <summary>
        /// Lấy mô tả ngắn cho banner (ngắn hơn)
        /// </summary>
        /// <param name="content">Nội dung mô tả phim</param>
        /// <returns>Mô tả ngắn cho banner</returns>
        public static string GetBannerDescription(string content)
        {
            return GetDescription(content, 100); // Ngắn hơn cho banner
        }
        
        /// <summary>
        /// Lấy mô tả dài cho trang chi tiết
        /// </summary>
        /// <param name="content">Nội dung mô tả phim</param>
        /// <returns>Mô tả dài</returns>
        public static string GetFullDescription(string content)
        {
            return GetDescription(content, 500); // Dài hơn cho trang chi tiết
        }
    }
}
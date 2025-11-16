using System.ComponentModel;

namespace MovieWeb.Models.ViewModels
{
    public class MovieFilterViewModel
    {
        // Lọc theo Loại phim (single, series...) - Này vẫn chỉ chọn 1
        [DisplayName("Loại phim")]
        public string? Type { get; set; }
        
        [DisplayName("Thể loại")]
        public string? Categories { get; set; } // Sẽ chứa: "hanh-dong,kinh-di"

        [DisplayName("Quốc gia")]
        public string? Countries { get; set; } // Sẽ chứa: "my,han-quoc"

        [DisplayName("Năm")]
        public string? Years { get; set; } // Sẽ chứa: "2024,2023"
        
        // Lọc theo Phiên bản (Cái này vẫn chọn 1)
        [DisplayName("Phiên bản")]
        public string? Language { get; set; }

        // Sắp xếp (Vẫn chọn 1)
        [DisplayName("Sắp xếp")]
        public string SortBy { get; set; } = "updated";

        // Phân trang
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }
}
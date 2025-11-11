using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MovieWeb.Models.Entities
{
    [Table("Advertisements")]
    public class Advertisement
    {
        [Key]
        public int AdId { get; set; }

        [Required]
        [StringLength(200)]
        public string AdName { get; set; } = string.Empty; // Tên quản lý: "Banner trang chủ 1"

        [Required]
        [StringLength(50)]
        public string Placement { get; set; } = string.Empty; // HomePage, PreRoll, WatchPage_Banner, ClimaxAd

        [Required]
        [StringLength(1000)]
        public string AdContentUrl { get; set; } = string.Empty; // Link ảnh/video quảng cáo

        [StringLength(1000)]
        public string? ClickUrl { get; set; } // Link đích khi click (có thể null nếu không cần click)

        [StringLength(500)]
        public string? Description { get; set; } // Mô tả cho admin

        public bool IsActive { get; set; } = true; // Bật/Tắt quảng cáo

        public int DisplayOrder { get; set; } = 0; // Thứ tự hiển thị (nếu có nhiều QC cùng placement)

        public DateTime? CreatedAt { get; set; } = DateTime.Now;

        public DateTime? UpdatedAt { get; set; }

        // Helper: Lấy tên hiển thị của Placement
        [NotMapped]
        public string PlacementDisplayName => Placement switch
        {
            "HomePage" => "🏠 Banner Trang Chủ",
            "WatchPage_Banner" => "🎬 Banner Dưới Trình Phát",
            "PreRoll" => "▶️ Video Trước Phim",
            "ClimaxAd" => "🔥 Video Cuối Phim (10 phút cuối)",
            _ => Placement
        };
    }
}
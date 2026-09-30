using System;

namespace MovieWeb.Models.DTOs
{
    public class AnnouncementSettingsDto
    {
        public bool IsEnabled { get; set; } = false;
        public string BadgeText { get; set; } = "🏮 CHÚC MỪNG TẾT TRUNG THU ĐOÀN VIÊN 🏮";
        public string Title { get; set; } = "🌕 MOONPHIM KÍNH CHÚC QUÝ KHÁN GIẢ 🌕";
        public string MainMessageHtml { get; set; } = "🥮 Nhân dịp Tết Trung Thu (Rằm tháng 8), <strong>MoonPhim</strong> thân ái gửi tới bạn cùng gia đình lời chúc một mùa trăng rằm thật ấm áp, vui vẻ, trọn vẹn yêu thương và ngập tràn hạnh phúc bên những người thân yêu! 💖";
        public string NoticeHeaderHtml { get; set; } = "<i class=\"bi bi-megaphone-fill text-warning me-2\"></i><strong class=\"text-white\">THÔNG BÁO VỀ SỰ CỐ KỸ THUẬT & TÍNH NĂNG XEM CHUNG</strong>";
        public string NoticeBodyHtml { get; set; } = "<div class=\"notice-item mb-2\"><i class=\"bi bi-patch-exclamation-fill text-warning me-2\"></i><div><strong>Lời xin lỗi sự cố đêm 24/09:</strong> Ban Quản Trị thành thật gửi lời xin lỗi chân thành tới các bạn vì sự cố gián đoạn phát video đêm qua. Đội ngũ đã xử lý triệt để, hệ thống hiện đã hoạt động mượt mà trở lại 100%.</div></div><div class=\"notice-item\"><i class=\"bi bi-gear-wide-connected text-info me-2\"></i><div><strong>Tạm hoãn tính năng \"Xem Chung\":</strong> Nhằm tối ưu trải nghiệm đồng bộ và độ ổn định cao nhất, tính năng Xem Chung sẽ được tạm hoãn để nâng cấp sâu hơn trước khi chính thức mở lại.</div></div>";
        public string ButtonText { get; set; } = "🍿 KHÁM PHÁ & XEM PHIM NGAY";
        public string ButtonUrl { get; set; } = "";
        public bool ShowOncePerUser { get; set; } = true;
        public string StorageKeyVersion { get; set; } = "v1";
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }
}

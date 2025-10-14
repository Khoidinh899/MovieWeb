using System;

namespace MovieWeb.Models
{
    public class PaymentHistoryViewModel
    {
        public int TransactionId { get; set; }
        public int? SubscriptionId { get; set; } // ← Đổi thành int? (nullable)
        public string? SubscriptionStatus { get; set; } // ← THÊM PROPERTY NÀY
        public string TransactionCode { get; set; }
        public string PlanName { get; set; }
        public decimal AmountVND { get; set; }
        public string Currency { get; set; }
        public string Status { get; set; }
        public string PaymentMethod { get; set; }
        public DateTime CreatedAt { get; set; }
        public string StatusDisplay { get; set; }
                // ✅ THÊM 2 PROPERTIES MỚI
        public DateTime? SubscriptionEndDate { get; set; } // 🆕 NGÀY HẾT HẠN
    }
}

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MovieWeb.Models.Entities
{
    public class Transaction
    {
        [Key]
        public int TransactionId { get; set; }

        [Required, MaxLength(100)]
        public string TransactionCode { get; set; }

        [MaxLength(100)]
        public string? StripePaymentIntentId { get; set; }

        [MaxLength(100)]
        public string? StripeChargeId { get; set; }

        [Required, Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        [Required, Column(TypeName = "decimal(18,0)")]
        public decimal AmountVND { get; set; }

        [Required, MaxLength(10)]
        public string Currency { get; set; } = "USD";

        [Required, MaxLength(50)]
        public string PaymentMethod { get; set; } = "stripe";

        [Required, MaxLength(20)]
        public string Status { get; set; } = "pending";

        [MaxLength(500)]
        public string? Description { get; set; }

        [MaxLength(1000)]
        public string? PaymentDetails { get; set; }

        [MaxLength(500)]
        public string? FailureReason { get; set; }

        [MaxLength(50)]
        public string? IpAddress { get; set; }

        [MaxLength(500)]
        public string? UserAgent { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? RefundAmount { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        
        // ===== NEW PROPERTIES =====
        public DateTime? CompletedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        // ===== RELATIONSHIPS =====

        public int UserId { get; set; }
        public User User { get; set; }

        public int? PlanId { get; set; }
        public virtual SubscriptionPlan? SubscriptionPlan { get; set; }

        public int? SubscriptionId { get; set; }
        public virtual UserSubscription? UserSubscription { get; set; }

        // ===== COMPUTED PROPERTIES =====

        [NotMapped]
        public string AmountDisplay => $"{AmountVND:N0} ₫";

        [NotMapped]
        public string StatusDisplay => Status switch
        {
            "pending" => "Đang xử lý",
            "completed" => "Thành công",
            "failed" => "Thất bại",
            "refunded" => "Đã hoàn tiền",
            "cancelled" => "Đã hủy",
            _ => Status
        };
    }
}
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MovieWeb.Models.Entities
{
    [Table("SubscriptionPlans")]
    public class SubscriptionPlan
    {
        [Key]
        public int PlanId { get; set; }

        [Required]
        [StringLength(50)]
        public string PlanName { get; set; } // MoonPro, MoonStu

        [Required]
        [StringLength(100)]
        public string DisplayName { get; set; } // "Premium 1 Tháng", "Student 6 Tháng"

        [StringLength(500)]
        public string? Description { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,0)")]
        public decimal PriceVND { get; set; } // Giá bằng VND

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal PriceUSD { get; set; } // Giá quy đổi sang USD cho Stripe

        [Required]
        public int DurationMonths { get; set; } // Số tháng thanh toán (1, 6, 12)

        [Required]
        public int ActualMonths { get; set; } // Số tháng thực tế (có tặng thêm)

        public int BonusMonths { get; set; } // Số tháng được tặng

        [Required]
        [StringLength(20)]
        public string PlanType { get; set; } // "Premium" hoặc "Student"

        [StringLength(100)]
        public string? StripePriceId { get; set; } // ID của Price trong Stripe

        [StringLength(100)]
        public string? StripeProductId { get; set; } // ID của Product trong Stripe

        public bool IsActive { get; set; } = true;

        public bool IsPopular { get; set; } = false; // Đánh dấu gói phổ biến

        public int DisplayOrder { get; set; } = 0; // Thứ tự hiển thị

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime? UpdatedAt { get; set; }
        // Navigation Properties
        public virtual ICollection<UserSubscription>? UserSubscriptions { get; set; }
        public virtual ICollection<Transaction>? Transactions { get; set; }

        // Helper Properties
        [NotMapped]
        public string PriceDisplay => $"{PriceVND:N0} ₫";

        [NotMapped]
        public string DurationDisplay => ActualMonths > DurationMonths 
            ? $"{DurationMonths} tháng (Tặng {BonusMonths} tháng)" 
            : $"{DurationMonths} tháng";

        [NotMapped]
        public decimal MonthlySavings => DurationMonths > 1 
            ? ((PriceVND / DurationMonths) - (GetBasePriceByType() / 1)) * DurationMonths
            : 0;

        private decimal GetBasePriceByType()
        {
            return PlanType == "Student" ? 39000 : 59000;
        }
    }
}
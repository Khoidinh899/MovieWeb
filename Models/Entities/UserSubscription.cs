using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MovieWeb.Models.Entities
{
    [Table("UserSubscriptions")]
    public class UserSubscription
    {
        [Key]
        public int SubscriptionId { get; set; }

        [Required]
        [ForeignKey("User")]
        public int UserId { get; set; }

        [Required]
        [ForeignKey("SubscriptionPlan")]
        public int PlanId { get; set; }

        [StringLength(100)]
        public string? StripeSubscriptionId { get; set; } // ID subscription trong Stripe

        [StringLength(100)]
        public string? StripeCustomerId { get; set; }

        [Required]
        [StringLength(20)]
        public string Status { get; set; } = "active"; // active, cancelled, expired, paused

        [Required]
        public DateTime StartDate { get; set; } = DateTime.Now;

        [Required]
        public DateTime EndDate { get; set; }

        public DateTime? CancelledAt { get; set; }

        public DateTime? NextBillingDate { get; set; } // Ngày billing tiếp theo (nếu auto-renew)

        public bool AutoRenew { get; set; } = true; // Tự động gia hạn
        public int BonusDaysFromPreviousPackage { get; set; } = 0;//Tính ngày còn của gói cũ


        [StringLength(500)]
        public string? CancellationReason { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime? UpdatedAt { get; set; }

        // Navigation Properties
        public virtual User? User { get; set; }
        public virtual SubscriptionPlan? SubscriptionPlan { get; set; }

        // Helper Properties
        [NotMapped]
        public bool IsActive => Status == "active" && EndDate > DateTime.Now;

        [NotMapped]
        public bool IsExpired => Status == "expired" || EndDate <= DateTime.Now;

        [NotMapped]
        public int DaysRemaining
        {
            get
            {
                if (!IsActive) return 0;
                var days = (EndDate - DateTime.Now).Days;
                return days > 0 ? days : 0;
            }
        }

        [NotMapped]
        public bool IsExpiringSoon => IsActive && DaysRemaining <= 7 && DaysRemaining > 0;

        [NotMapped]
        public string StatusDisplay => Status switch
        {
            "active" => "Đang hoạt động",
            "cancelled" => "Đã hủy",
            "expired" => "Đã hết hạn",
            "paused" => "Tạm dừng",
            _ => Status
        };

        [NotMapped]
        public string DurationDisplay
        {
            get
            {
                var totalDays = (EndDate - StartDate).Days;
                var months = totalDays / 30;
                return months > 0 ? $"{months} tháng" : $"{totalDays} ngày";
            }
        }
    }
}
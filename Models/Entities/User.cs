using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MovieWeb.Models.Entities
{
    [Table("Users")]
    public class User : IdentityUser<int>
    {
        // Basic Information
        [StringLength(50)]
        public string? FirstName { get; set; }

        [StringLength(50)]
        public string? LastName { get; set; }

        [StringLength(255)]
        public string? Avatar { get; set; }

        [NotMapped]
        public string FullName => $"{FirstName} {LastName}".Trim();

        [StringLength(200)]
        public string? Address { get; set; }

        [StringLength(10)]
        public string? Gender { get; set; }

        [Column(TypeName = "date")]
        public DateTime? DateOfBirth { get; set; }

        [StringLength(500)]
        public string? Bio { get; set; }

        // Account Status
        public bool? IsActive { get; set; } = true;

        public DateTime? CreatedAt { get; set; } = DateTime.Now;

        public DateTime? UpdatedAt { get; set; }

        public DateTime? LastLogin { get; set; }

        // Role
        [Required]
        [ForeignKey("Role")]
        public int RoleId { get; set; } = 2;

        public virtual Role? Role { get; set; }

        [NotMapped]
        public bool IsAdmin => RoleId == 1;

        // Email Verification
        [StringLength(255)]
        public string? EmailConfirmToken { get; set; }

        [StringLength(255)]
        public string? PasswordResetToken { get; set; }

        // ===== SUBSCRIPTION FIELDS =====

        [StringLength(20)]
        public string SubscriptionType { get; set; } = "free"; // free, premium, student

        public DateTime? SubscriptionStartDate { get; set; }

        public DateTime? SubscriptionEndDate { get; set; }

        [StringLength(100)]
        public string? StripeCustomerId { get; set; } // Stripe Customer ID

        public bool IsStudentVerified { get; set; } = false; // Đã verify email .edu chưa

        [StringLength(100)]
        public string? StudentEmail { get; set; } // Email .edu để verify

        public DateTime? StudentVerifiedAt { get; set; }
        // ===== STUDENT EMAIL VERIFICATION =====
        public string? StudentEmailVerificationCode { get; set; }
        public DateTime? StudentEmailVerificationExpiry { get; set; }
        public DateTime? StudentEmailVerifiedAt { get; set; }

        // FCM and Notification Preferences
        public string? FcmToken { get; set; }
        public bool NotifySystem { get; set; } = true;
        public bool NotifyPayment { get; set; } = true;
        public bool NotifyMovie { get; set; } = true;

        // Helper method để check xem email .edu có cần verify lại không (mỗi năm 1 lần)
        public bool NeedsStudentEmailReverification()
        {
            if (!IsStudentVerified || !StudentEmailVerifiedAt.HasValue)
                return true;

            // Nếu đã verify hơn 12 tháng thì cần verify lại
            return DateTime.Now.Subtract(StudentEmailVerifiedAt.Value).TotalDays > 365;
        }

        // Helper method để check OTP có còn hạn không
        public bool IsStudentVerificationCodeValid(string code)
        {
            if (string.IsNullOrEmpty(StudentEmailVerificationCode) ||
                !StudentEmailVerificationExpiry.HasValue)
                return false;

            return StudentEmailVerificationCode == code &&
                   DateTime.Now <= StudentEmailVerificationExpiry.Value;
        }

        // Helper Properties for Subscription
        [NotMapped]
    public bool IsPremium
    {
        get
        {
            // Kiểm tra theo UserSubscriptions (chính xác hơn)
            // HOẶC fallback về SubscriptionEndDate
            return SubscriptionEndDate.HasValue 
                && SubscriptionEndDate.Value > DateTime.Now;
        }
    }

        [NotMapped]
        public bool IsStudent => SubscriptionType == "student" && IsPremium;

        [NotMapped]
        public bool IsFreeUser => SubscriptionType == "free" || !IsPremium;

        [NotMapped]
        public int DaysRemaining
        {
            get
            {
                if (!IsPremium || !SubscriptionEndDate.HasValue) return 0;
                var days = (SubscriptionEndDate.Value - DateTime.Now).Days;
                return days > 0 ? days : 0;
            }
        }

        [NotMapped]
        public string SubscriptionDisplayName => SubscriptionType switch
        {
            "premium" => "MoonPro",
            "student" => "MoonStu",
            _ => "Free"
        };

        // Navigation Properties
        public virtual ICollection<Comment>? Comments { get; set; }
        public virtual ICollection<Favorite>? Favorites { get; set; }
        public virtual ICollection<Rating>? Ratings { get; set; }
        public virtual ICollection<WatchHistory>? WatchHistories { get; set; }
        public virtual ICollection<AdminLog>? AdminLogs { get; set; }
        public virtual ICollection<Notification>? Notifications { get; set; }

        // Subscription Navigation Properties
        public virtual ICollection<UserSubscription>? UserSubscriptions { get; set; }
        public virtual ICollection<Transaction>? Transactions { get; set; }
    }
}
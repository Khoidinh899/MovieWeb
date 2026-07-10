using System.ComponentModel.DataAnnotations;

namespace MovieWeb.Models.DTOs
{
    // DTO trả về thông tin profile đầy đủ
    public class UserProfileDto
    {
        public int UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string FullName => $"{FirstName} {LastName}".Trim();
        public string? Avatar { get; set; }
        public bool IsActive { get; set; }
        public bool EmailConfirmed { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public DateTime? LastLogin { get; set; }
        public int RoleId { get; set; }
        public string RoleName => RoleId == 1 ? "Admin" : "User";
        public bool IsAdmin => RoleId == 1;
        public string? PhoneNumber { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public string? Gender { get; set; }
        public string? Address { get; set; }
        public string? Bio { get; set; }
        public int TotalFavorites { get; set; }
        public int TotalComments { get; set; }
        public int TotalRatings { get; set; }
        public int TotalWatchHistory { get; set; }

        // ===== SUBSCRIPTION PROPERTIES =====
        public string SubscriptionType { get; set; } = "free";
        public DateTime? SubscriptionStartDate { get; set; }
        public DateTime? SubscriptionEndDate { get; set; }
        public int RemainingDaysFromPreviousPackage { get; set; } // Bonus days từ gói cũ
        public bool IsCancelled { get; set; } // True nếu gói bị hủy nhưng còn hạn
        public string SubscriptionDisplayName
        {
            get => SubscriptionType switch
            {
                "premium" => "MoonPro",
                "student" => "MoonStu",
                _ => "Free"
            };
        }

        public bool IsPremium =>
            (SubscriptionType == "premium" || SubscriptionType == "student")
            && SubscriptionEndDate.HasValue
            && SubscriptionEndDate.Value > DateTime.Now;

        public int DaysRemaining =>
            (IsPremium && SubscriptionEndDate.HasValue)
                ? Math.Max(0, (int)(SubscriptionEndDate.Value - DateTime.Now).TotalDays)
                : 0;

        public bool IsExpiringSoon =>
            IsPremium && DaysRemaining <= 7;

        // ===== STUDENT VERIFICATION PROPERTIES =====
        public bool IsStudentVerified { get; set; }
        public string? StudentEmail { get; set; }
        public DateTime? StudentEmailVerifiedAt { get; set; }
        public DateTime? StudentEmailVerificationExpiry { get; set; }

        // Notification Preferences
        public bool NotifySystem { get; set; }
        public bool NotifyPayment { get; set; }
        public bool NotifyMovie { get; set; }
    }
    // DTO để cập nhật thông tin cá nhân
    public class UpdateProfileDto
    {
        [Required(ErrorMessage = "Họ là bắt buộc")]
        [StringLength(50, ErrorMessage = "Họ không được quá 50 ký tự")]
        public string FirstName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Tên là bắt buộc")]
        [StringLength(50, ErrorMessage = "Tên không được quá 50 ký tự")]
        public string LastName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email là bắt buộc")]
        [EmailAddress(ErrorMessage = "Email không hợp lệ")]
        public string Email { get; set; } = string.Empty;

        public string? CurrentEmail { get; set; } // Để so sánh khi thay đổi email

        // ✅ Thêm các trường bổ sung
        [Phone(ErrorMessage = "Số điện thoại không hợp lệ")]
        public string? PhoneNumber { get; set; }

        [DataType(DataType.Date)]
        public DateTime? DateOfBirth { get; set; }

        [StringLength(10)]
        public string? Gender { get; set; }

        [StringLength(200)]
        public string? Address { get; set; }

        [StringLength(500)]
        public string? Bio { get; set; }

        // Notification Preferences
        public bool NotifySystem { get; set; } = true;
        public bool NotifyPayment { get; set; } = true;
        public bool NotifyMovie { get; set; } = true;
    }

    // DTO để thay đổi mật khẩu
    public class ChangePasswordDto
{
    [Required(ErrorMessage = "Mật khẩu hiện tại là bắt buộc")]
    [DataType(DataType.Password)]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Mật khẩu mới là bắt buộc")]
    [DataType(DataType.Password)]
    // THAY ĐỔI Ở ĐÂY: Thêm Regular Expression để kiểm tra độ phức tạp
    [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&.])[A-Za-z\d@$!%*?&.]{8,}$",
        ErrorMessage = "Mật khẩu mới phải có ít nhất 8 ký tự, bao gồm chữ hoa, chữ thường, số và ký tự đặc biệt.")]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Xác nhận mật khẩu là bắt buộc")]
    [DataType(DataType.Password)]
    [Compare("NewPassword", ErrorMessage = "Xác nhận mật khẩu không khớp")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

    // DTO để upload avatar
    public class UpdateAvatarDto
    {
        [Required(ErrorMessage = "Vui lòng chọn ảnh")]
        public IFormFile Avatar { get; set; } = null!;
    }

    // DTO cho admin quản lý user
    public class AdminUpdateUserDto
    {
        [Required]
        public int UserId { get; set; }

        [Required(ErrorMessage = "Họ là bắt buộc")]
        [StringLength(50)]
        public string FirstName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Tên là bắt buộc")]
        [StringLength(50)]
        public string LastName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email là bắt buộc")]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vai trò là bắt buộc")]
        public int RoleId { get; set; }

        public bool IsActive { get; set; }

        // ✅ Thêm các trường bổ sung
        [Phone(ErrorMessage = "Số điện thoại không hợp lệ")]
        public string? PhoneNumber { get; set; }

        [DataType(DataType.Date)]
        public DateTime? DateOfBirth { get; set; }

        [StringLength(10)]
        public string? Gender { get; set; }

        [StringLength(200)]
        public string? Address { get; set; }
    }

    // DTO để admin thay đổi mật khẩu user
    public class AdminChangePasswordDto
    {
        [Required]
        public int UserId { get; set; }

        [Required(ErrorMessage = "Mật khẩu mới là bắt buộc")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "Mật khẩu phải từ 6-100 ký tự")]
        public string NewPassword { get; set; } = string.Empty;

        [Compare("NewPassword", ErrorMessage = "Xác nhận mật khẩu không khớp")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    // Response chung
    public class ProfileResult
    {
        public bool IsSuccess { get; set; }
        public string Message { get; set; } = string.Empty;
        public List<string> Errors { get; set; } = new();
        public UserProfileDto? User { get; set; }

        public static ProfileResult Success(string message, UserProfileDto? user = null)
        {
            return new ProfileResult
            {
                IsSuccess = true,
                Message = message,
                User = user
            };
        }

        public static ProfileResult Failed(string message)
        {
            return new ProfileResult
            {
                IsSuccess = false,
                Message = message
            };
        }

        public static ProfileResult Failed(List<string> errors)
        {
            return new ProfileResult
            {
                IsSuccess = false,
                Message = "Có lỗi xảy ra",
                Errors = errors
            };
        }
    }
}
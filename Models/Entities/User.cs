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

        // ✅ Thêm thiếu
        [StringLength(255)]
        public string? EmailConfirmToken { get; set; }

        [StringLength(255)]
        public string? PasswordResetToken { get; set; }

        // Navigation Properties
        public virtual ICollection<Comment>? Comments { get; set; }
        public virtual ICollection<Favorite>? Favorites { get; set; }
        public virtual ICollection<Rating>? Ratings { get; set; }
        public virtual ICollection<WatchHistory>? WatchHistories { get; set; }
        public virtual ICollection<AdminLog>? AdminLogs { get; set; }

        // ✅ Thêm thiếu
        public virtual ICollection<Notification>? Notifications { get; set; }
    }
}

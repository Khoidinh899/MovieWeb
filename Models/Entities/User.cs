using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;

namespace MovieWeb.Models.Entities
{
    public partial class User : IdentityUser<int>
    {
        public User()
        {
            // Không cần logic khởi tạo thủ công, để EF Core xử lý
        }

        // Xóa thuộc tính Username tùy chỉnh, sử dụng trực tiếp UserName từ IdentityUser
        // Đồng bộ NormalizedUserName qua setter của UserName nếu cần

        public override string? UserName
        {
            get => base.UserName;
            set
            {
                base.UserName = value;
                NormalizedUserName = value?.ToUpper();
            }
        }

        public override string? Email
        {
            get => base.Email;
            set
            {
                base.Email = value;
                NormalizedEmail = value?.ToUpper();
            }
        }

        public string? FirstName { get; set; }

        public string? LastName { get; set; }

        public bool IsAdmin => RoleId == 1;

        public string FullName => $"{FirstName} {LastName}".Trim();

        public string? Avatar { get; set; }

        public int RoleId { get; set; }

        public bool? IsActive { get; set; } = true;

        public string? EmailConfirmToken { get; set; }

        public string? PasswordResetToken { get; set; }

        public DateTime? PasswordResetExpires { get; set; }

        public DateTime? LastLogin { get; set; }

        public DateTime? CreatedAt { get; set; } // Sử dụng DefaultValueSql trong OnModelCreating

        public DateTime? UpdatedAt { get; set; } // Sử dụng DefaultValueSql trong OnModelCreating

        // Navigation properties
        public virtual ICollection<AdminLog> AdminLogs { get; set; } = new List<AdminLog>();

        public virtual ICollection<Comment> Comments { get; set; } = new List<Comment>();

        public virtual ICollection<Favorite> Favorites { get; set; } = new List<Favorite>();

        public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();

        public virtual ICollection<Rating> Ratings { get; set; } = new List<Rating>();

        public virtual Role Role { get; set; } = null!;

        public virtual ICollection<WatchHistory> WatchHistories { get; set; } = new List<WatchHistory>();

        public override string ToString()
        {
            return FullName ?? UserName ?? Email ?? Id.ToString();
        }
    }
}
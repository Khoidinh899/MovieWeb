using System;
using System.Collections.Generic;

namespace MovieWeb.Models.Entities;

public partial class User
{
    public int UserId { get; set; }

    public string Username { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string PasswordHash { get; set; } = null!;

    public string? FirstName { get; set; }

    public string? LastName { get; set; }

    public string? Avatar { get; set; }

    public int RoleId { get; set; }

    public bool? IsActive { get; set; }

    public bool? IsEmailConfirmed { get; set; }

    public string? EmailConfirmToken { get; set; }

    public string? PasswordResetToken { get; set; }

    public DateTime? PasswordResetExpires { get; set; }

    public DateTime? LastLogin { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual ICollection<AdminLog> AdminLogs { get; set; } = new List<AdminLog>();

    public virtual ICollection<Comment> Comments { get; set; } = new List<Comment>();

    public virtual ICollection<Favorite> Favorites { get; set; } = new List<Favorite>();

    public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();

    public virtual ICollection<Rating> Ratings { get; set; } = new List<Rating>();

    public virtual Role Role { get; set; } = null!;

    public virtual ICollection<WatchHistory> WatchHistories { get; set; } = new List<WatchHistory>();
}

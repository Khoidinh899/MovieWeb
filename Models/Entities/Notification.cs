using System;
using System.Collections.Generic;

namespace MovieWeb.Models.Entities;

public partial class Notification
{
    public int NotificationId { get; set; }

    public int? UserId { get; set; }

    public string Title { get; set; } = null!;

    public string Content { get; set; } = null!;

    public string? Type { get; set; }

    public bool? IsRead { get; set; }

    public string? Url { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual User? User { get; set; }
}

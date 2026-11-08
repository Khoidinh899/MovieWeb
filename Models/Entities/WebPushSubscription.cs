using System;

namespace MovieWeb.Models.Entities;

/// <summary>
/// One browser/PWA push subscription (Web Push, VAPID). A user can have several (phone, laptop...).
/// Separate from User.FcmToken, which holds the Android app's single FCM token.
/// </summary>
public class WebPushSubscription
{
    public int Id { get; set; }

    public int UserId { get; set; }

    /// <summary>Push service URL issued by the browser; unique per subscription.</summary>
    public string Endpoint { get; set; } = null!;

    public string P256dh { get; set; } = null!;

    public string Auth { get; set; } = null!;

    public string? UserAgent { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? LastSuccessAt { get; set; }

    public virtual User User { get; set; } = null!;
}

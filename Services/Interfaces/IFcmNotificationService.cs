namespace MovieWeb.Services.Interfaces
{
    public interface IFcmNotificationService
    {
        Task<bool> SendPushNotificationAsync(string fcmToken, string title, string content, string type, string? url = null);
    }
}

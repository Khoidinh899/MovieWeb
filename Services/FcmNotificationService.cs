using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using MovieWeb.Services.Interfaces;
using System.IO;

namespace MovieWeb.Services
{
    public class FcmNotificationService : IFcmNotificationService
    {
        private readonly ILogger<FcmNotificationService> _logger;
        private readonly bool _isFirebaseInitialized;

        public FcmNotificationService(ILogger<FcmNotificationService> logger)
        {
            _logger = logger;
            _isFirebaseInitialized = InitializeFirebase();
        }

        private bool InitializeFirebase()
        {
            try
            {
                if (FirebaseApp.DefaultInstance != null)
                {
                    return true;
                }

                string credentialPath = Path.Combine(Directory.GetCurrentDirectory(), "firebase-service-account.json");
                if (!File.Exists(credentialPath))
                {
                    _logger.LogWarning("⚠️ Firebase Service Account file not found at '{Path}'. FCM push notifications will be simulated.", credentialPath);
                    return false;
                }

                FirebaseApp.Create(new AppOptions()
                {
                    Credential = GoogleCredential.FromFile(credentialPath)
                });

                _logger.LogInformation("✅ Firebase App initialized successfully for FCM.");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error initializing Firebase App.");
                return false;
            }
        }

        public async Task<bool> SendPushNotificationAsync(string fcmToken, string title, string content, string type, string? url = null)
        {
            if (string.IsNullOrEmpty(fcmToken))
            {
                return false;
            }

            _logger.LogInformation("Sending push notification: Title='{Title}', Content='{Content}', Token='{Token}', Type='{Type}'", title, content, fcmToken, type);

            if (!_isFirebaseInitialized)
            {
                _logger.LogWarning("FCM is not initialized (missing credentials). Logged notification only.");
                return true; // Return true as simulation success
            }

            try
            {
                var message = new Message()
                {
                    Token = fcmToken,
                    Notification = new FirebaseAdmin.Messaging.Notification()
                    {
                        Title = title,
                        Body = content
                    },
                    Data = new Dictionary<string, string>()
                    {
                        { "type", type },
                        { "url", url ?? "" }
                    }
                };

                string response = await FirebaseMessaging.DefaultInstance.SendAsync(message);
                _logger.LogInformation("Successfully sent FCM message: {Response}", response);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending FCM message to token: {Token}", fcmToken);
                return false;
            }
        }
    }
}

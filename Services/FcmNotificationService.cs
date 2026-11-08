using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using MovieWeb.Services.Interfaces;
using System.IO;
using System.Text.Json;

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
                    _logger.LogWarning("⚠️ Firebase Service Account file not found at '{Path}'. FCM push notifications are unavailable.", credentialPath);
                    return false;
                }

                using var credentialDocument = JsonDocument.Parse(File.ReadAllText(credentialPath));
                var root = credentialDocument.RootElement;
                // Firebase Admin requires a service account, not an OAuth client or web config.
                string[] requiredFields = ["project_id", "client_email", "private_key"];
                if (root.ValueKind != JsonValueKind.Object
                    || !root.TryGetProperty("type", out var credentialType)
                    || credentialType.ValueKind != JsonValueKind.String
                    || credentialType.GetString() != "service_account"
                    || requiredFields.Any(field => !root.TryGetProperty(field, out var value)
                        || value.ValueKind != JsonValueKind.String
                        || string.IsNullOrWhiteSpace(value.GetString())))
                {
                    _logger.LogWarning("Firebase service account configuration is incomplete. FCM push notifications are unavailable.");
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

            _logger.LogDebug("Sending FCM push notification of type {Type}.", type);

            if (!_isFirebaseInitialized)
            {
                _logger.LogWarning("FCM is unavailable. Push notification was not sent.");
                return false;
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
                _logger.LogError("Error sending FCM message ({ErrorType}).", ex.GetType().Name);
                return false;
            }
        }
    }
}

using System.Net;
using System.Text.Json;
using EventsApp.Data;
using Microsoft.EntityFrameworkCore;
using WebPush;

namespace EventsApp.Services
{
    public class WebPushNotificationService : IPushNotificationService
    {
        private readonly ApplicationDbContext _db;
        private readonly IConfiguration _configuration;
        private readonly ILogger<WebPushNotificationService> _logger;

        public WebPushNotificationService(
            ApplicationDbContext db,
            IConfiguration configuration,
            ILogger<WebPushNotificationService> logger)
        {
            _db = db;
            _configuration = configuration;
            _logger = logger;
        }

        public string? PublicKey => GetSetting("VAPID_PUBLIC_KEY", "WebPush:PublicKey");

        private string? PrivateKey => GetSetting("VAPID_PRIVATE_KEY", "WebPush:PrivateKey");

        private string Subject => GetSetting("VAPID_SUBJECT", "WebPush:Subject") ?? "mailto:eventopulsee@gmail.com";

        public bool IsConfigured => !string.IsNullOrWhiteSpace(PublicKey) && !string.IsNullOrWhiteSpace(PrivateKey);

        public async Task SendMessageNotificationAsync(
            string recipientUserId,
            string title,
            string body,
            string url,
            int? badgeCount = null,
            CancellationToken cancellationToken = default)
        {
            if (!IsConfigured)
            {
                return;
            }

            var subscriptions = await _db.UserPushSubscriptions
                .Where(s => s.UserId == recipientUserId)
                .ToListAsync(cancellationToken);

            if (subscriptions.Count == 0)
            {
                return;
            }

            var payload = JsonSerializer.Serialize(new
            {
                title,
                body,
                url,
                tag = "evento-message",
                icon = "/img/logo.svg",
                badge = "/img/logo.svg",
                badgeCount,
            });

            var vapid = new VapidDetails(Subject, PublicKey, PrivateKey);
            using var client = new WebPushClient();
            var staleSubscriptions = new List<Models.UserPushSubscription>();

            foreach (var subscription in subscriptions)
            {
                try
                {
                    var pushSubscription = new PushSubscription(
                        subscription.Endpoint,
                        subscription.P256DH,
                        subscription.Auth);

                    await client.SendNotificationAsync(pushSubscription, payload, vapid, cancellationToken);
                }
                catch (WebPushException ex) when (ex.StatusCode is HttpStatusCode.Gone or HttpStatusCode.NotFound)
                {
                    staleSubscriptions.Add(subscription);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to send push notification to subscription {SubscriptionId}.", subscription.Id);
                }
            }

            if (staleSubscriptions.Count > 0)
            {
                _db.UserPushSubscriptions.RemoveRange(staleSubscriptions);
                await _db.SaveChangesAsync(cancellationToken);
            }
        }

        private string? GetSetting(params string[] keys)
        {
            foreach (var key in keys)
            {
                var value = _configuration[key];
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }

            return null;
        }
    }
}

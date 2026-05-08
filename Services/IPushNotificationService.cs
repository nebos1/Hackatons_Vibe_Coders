namespace EventsApp.Services
{
    public interface IPushNotificationService
    {
        string? PublicKey { get; }

        bool IsConfigured { get; }

        Task SendMessageNotificationAsync(
            string recipientUserId,
            string title,
            string body,
            string url,
            int? badgeCount = null,
            CancellationToken cancellationToken = default);
    }
}

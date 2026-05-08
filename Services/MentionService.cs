using System.Text.RegularExpressions;
using EventsApp.Data;
using Microsoft.EntityFrameworkCore;

namespace EventsApp.Services
{
    public interface IMentionService
    {
        IReadOnlyList<string> ExtractUsernames(string? text);
        Task<IReadOnlyList<MentionedUser>> ResolveAsync(string? text, string? excludeUserId, CancellationToken cancellationToken = default);
        Task NotifyMentionsAsync(
            string? text,
            string senderUserId,
            string senderDisplayName,
            string contextLabel,
            string url,
            CancellationToken cancellationToken = default);
    }

    public record MentionedUser(string Id, string UserName);

    public class MentionService : IMentionService
    {
        private static readonly Regex MentionRegex = new(
            @"(?<![A-Za-z0-9_])@([A-Za-z0-9._]{3,30})",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private readonly ApplicationDbContext _db;
        private readonly IPushNotificationService _push;
        private readonly ILogger<MentionService> _logger;

        public MentionService(
            ApplicationDbContext db,
            IPushNotificationService push,
            ILogger<MentionService> logger)
        {
            _db = db;
            _push = push;
            _logger = logger;
        }

        public IReadOnlyList<string> ExtractUsernames(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return Array.Empty<string>();
            var matches = MentionRegex.Matches(text);
            if (matches.Count == 0) return Array.Empty<string>();
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (Match m in matches)
            {
                if (m.Groups.Count > 1)
                {
                    names.Add(m.Groups[1].Value.Trim());
                }
            }
            return names.ToList();
        }

        public async Task<IReadOnlyList<MentionedUser>> ResolveAsync(
            string? text,
            string? excludeUserId,
            CancellationToken cancellationToken = default)
        {
            var usernames = ExtractUsernames(text);
            if (usernames.Count == 0) return Array.Empty<MentionedUser>();

            var normalized = usernames.Select(u => u.ToUpperInvariant()).ToList();
            var users = await _db.Users
                .AsNoTracking()
                .Where(u => u.UserName != null && normalized.Contains(u.NormalizedUserName!))
                .Select(u => new MentionedUser(u.Id, u.UserName!))
                .ToListAsync(cancellationToken);

            if (!string.IsNullOrEmpty(excludeUserId))
            {
                users = users.Where(u => u.Id != excludeUserId).ToList();
            }
            return users;
        }

        public async Task NotifyMentionsAsync(
            string? text,
            string senderUserId,
            string senderDisplayName,
            string contextLabel,
            string url,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var mentioned = await ResolveAsync(text, excludeUserId: senderUserId, cancellationToken);
                if (mentioned.Count == 0) return;

                var title = $"{senderDisplayName} те спомена";
                var trimmed = (text ?? "").Trim();
                if (trimmed.Length > 140) trimmed = trimmed[..140] + "...";

                foreach (var user in mentioned)
                {
                    await _push.SendNotificationAsync(
                        user.Id,
                        title,
                        $"{contextLabel}: {trimmed}",
                        url,
                        tag: $"evento-mention-{user.Id}",
                        cancellationToken: cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send mention notifications.");
            }
        }
    }
}

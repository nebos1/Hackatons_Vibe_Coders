using System.Collections.Concurrent;
using EventsApp.Data;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace EventsApp.Hubs
{
    public class ChatHub : Hub
    {
        private sealed record ActiveConversationConnection(string Token, string UserId);

        private static readonly ConcurrentDictionary<string, ActiveConversationConnection> ActiveConnections = new();

        // userId -> set of active hub connectionIds. Used for online-presence broadcasts.
        private static readonly ConcurrentDictionary<string, HashSet<string>> UserConnections = new();

        private readonly IServiceScopeFactory _scopeFactory;

        public ChatHub(IServiceScopeFactory scopeFactory)
        {
            _scopeFactory = scopeFactory;
        }

        public static bool IsUserActiveInConversation(Guid token, string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return false;
            }

            var normalizedToken = NormalizeToken(token.ToString());
            return ActiveConnections.Values.Any(c => c.Token == normalizedToken && c.UserId == userId);
        }

        public static bool IsUserOnline(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId)) return false;
            return UserConnections.TryGetValue(userId, out var set) && set.Count > 0;
        }

        // Records the moment the final chat connection dropped. Mirrors
        // LastLogoutAt so the chat header can show "Active X ago" based on
        // the same column the auth Logout endpoint writes to.
        private async Task UpdateUserLastSeenAsync(string userId, DateTime when)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.Users
                .Where(u => u.Id == userId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(u => u.LastSeenAt, when)
                    .SetProperty(u => u.LastLogoutAt, when));
        }

        // Refresh LastLoginAt when a connection establishes so the
        // login/logout pair stays the authoritative source for presence,
        // even if VisitTracker hasn't fired yet.
        private async Task TouchUserLastLoginAsync(string userId, DateTime when)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.Users
                .Where(u => u.Id == userId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(u => u.LastLoginAt, when));
        }

        private sealed record UserPresenceSnapshot(DateTime? LastLoginAt, DateTime? LastLogoutAt);

        private async Task<UserPresenceSnapshot> ReadUserPresenceAsync(string userId)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var record = await db.Users
                .Where(u => u.Id == userId)
                .Select(u => new { u.LastLoginAt, u.LastLogoutAt })
                .FirstOrDefaultAsync();
            return new UserPresenceSnapshot(record?.LastLoginAt, record?.LastLogoutAt);
        }

        // "Online" if the user's most recent action was a login (or
        // heartbeat that refreshed LastLoginAt) AND that action happened
        // within the heartbeat freshness window. VisitTracker keeps the
        // column current while the tab is open.
        private static readonly TimeSpan OnlineWindow = TimeSpan.FromMinutes(2);

        private static bool IsOnlineFromSnapshot(UserPresenceSnapshot s)
        {
            if (s.LastLoginAt is null) return false;
            if (s.LastLogoutAt is not null && s.LastLogoutAt > s.LastLoginAt) return false;
            return DateTime.UtcNow - s.LastLoginAt.Value <= OnlineWindow;
        }

        public override async Task OnConnectedAsync()
        {
            var userId = Context.UserIdentifier;
            if (!string.IsNullOrWhiteSpace(userId))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, UserGroup(userId));
                var becameOnline = false;
                UserConnections.AddOrUpdate(userId,
                    _ => { becameOnline = true; return new HashSet<string> { Context.ConnectionId }; },
                    (_, set) =>
                    {
                        lock (set)
                        {
                            if (set.Count == 0) becameOnline = true;
                            set.Add(Context.ConnectionId);
                        }
                        return set;
                    });

                if (becameOnline)
                {
                    var when = DateTime.UtcNow;
                    try { await TouchUserLastLoginAsync(userId, when); } catch { /* best-effort */ }
                    await Clients.All.SendAsync("PresenceChanged", new { userId, online = true, lastLoginAt = when });
                }
            }
            await base.OnConnectedAsync();
        }

        public Task JoinConversation(string token)
        {
            var userId = Context.UserIdentifier;
            if (!string.IsNullOrWhiteSpace(userId) && Guid.TryParse(token, out var parsedToken))
            {
                ActiveConnections[Context.ConnectionId] = new ActiveConversationConnection(NormalizeToken(parsedToken.ToString()), userId);
            }

            return Groups.AddToGroupAsync(Context.ConnectionId, token);
        }

        public Task LeaveConversation(string token)
        {
            ActiveConnections.TryRemove(Context.ConnectionId, out _);
            return Groups.RemoveFromGroupAsync(Context.ConnectionId, token);
        }

        // Snapshot of a user's presence. Online state is derived from the
        // LastLoginAt/LastLogoutAt columns (refreshed by login + the
        // VisitTracker heartbeat; cleared by explicit Logout, tab-close
        // beacon, or the chat hub disconnect), with a freshness window so
        // a long-idle session does not appear online forever.
        public async Task<object> QueryPresence(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return new { online = false, lastLoginAt = (DateTime?)null, lastLogoutAt = (DateTime?)null };
            }
            var snap = await ReadUserPresenceAsync(userId);
            var online = IsOnlineFromSnapshot(snap);
            return new
            {
                online,
                lastLoginAt = snap.LastLoginAt,
                lastLogoutAt = snap.LastLogoutAt,
                // Echoed for backward compatibility with older clients
                // that read this field for the "Active X ago" label.
                lastSeenAt = snap.LastLogoutAt,
            };
        }

        // Lightweight typing broadcast — no persistence. Fires to the other
        // participant(s) so they can render the "is typing…" bubble. The
        // conversationToken is echoed back so list rows for non-active chats
        // can flip to "typing…" too.
        public Task Typing(string token, bool isTyping)
        {
            var userId = Context.UserIdentifier;
            if (string.IsNullOrWhiteSpace(userId)) return Task.CompletedTask;
            return Clients.OthersInGroup(token).SendAsync("UserTyping", new
            {
                userId,
                isTyping,
                conversationToken = token,
            });
        }

        // Mark every incoming message in the conversation with id ≤ lastMessageId
        // as seen by the current user, then broadcast to the conversation so the
        // sender can render read-receipts in real time.
        public async Task MarkSeen(string token, int lastMessageId)
        {
            var userId = Context.UserIdentifier;
            if (string.IsNullOrWhiteSpace(userId) || !Guid.TryParse(token, out var parsedToken)) return;

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var convo = await db.Conversations
                .FirstOrDefaultAsync(c => c.Token == parsedToken);
            if (convo == null) return;
            if (convo.ParticipantOneId != userId && convo.ParticipantTwoId != userId) return;

            var now = DateTime.UtcNow;
            var msgs = await db.Messages
                .Where(m => m.ConversationId == convo.Id
                            && m.Id <= lastMessageId
                            && m.SenderId != userId
                            && m.SeenAt == null
                            && !m.IsDeleted)
                .ToListAsync();

            if (msgs.Count > 0)
            {
                foreach (var m in msgs) m.SeenAt = now;
                await db.SaveChangesAsync();

                await Clients.OthersInGroup(token).SendAsync("MessageSeen", new
                {
                    lastMessageId,
                    seenAt = now,
                    seenByUserId = userId,
                });
                await Clients.Group(UserGroup(userId)).SendAsync("ConversationRead", new
                {
                    token,
                });
            }
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            ActiveConnections.TryRemove(Context.ConnectionId, out _);

            var userId = Context.UserIdentifier;
            if (!string.IsNullOrWhiteSpace(userId) && UserConnections.TryGetValue(userId, out var set))
            {
                var becameOffline = false;
                lock (set)
                {
                    set.Remove(Context.ConnectionId);
                    becameOffline = set.Count == 0;
                }
                if (becameOffline)
                {
                    UserConnections.TryRemove(userId, out _);
                    var lastLogoutAt = DateTime.UtcNow;
                    try { await UpdateUserLastSeenAsync(userId, lastLogoutAt); } catch { /* best-effort */ }
                    await Clients.All.SendAsync("PresenceChanged", new
                    {
                        userId,
                        online = false,
                        lastLogoutAt,
                        // Back-compat with older clients reading lastSeenAt.
                        lastSeenAt = lastLogoutAt,
                    });
                }
            }

            await base.OnDisconnectedAsync(exception);
        }

        private static string NormalizeToken(string token)
        {
            return token.Trim().ToLowerInvariant();
        }

        public static string UserGroup(string userId) => $"user:{userId}";
    }
}

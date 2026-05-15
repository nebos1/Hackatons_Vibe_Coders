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

        public override async Task OnConnectedAsync()
        {
            var userId = Context.UserIdentifier;
            if (!string.IsNullOrWhiteSpace(userId))
            {
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
                    await Clients.All.SendAsync("PresenceChanged", new { userId, online = true });
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

        // Lightweight typing broadcast — no persistence. Fires to the other
        // participant(s) so they can render the "is typing…" bubble.
        public Task Typing(string token, bool isTyping)
        {
            var userId = Context.UserIdentifier;
            if (string.IsNullOrWhiteSpace(userId)) return Task.CompletedTask;
            return Clients.OthersInGroup(token).SendAsync("UserTyping", new { userId, isTyping });
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
                    await Clients.All.SendAsync("PresenceChanged", new { userId, online = false });
                }
            }

            await base.OnDisconnectedAsync(exception);
        }

        private static string NormalizeToken(string token)
        {
            return token.Trim().ToLowerInvariant();
        }
    }
}

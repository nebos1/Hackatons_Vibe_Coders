using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;

namespace EventsApp.Hubs
{
    public class ChatHub : Hub
    {
        private sealed record ActiveConversationConnection(string Token, string UserId);

        private static readonly ConcurrentDictionary<string, ActiveConversationConnection> ActiveConnections = new();

        public static bool IsUserActiveInConversation(Guid token, string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return false;
            }

            var normalizedToken = NormalizeToken(token.ToString());
            return ActiveConnections.Values.Any(c => c.Token == normalizedToken && c.UserId == userId);
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

        public override Task OnDisconnectedAsync(Exception? exception)
        {
            ActiveConnections.TryRemove(Context.ConnectionId, out _);
            return base.OnDisconnectedAsync(exception);
        }

        private static string NormalizeToken(string token)
        {
            return token.Trim().ToLowerInvariant();
        }
    }
}

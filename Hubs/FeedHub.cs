using Microsoft.AspNetCore.SignalR;

namespace EventsApp.Hubs
{
    public class FeedHub : Hub
    {
        public Task JoinPost(int postId) =>
            Groups.AddToGroupAsync(Context.ConnectionId, $"post:{postId}");

        public Task LeavePost(int postId) =>
            Groups.RemoveFromGroupAsync(Context.ConnectionId, $"post:{postId}");

        public Task JoinEvent(int eventId) =>
            Groups.AddToGroupAsync(Context.ConnectionId, $"event:{eventId}");

        public Task LeaveEvent(int eventId) =>
            Groups.RemoveFromGroupAsync(Context.ConnectionId, $"event:{eventId}");
    }
}

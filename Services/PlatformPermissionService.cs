using System.Security.Claims;
using EventsApp.Common;
using EventsApp.Data;
using EventsApp.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace EventsApp.Services
{
    public interface IPlatformPermissionService
    {
        Task<bool> CanCreatePostAsync(ClaimsPrincipal user, CancellationToken cancellationToken = default);

        Task<bool> CanCreateStoryAsync(ClaimsPrincipal user, CancellationToken cancellationToken = default);

        Task<bool> CanManageWorkspaceAsync(ClaimsPrincipal user, int workspaceId, CancellationToken cancellationToken = default);

        Task<bool> CanManageOrganizerPageAsync(ClaimsPrincipal user, int organizerProfileId, CancellationToken cancellationToken = default);

        Task<bool> CanActAsUserAsync(ClaimsPrincipal currentUser, string targetUserId, CancellationToken cancellationToken = default);

        Task<bool> CanActAsOrganizerPageAsync(ClaimsPrincipal currentUser, int organizerProfileId, CancellationToken cancellationToken = default);

        Task<bool> CanCommentAsIdentityAsync(ClaimsPrincipal currentUser, AuthorIdentityType authorType, int? organizerProfileId, CancellationToken cancellationToken = default);

        Task<bool> CanMessageAsIdentityAsync(ClaimsPrincipal currentUser, int conversationId, AuthorIdentityType authorType, int? organizerProfileId, CancellationToken cancellationToken = default);

        Task<bool> CanPublishAsIdentityAsync(ClaimsPrincipal currentUser, AuthorIdentityType authorType, int? organizerProfileId, CancellationToken cancellationToken = default);

        Task<bool> CanViewStoryAsync(ClaimsPrincipal viewer, Story story, CancellationToken cancellationToken = default);

        Task<bool> CanEditProfileStatusAsync(ClaimsPrincipal user, string profileOwnerId, CancellationToken cancellationToken = default);

        Task<bool> CanPinEventAsync(ClaimsPrincipal user, string profileOwnerId, int eventId, CancellationToken cancellationToken = default);

        Task<bool> CanShareEventToProfileAsync(ClaimsPrincipal user, string profileOwnerId, int eventId, CancellationToken cancellationToken = default);

        Task<bool> CanViewProfileSectionAsync(ClaimsPrincipal viewer, string ownerId, ProfileStatusVisibility visibility, CancellationToken cancellationToken = default);

        Task<bool> CanMessageUserAsync(ClaimsPrincipal sender, string receiverId, CancellationToken cancellationToken = default);

        Task<bool> CanCommentAsync(ClaimsPrincipal user, CancellationToken cancellationToken = default);

        Task<bool> CanReviewEventAsync(ClaimsPrincipal user, int eventId, CancellationToken cancellationToken = default);
    }

    public class PlatformPermissionService : IPlatformPermissionService
    {
        private const int MaxNewConversationsPerHour = 5;

        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public PlatformPermissionService(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        public async Task<bool> CanCreatePostAsync(ClaimsPrincipal user, CancellationToken cancellationToken = default)
        {
            if (user.Identity?.IsAuthenticated != true)
            {
                return false;
            }

            if (user.IsInRole(GlobalConstants.Roles.Admin))
            {
                return true;
            }

            var userId = _userManager.GetUserId(user);
            if (string.IsNullOrWhiteSpace(userId) || !user.IsInRole(GlobalConstants.Roles.Organizer))
            {
                return false;
            }

            return await _db.OrganizerData
                .AsNoTracking()
                .AnyAsync(o => o.OrganizerId == userId && o.Approved, cancellationToken);
        }

        public Task<bool> CanCreateStoryAsync(ClaimsPrincipal user, CancellationToken cancellationToken = default)
        {
            return CanCreatePostAsync(user, cancellationToken);
        }

        public async Task<bool> CanManageWorkspaceAsync(ClaimsPrincipal user, int workspaceId, CancellationToken cancellationToken = default)
        {
            if (user.IsInRole(GlobalConstants.Roles.Admin))
            {
                return true;
            }

            var userId = _userManager.GetUserId(user);
            return !string.IsNullOrWhiteSpace(userId)
                && await _db.BusinessWorkspaces.AsNoTracking().AnyAsync(w =>
                    w.Id == workspaceId &&
                    w.OwnerId == userId &&
                    w.Status != BusinessWorkspaceStatus.Archived,
                    cancellationToken);
        }

        public async Task<bool> CanManageOrganizerPageAsync(ClaimsPrincipal user, int organizerProfileId, CancellationToken cancellationToken = default)
        {
            if (user.IsInRole(GlobalConstants.Roles.Admin))
            {
                return true;
            }

            var userId = _userManager.GetUserId(user);
            return !string.IsNullOrWhiteSpace(userId)
                && await _db.OrganizerProfiles.AsNoTracking().AnyAsync(p => p.Id == organizerProfileId && p.OwnerId == userId, cancellationToken);
        }

        public Task<bool> CanActAsUserAsync(ClaimsPrincipal currentUser, string targetUserId, CancellationToken cancellationToken = default)
        {
            var userId = _userManager.GetUserId(currentUser);
            return Task.FromResult(!string.IsNullOrWhiteSpace(userId) && userId == targetUserId);
        }

        public async Task<bool> CanActAsOrganizerPageAsync(ClaimsPrincipal currentUser, int organizerProfileId, CancellationToken cancellationToken = default)
        {
            var userId = _userManager.GetUserId(currentUser);
            if (string.IsNullOrWhiteSpace(userId) || !currentUser.IsInRole(GlobalConstants.Roles.Organizer))
            {
                return false;
            }

            return await _db.OrganizerProfiles
                .AsNoTracking()
                .AnyAsync(p =>
                    p.Id == organizerProfileId &&
                    p.OwnerId == userId &&
                    p.IsActive &&
                    p.IsApproved &&
                    p.Owner.OrganizerData != null &&
                    p.Owner.OrganizerData.Approved &&
                    (p.BusinessWorkspace == null || p.BusinessWorkspace.Status == BusinessWorkspaceStatus.Active),
                    cancellationToken);
        }

        public async Task<bool> CanCommentAsIdentityAsync(ClaimsPrincipal currentUser, AuthorIdentityType authorType, int? organizerProfileId, CancellationToken cancellationToken = default)
        {
            if (!await CanCommentAsync(currentUser, cancellationToken))
            {
                return false;
            }

            return authorType switch
            {
                AuthorIdentityType.User => true,
                AuthorIdentityType.Admin => currentUser.IsInRole(GlobalConstants.Roles.Admin),
                AuthorIdentityType.OrganizerPage => organizerProfileId.HasValue && await CanActAsOrganizerPageAsync(currentUser, organizerProfileId.Value, cancellationToken),
                _ => currentUser.IsInRole(GlobalConstants.Roles.Admin),
            };
        }

        public async Task<bool> CanMessageAsIdentityAsync(ClaimsPrincipal currentUser, int conversationId, AuthorIdentityType authorType, int? organizerProfileId, CancellationToken cancellationToken = default)
        {
            var userId = _userManager.GetUserId(currentUser);
            if (string.IsNullOrWhiteSpace(userId))
            {
                return false;
            }

            var conversation = await _db.Conversations
                .AsNoTracking()
                .Include(c => c.OrganizerProfile)
                .FirstOrDefaultAsync(c => c.Id == conversationId && (c.ParticipantOneId == userId || c.ParticipantTwoId == userId), cancellationToken);
            if (conversation == null)
            {
                return false;
            }

            var otherUserId = conversation.ParticipantOneId == userId ? conversation.ParticipantTwoId : conversation.ParticipantOneId;
            if (conversation.OrganizerProfileId.HasValue)
            {
                if (authorType == AuthorIdentityType.OrganizerPage)
                {
                    return organizerProfileId == conversation.OrganizerProfileId
                        && conversation.Status == ConversationStatus.Accepted
                        && await CanActAsOrganizerPageAsync(currentUser, organizerProfileId.Value, cancellationToken);
                }

                return conversation.OrganizerProfile?.OwnerId != userId
                    && (authorType == AuthorIdentityType.User || authorType == AuthorIdentityType.Admin)
                    && conversation.Status == ConversationStatus.Accepted
                    && await CanCommentAsIdentityAsync(currentUser, authorType, organizerProfileId, cancellationToken);
            }

            if (authorType == AuthorIdentityType.OrganizerPage)
            {
                return false;
            }

            return await CanMessageUserAsync(currentUser, otherUserId, cancellationToken)
                && await CanCommentAsIdentityAsync(currentUser, authorType, organizerProfileId, cancellationToken);
        }

        public async Task<bool> CanPublishAsIdentityAsync(ClaimsPrincipal currentUser, AuthorIdentityType authorType, int? organizerProfileId, CancellationToken cancellationToken = default)
        {
            if (authorType == AuthorIdentityType.Admin)
            {
                return currentUser.IsInRole(GlobalConstants.Roles.Admin);
            }

            return authorType == AuthorIdentityType.OrganizerPage
                && organizerProfileId.HasValue
                && await CanCreatePostAsync(currentUser, cancellationToken)
                && await CanActAsOrganizerPageAsync(currentUser, organizerProfileId.Value, cancellationToken);
        }

        public Task<bool> CanViewStoryAsync(ClaimsPrincipal viewer, Story story, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(story.Author?.OrganizerData?.Approved == true || viewer.IsInRole(GlobalConstants.Roles.Admin));
        }

        public Task<bool> CanEditProfileStatusAsync(ClaimsPrincipal user, string profileOwnerId, CancellationToken cancellationToken = default)
        {
            var userId = _userManager.GetUserId(user);
            return Task.FromResult(!string.IsNullOrWhiteSpace(userId) && userId == profileOwnerId);
        }

        public async Task<bool> CanPinEventAsync(ClaimsPrincipal user, string profileOwnerId, int eventId, CancellationToken cancellationToken = default)
        {
            if (!await CanEditProfileStatusAsync(user, profileOwnerId, cancellationToken))
            {
                return false;
            }

            return await _db.Events.AsNoTracking().AnyAsync(e => e.Id == eventId && e.IsApproved, cancellationToken);
        }

        public Task<bool> CanShareEventToProfileAsync(ClaimsPrincipal user, string profileOwnerId, int eventId, CancellationToken cancellationToken = default)
        {
            return CanPinEventAsync(user, profileOwnerId, eventId, cancellationToken);
        }

        public async Task<bool> CanViewProfileSectionAsync(ClaimsPrincipal viewer, string ownerId, ProfileStatusVisibility visibility, CancellationToken cancellationToken = default)
        {
            if (visibility == ProfileStatusVisibility.Public)
            {
                return true;
            }

            var viewerId = _userManager.GetUserId(viewer);
            if (string.IsNullOrWhiteSpace(viewerId))
            {
                return false;
            }

            if (viewerId == ownerId || viewer.IsInRole(GlobalConstants.Roles.Admin))
            {
                return true;
            }

            return visibility == ProfileStatusVisibility.FollowersOnly
                && await _db.Follows.AnyAsync(f => f.FollowerId == viewerId && f.FollowingId == ownerId, cancellationToken);
        }

        public async Task<bool> CanMessageUserAsync(ClaimsPrincipal sender, string receiverId, CancellationToken cancellationToken = default)
        {
            if (sender.Identity?.IsAuthenticated != true || string.IsNullOrWhiteSpace(receiverId))
            {
                return false;
            }

            var senderId = _userManager.GetUserId(sender);
            if (string.IsNullOrWhiteSpace(senderId) || senderId == receiverId)
            {
                return false;
            }

            if (sender.IsInRole(GlobalConstants.Roles.Admin))
            {
                return true;
            }

            var participants = await _db.Users
                .AsNoTracking()
                .Where(u => u.Id == senderId || u.Id == receiverId)
                .Select(u => new
                {
                    u.Id,
                    IsApprovedOrganizer = u.OrganizerData != null && u.OrganizerData.Approved,
                })
                .ToListAsync(cancellationToken);

            var senderInfo = participants.FirstOrDefault(u => u.Id == senderId);
            var receiverInfo = participants.FirstOrDefault(u => u.Id == receiverId);
            if (senderInfo == null || receiverInfo == null)
            {
                return false;
            }

            var (one, two) = SortParticipants(senderId, receiverId);
            var existingConversationStatus = await _db.Conversations
                .AsNoTracking()
                .Where(c => c.ParticipantOneId == one && c.ParticipantTwoId == two && c.OrganizerProfileId == null)
                .Select(c => (ConversationStatus?)c.Status)
                .FirstOrDefaultAsync(cancellationToken);
            var existingConversation = existingConversationStatus.HasValue;

            if (existingConversationStatus == ConversationStatus.Accepted)
            {
                return true;
            }

            if (!existingConversation)
            {
                var since = DateTime.UtcNow.AddHours(-1);
                var recentStarted = await _db.Conversations
                    .AsNoTracking()
                    .CountAsync(c =>
                        c.CreatedAt >= since &&
                        (c.ParticipantOneId == senderId || c.ParticipantTwoId == senderId),
                        cancellationToken);

                if (recentStarted >= MaxNewConversationsPerHour)
                {
                    return false;
                }
            }

            if (!senderInfo.IsApprovedOrganizer && receiverInfo.IsApprovedOrganizer)
            {
                return true;
            }

            if (senderInfo.IsApprovedOrganizer && !receiverInfo.IsApprovedOrganizer)
            {
                return existingConversation
                    || await _db.Follows.AnyAsync(f => f.FollowerId == receiverId && f.FollowingId == senderId, cancellationToken)
                    || await _db.UserTickets.AnyAsync(ut =>
                        ut.Transaction.UserId == receiverId &&
                        ut.Ticket.Event.OrganizerId == senderId,
                        cancellationToken);
            }

            if (!senderInfo.IsApprovedOrganizer && !receiverInfo.IsApprovedOrganizer)
            {
                return await _db.Follows.AnyAsync(f => f.FollowerId == senderId && f.FollowingId == receiverId, cancellationToken)
                    && await _db.Follows.AnyAsync(f => f.FollowerId == receiverId && f.FollowingId == senderId, cancellationToken);
            }

            return senderInfo.IsApprovedOrganizer && receiverInfo.IsApprovedOrganizer;
        }

        public Task<bool> CanCommentAsync(ClaimsPrincipal user, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(user.Identity?.IsAuthenticated == true);
        }

        public async Task<bool> CanReviewEventAsync(ClaimsPrincipal user, int eventId, CancellationToken cancellationToken = default)
        {
            var userId = _userManager.GetUserId(user);
            if (string.IsNullOrWhiteSpace(userId))
            {
                return false;
            }

            if (user.IsInRole(GlobalConstants.Roles.Admin))
            {
                return true;
            }

            return await _db.UserTickets.AnyAsync(ut =>
                    ut.Transaction.UserId == userId &&
                    ut.Ticket.EventId == eventId,
                    cancellationToken)
                || await _db.EventAttendances.AnyAsync(a =>
                    a.UserId == userId &&
                    a.EventId == eventId &&
                    a.Status == EventAttendanceStatus.Going,
                    cancellationToken);
        }

        private static (string One, string Two) SortParticipants(string first, string second)
        {
            return string.CompareOrdinal(first, second) <= 0 ? (first, second) : (second, first);
        }
    }
}

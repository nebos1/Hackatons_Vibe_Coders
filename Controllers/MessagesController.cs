using EventsApp.Common;
using EventsApp.Data;
using EventsApp.Models;
using EventsApp.Services;
using EventsApp.ViewModels.Social;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using EventsApp.Hubs;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace EventsApp.Controllers
{
    [Authorize]
    [EnableRateLimiting("messages")]
    public class MessagesController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IPlatformPermissionService _permissions;
        private readonly IActingIdentityService _actingIdentity;
        private readonly IHubContext<ChatHub> _hubContext;
        private readonly IPushNotificationService _pushNotifications;

        public MessagesController(
            ApplicationDbContext db,
            UserManager<ApplicationUser> userManager,
            IPlatformPermissionService permissions,
            IActingIdentityService actingIdentity,
            IHubContext<ChatHub> hubContext,
            IPushNotificationService pushNotifications)
        {
            _db = db;
            _userManager = userManager;
            _permissions = permissions;
            _actingIdentity = actingIdentity;
            _hubContext = hubContext;
            _pushNotifications = pushNotifications;
        }

        public async Task<IActionResult> Index()
        {
            var userId = _userManager.GetUserId(User)!;
            var canUseOrganizerIdentity = User.IsInRole(GlobalConstants.Roles.Organizer);

            var conversationRows = await _db.Conversations
                .AsNoTracking()
                .Where(c => c.ParticipantOneId == userId || c.ParticipantTwoId == userId)
                .OrderByDescending(c => c.UpdatedAt)
                .Select(c => new
                {
                    c.Id,
                    c.Token,
                    c.UpdatedAt,
                    c.Status,
                    c.RequestedByUserId,
                    c.OrganizerProfileId,
                    PageName = c.OrganizerProfile != null ? c.OrganizerProfile.DisplayName : null,
                    PageImageUrl = c.OrganizerProfile != null ? c.OrganizerProfile.AvatarImageUrl : null,
                    PageOwnerId = c.OrganizerProfile != null ? c.OrganizerProfile.OwnerId : null,
                    PageCanActAsCurrentUser = c.OrganizerProfile != null
                        && c.OrganizerProfile.OwnerId == userId
                        && canUseOrganizerIdentity
                        && c.OrganizerProfile.IsActive
                        && c.OrganizerProfile.IsApproved
                        && c.OrganizerProfile.Owner.OrganizerData != null
                        && c.OrganizerProfile.Owner.OrganizerData.Approved
                        && (c.OrganizerProfile.BusinessWorkspace == null
                            || c.OrganizerProfile.BusinessWorkspace.Status == BusinessWorkspaceStatus.Active),
                    Other = c.ParticipantOneId == userId ? c.ParticipantTwo : c.ParticipantOne,
                    LastMessage = c.Messages
                        .OrderByDescending(m => m.CreatedAt)
                        .Select(m => m.Content)
                        .FirstOrDefault(),
                    HasMessages = c.Messages.Any(),
                    UnseenCount = c.Messages.Count(m => m.SenderId != userId && m.SeenAt == null),
                })
                .ToListAsync();

            var conversations = conversationRows
                .Select(c =>
                {
                    var currentUserOwnsPage = c.OrganizerProfileId.HasValue && c.PageOwnerId == userId;
                    var currentUserCanActAsPage = currentUserOwnsPage && c.PageCanActAsCurrentUser;
                    var displayName = c.OrganizerProfileId.HasValue && !currentUserOwnsPage
                        ? c.PageName ?? GetDisplayName(c.Other)
                        : GetDisplayName(c.Other);
                    var imageUrl = c.OrganizerProfileId.HasValue && !currentUserOwnsPage
                        ? c.PageImageUrl ?? c.Other.ProfileImageUrl
                        : c.Other.ProfileImageUrl;

                    return new ConversationListItemViewModel
                    {
                        Id = c.Id,
                        Token = c.Token,
                        OtherUserId = c.Other.Id,
                        OtherUserName = displayName,
                        OtherUserImageUrl = imageUrl,
                        LastMessage = c.LastMessage,
                        HasMessages = c.HasMessages,
                        UpdatedAt = c.UpdatedAt,
                        UnseenCount = c.UnseenCount,
                        Status = c.Status,
                        IsRequestedByCurrentUser = c.RequestedByUserId == userId,
                        OrganizerProfileId = c.OrganizerProfileId,
                        PageName = c.PageName,
                        PageImageUrl = c.PageImageUrl,
                        CurrentUserOwnsPage = currentUserOwnsPage,
                        CurrentUserCanActAsPage = currentUserCanActAsPage,
                    };
                })
                .ToList();

            var hasOrganizerPages = await CurrentUserHasActiveOrganizerPageAsync(userId);
            var vm = new MessagesIndexViewModel
            {
                RequestConversations = conversations
                    .Where(c => c.IsIncomingRequest
                        && c.HasMessages
                        && (!c.IsPageConversation || !c.CurrentUserOwnsPage || c.CurrentUserCanActAsPage))
                    .ToList(),
                PersonalConversations = conversations
                    .Where(c => !c.IsPageConversation && !c.IsIncomingRequest)
                    .ToList(),
                PageConversations = conversations
                    .Where(c => c.IsPageConversation && !c.IsIncomingRequest && (!c.CurrentUserOwnsPage || c.CurrentUserCanActAsPage))
                    .ToList(),
                HasOrganizerPages = hasOrganizerPages,
            };

            return View(vm);
        }

        public async Task<IActionResult> Details(Guid token)
        {
            var userId = _userManager.GetUserId(User)!;
            var conversation = await _db.Conversations
                .Include(c => c.ParticipantOne)
                    .ThenInclude(u => u.OrganizerData)
                .Include(c => c.ParticipantTwo)
                    .ThenInclude(u => u.OrganizerData)
                .Include(c => c.OrganizerProfile)
                .Include(c => c.Messages)
                    .ThenInclude(m => m.Sender)
                .Include(c => c.Messages)
                    .ThenInclude(m => m.AuthorOrganizerProfile)
                .Include(c => c.Messages)
                    .ThenInclude(m => m.SharedEvent)
                .Include(c => c.Messages)
                    .ThenInclude(m => m.SharedPost)
                        .ThenInclude(p => p!.Images)
                .Include(c => c.Messages)
                    .ThenInclude(m => m.ReplyToMessage)
                        .ThenInclude(m => m!.Sender)
                .Include(c => c.Messages)
                    .ThenInclude(m => m.ReplyToMessage)
                        .ThenInclude(m => m!.AuthorOrganizerProfile)
                .Include(c => c.Messages)
                    .ThenInclude(m => m.Likes)
                .FirstOrDefaultAsync(c => c.Token == token && (c.ParticipantOneId == userId || c.ParticipantTwoId == userId));

            if (conversation == null)
            {
                return NotFound();
            }

            var other = conversation.ParticipantOneId == userId
                ? conversation.ParticipantTwo
                : conversation.ParticipantOne;
            var currentUserOwnsPage = conversation.OrganizerProfile?.OwnerId == userId;
            if (conversation.OrganizerProfileId.HasValue
                && currentUserOwnsPage
                && !await _permissions.CanActAsOrganizerPageAsync(User, conversation.OrganizerProfileId.Value))
            {
                return Forbid();
            }

            var now = DateTime.UtcNow;
            var unseen = conversation.Messages.Where(m => m.SenderId != userId && m.SeenAt == null).ToList();
            foreach (var message in unseen)
            {
                message.SeenAt = now;
            }

            if (unseen.Count > 0)
            {
                await _db.SaveChangesAsync();
            }

            var displayName = conversation.OrganizerProfileId.HasValue && !currentUserOwnsPage
                ? conversation.OrganizerProfile?.DisplayName ?? GetDisplayName(other)
                : GetDisplayName(other);
            var imageUrl = conversation.OrganizerProfileId.HasValue && !currentUserOwnsPage
                ? conversation.OrganizerProfile?.AvatarImageUrl ?? other.ProfileImageUrl
                : other.ProfileImageUrl;
            var actingIdentities = await GetConversationIdentityOptionsAsync(conversation, userId);
            var canUseConversationIdentity = !conversation.OrganizerProfileId.HasValue
                || !currentUserOwnsPage
                || actingIdentities.Count > 0;
            var hasMessages = conversation.Messages.Any();
            var canSendInitialRequestMessage = conversation.Status == ConversationStatus.Pending
                && conversation.RequestedByUserId == userId
                && !hasMessages
                && actingIdentities.Count > 0;

            var vm = new ConversationDetailsViewModel
            {
                Id = conversation.Id,
                Token = conversation.Token,
                OtherUserId = other.Id,
                OtherUserName = displayName,
                OtherUserImageUrl = imageUrl,
                Status = conversation.Status,
                IsRequestedByCurrentUser = conversation.RequestedByUserId == userId,
                CanRespondToRequest = conversation.Status == ConversationStatus.Pending
                    && conversation.RequestedByUserId != userId
                    && hasMessages
                    && canUseConversationIdentity,
                HasMessages = hasMessages,
                CanSendInitialRequestMessage = canSendInitialRequestMessage,
                CanSendMessage = conversation.Status == ConversationStatus.Accepted && actingIdentities.Count > 0
                    || canSendInitialRequestMessage,
                OrganizerProfileId = conversation.OrganizerProfileId,
                PageName = conversation.OrganizerProfile?.DisplayName,
                PageImageUrl = conversation.OrganizerProfile?.AvatarImageUrl,
                CurrentUserOwnsPage = currentUserOwnsPage,
                Messages = conversation.Messages
                    .OrderBy(m => m.CreatedAt)
                    .Select(m => new MessageBubbleViewModel
                    {
                        Id = m.Id,
                        SenderId = m.SenderId,
                        SenderName = GetMessageDisplayName(m, userId),
                        SenderImageUrl = m.AuthorType == AuthorIdentityType.OrganizerPage ? m.AuthorOrganizerProfile?.AvatarImageUrl : m.Sender.ProfileImageUrl,
                        SenderBadgeKey = GetAuthorBadgeKey(m.AuthorType),
                        SenderBadgeText = GetAuthorBadgeText(m.AuthorType, m.SenderId == userId),
                        Content = m.Content,
                        CreatedAt = m.CreatedAt,
                        SeenAt = m.SeenAt,
                        EditedAt = m.EditedAt,
                        IsDeleted = m.IsDeleted,
                        IsMine = m.SenderId == userId,
                        SharedEventId = m.SharedEventId,
                        SharedEventTitle = m.SharedEvent?.Title,
                        SharedEventImageUrl = m.SharedEvent?.ImageUrl,
                        SharedEventMeta = m.SharedEvent == null
                            ? null
                            : $"{m.SharedEvent.City} · {m.SharedEvent.StartTime:dd.MM HH:mm}",
                        SharedPostId = m.SharedPostId,
                        SharedPostTitle = m.SharedPost == null
                            ? null
                            : m.SharedPost.Content.Length > 90
                                ? m.SharedPost.Content[..90] + "..."
                                : m.SharedPost.Content,
                        SharedPostImageUrl = m.SharedPost?.Images.Select(i => i.ImageUrl).FirstOrDefault(),
                        SharedPostMeta = m.SharedPost == null ? null : $"Post · {m.SharedPost.CreatedAt:dd.MM HH:mm}",
                        ReplyToMessageId = m.ReplyToMessageId,
                        ReplyToSenderName = GetReplySenderName(m.ReplyToMessage, userId),
                        ReplyToPreview = GetReplyPreview(m.ReplyToMessage),
                        ReplyToSharedLabel = GetReplySharedLabel(m.ReplyToMessage),
                        LikesCount = m.Likes.Count,
                        CurrentUserLiked = m.Likes.Any(l => l.UserId == userId),
                    })
                    .ToList(),
                ActingIdentities = actingIdentities,
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditMessage(int id, string content)
        {
            var userId = _userManager.GetUserId(User)!;
            var message = await _db.Messages
                .Include(m => m.Conversation)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (message == null) return NotFound();
            if (message.SenderId != userId) return Forbid();

            var token = message.Conversation.Token;

            if (message.IsDeleted)
            {
                TempData["StatusMessage"] = "Изтрито съобщение не може да се редактира.";
                return RedirectToAction(nameof(Details), new { token });
            }
            if (message.SharedEventId.HasValue || message.SharedPostId.HasValue)
            {
                TempData["StatusMessage"] = "Споделените картички не могат да се редактират.";
                return RedirectToAction(nameof(Details), new { token });
            }
            var trimmed = (content ?? string.Empty).Trim();
            if (trimmed.Length < GlobalConstants.Social.MessageContentMinLength
                || trimmed.Length > GlobalConstants.Social.MessageContentMaxLength)
            {
                TempData["StatusMessage"] = "Съобщението е празно или твърде дълго.";
                return RedirectToAction(nameof(Details), new { token });
            }

            message.Content = trimmed;
            message.EditedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            return RedirectToAction(nameof(Details), new { token });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteMessage(int id)
        {
            var userId = _userManager.GetUserId(User)!;
            var message = await _db.Messages
                .Include(m => m.Conversation)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (message == null) return NotFound();
            if (message.SenderId != userId) return Forbid();

            var token = message.Conversation.Token;

            if (message.IsDeleted)
            {
                return RedirectToAction(nameof(Details), new { token });
            }

            message.IsDeleted = true;
            message.DeletedAt = DateTime.UtcNow;
            message.Content = string.Empty;
            message.SharedEventId = null;
            message.SharedPostId = null;
            await _db.SaveChangesAsync();

            return RedirectToAction(nameof(Details), new { token });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Start(string userId)
        {
            var currentUserId = _userManager.GetUserId(User)!;
            if (string.IsNullOrWhiteSpace(userId) || userId == currentUserId)
            {
                return RedirectToAction("Index", "Posts");
            }

            if (!await _db.Users.AnyAsync(u => u.Id == userId))
            {
                return NotFound();
            }

            var (one, two) = SortParticipants(currentUserId, userId);
            var conversation = await _db.Conversations
                .FirstOrDefaultAsync(c => c.ParticipantOneId == one && c.ParticipantTwoId == two && c.OrganizerProfileId == null);
            var canMessageDirectly = await _permissions.CanMessageUserAsync(User, userId);

            if (conversation == null && !canMessageDirectly)
            {
                var since = DateTime.UtcNow.AddHours(-1);
                var recentRequests = await _db.Conversations
                    .AsNoTracking()
                    .CountAsync(c => c.CreatedAt >= since && c.RequestedByUserId == currentUserId);
                if (recentRequests >= 5)
                {
                    TempData["StatusMessage"] = "Изпрати твърде много заявки за съобщения. Опитай пак след малко.";
                    return RedirectToAction("Details", "Profiles", new { id = userId });
                }
            }

            if (conversation == null)
            {
                conversation = new Conversation
                {
                    ParticipantOneId = one,
                    ParticipantTwoId = two,
                    Status = canMessageDirectly ? ConversationStatus.Accepted : ConversationStatus.Pending,
                    RequestedByUserId = currentUserId,
                };
                _db.Conversations.Add(conversation);
                await _db.SaveChangesAsync();

                if (!canMessageDirectly)
                {
                    TempData["StatusMessage"] = "Заявката за съобщение е изпратена. Ще можете да си пишете, когато бъде одобрена.";
                }
            }
            else if (conversation.Status == ConversationStatus.Declined && canMessageDirectly)
            {
                conversation.Status = ConversationStatus.Accepted;
                conversation.RequestedByUserId = currentUserId;
                conversation.RespondedAt = DateTime.UtcNow;
                conversation.UpdatedAt = conversation.RespondedAt.Value;
                await _db.SaveChangesAsync();
            }
            else if (conversation.Status == ConversationStatus.Declined)
            {
                TempData["StatusMessage"] = "Тази заявка за съобщение е отказана.";
            }

            if (conversation.Status == ConversationStatus.Pending
                && conversation.RequestedByUserId == currentUserId
                && !await _db.Messages.AnyAsync(m => m.ConversationId == conversation.Id))
            {
                TempData["StatusMessage"] = "Write one message to send the request.";
            }

            return RedirectToAction(nameof(Details), new { token = conversation.Token });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> StartPage(int organizerProfileId)
        {
            var currentUserId = _userManager.GetUserId(User)!;
            var page = await _db.OrganizerProfiles
                .AsNoTracking()
                .Include(p => p.Owner)
                    .ThenInclude(o => o.OrganizerData)
                .Include(p => p.BusinessWorkspace)
                .FirstOrDefaultAsync(p =>
                    p.Id == organizerProfileId &&
                    p.IsActive &&
                    p.IsApproved &&
                    (p.BusinessWorkspace == null || p.BusinessWorkspace.Status == BusinessWorkspaceStatus.Active));

            if (page == null)
            {
                return NotFound();
            }

            if (page.Owner.OrganizerData?.Approved != true
                || !await _userManager.IsInRoleAsync(page.Owner, GlobalConstants.Roles.Organizer))
            {
                return NotFound();
            }

            if (page.OwnerId == currentUserId)
            {
                return RedirectToAction(nameof(Index));
            }

            var (one, two) = SortParticipants(currentUserId, page.OwnerId);
            var conversation = await _db.Conversations
                .FirstOrDefaultAsync(c =>
                    c.ParticipantOneId == one &&
                    c.ParticipantTwoId == two &&
                    c.OrganizerProfileId == page.Id);

            var canMessageDirectly = await _permissions.CanMessageUserAsync(User, page.OwnerId);
            if (conversation == null && !canMessageDirectly)
            {
                var since = DateTime.UtcNow.AddHours(-1);
                var recentRequests = await _db.Conversations
                    .AsNoTracking()
                    .CountAsync(c => c.CreatedAt >= since && c.RequestedByUserId == currentUserId);
                if (recentRequests >= 5)
                {
                    TempData["StatusMessage"] = "Too many message requests. Try again later.";
                    return RedirectToAction("Index", "Posts");
                }
            }

            if (conversation == null)
            {
                conversation = new Conversation
                {
                    ParticipantOneId = one,
                    ParticipantTwoId = two,
                    OrganizerProfileId = page.Id,
                    Status = canMessageDirectly ? ConversationStatus.Accepted : ConversationStatus.Pending,
                    RequestedByUserId = currentUserId,
                };
                _db.Conversations.Add(conversation);
                await _db.SaveChangesAsync();
            }
            else if (conversation.Status == ConversationStatus.Declined && canMessageDirectly)
            {
                conversation.Status = ConversationStatus.Accepted;
                conversation.RequestedByUserId = currentUserId;
                conversation.RespondedAt = DateTime.UtcNow;
                conversation.UpdatedAt = conversation.RespondedAt.Value;
                await _db.SaveChangesAsync();
            }

            if (conversation.Status == ConversationStatus.Pending
                && conversation.RequestedByUserId == currentUserId
                && !await _db.Messages.AnyAsync(m => m.ConversationId == conversation.Id))
            {
                TempData["StatusMessage"] = "Write one message to send the request.";
            }

            return RedirectToAction(nameof(Details), new { token = conversation.Token });
        }

        [HttpGet]
        public async Task<IActionResult> ShareEvent(int id)
        {
            var evt = await _db.Events
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == id);
            if (evt == null)
            {
                return NotFound();
            }

            if (!evt.IsApproved)
            {
                TempData["StatusMessage"] = "Събитието трябва да е одобрено, преди да се изпраща в чат.";
                return RedirectToAction("Details", "Events", new { id });
            }

            var userId = _userManager.GetUserId(User)!;
            var vm = new ShareToChatViewModel
            {
                ShareType = "event",
                ShareId = evt.Id,
                Title = evt.Title,
                ImageUrl = evt.ImageUrl,
                Meta = $"{evt.City} - {evt.StartTime:dd.MM HH:mm}",
                Conversations = await BuildShareTargetsAsync(userId),
            };

            if (IsAjaxRequest())
            {
                return PartialView("_ShareChatSheet", vm);
            }

            return View("Share", vm);
        }

        [HttpGet]
        public async Task<IActionResult> SharePost(int id)
        {
            var post = await _db.Posts
                .AsNoTracking()
                .Include(p => p.Images)
                .FirstOrDefaultAsync(p => p.Id == id);
            if (post == null)
            {
                return NotFound();
            }

            var title = string.IsNullOrWhiteSpace(post.Content)
                ? "Evento post"
                : post.Content.Length > 90
                    ? post.Content[..90] + "..."
                    : post.Content;
            var userId = _userManager.GetUserId(User)!;
            var vm = new ShareToChatViewModel
            {
                ShareType = "post",
                ShareId = post.Id,
                Title = title,
                ImageUrl = post.Images.Select(i => i.ImageUrl).FirstOrDefault(),
                Meta = $"Post - {post.CreatedAt:dd.MM HH:mm}",
                Conversations = await BuildShareTargetsAsync(userId),
            };

            if (IsAjaxRequest())
            {
                return PartialView("_ShareChatSheet", vm);
            }

            return View("Share", vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendEvent(int shareId, int conversationId, string? note)
        {
            var exists = await _db.Events
                .AsNoTracking()
                .AnyAsync(e => e.Id == shareId && e.IsApproved);
            if (!exists)
            {
                return NotFound();
            }

            return await SendSharedCardAsync(conversationId, shareId, null, note, "Shared an event.");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendPost(int shareId, int conversationId, string? note)
        {
            var exists = await _db.Posts
                .AsNoTracking()
                .AnyAsync(p => p.Id == shareId);
            if (!exists)
            {
                return NotFound();
            }

            return await SendSharedCardAsync(conversationId, null, shareId, note, "Shared a post.");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleLike(int id)
        {
            var userId = _userManager.GetUserId(User)!;
            var message = await _db.Messages
                .Include(m => m.Conversation)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (message == null)
            {
                return NotFound();
            }

            if (message.Conversation.ParticipantOneId != userId && message.Conversation.ParticipantTwoId != userId)
            {
                return Forbid();
            }

            if (message.IsDeleted)
            {
                return BadRequest();
            }

            var like = await _db.MessageLikes.FindAsync(id, userId);
            var liked = like == null;
            if (liked)
            {
                _db.MessageLikes.Add(new MessageLike
                {
                    MessageId = id,
                    UserId = userId,
                    CreatedAt = DateTime.UtcNow,
                });
            }
            else
            {
                _db.MessageLikes.Remove(like!);
            }

            await _db.SaveChangesAsync();
            var likesCount = await _db.MessageLikes.CountAsync(l => l.MessageId == id);

            return Json(new { liked, likesCount });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(Guid token)
        {
            var userId = _userManager.GetUserId(User)!;
            var conversation = await _db.Conversations
                .Include(c => c.OrganizerProfile)
                .FirstOrDefaultAsync(c => c.Token == token && (c.ParticipantOneId == userId || c.ParticipantTwoId == userId));

            if (conversation == null)
            {
                return NotFound();
            }

            if (conversation.Status != ConversationStatus.Pending || conversation.RequestedByUserId == userId)
            {
                return Forbid();
            }

            if (!await _db.Messages.AnyAsync(m => m.ConversationId == conversation.Id))
            {
                TempData["StatusMessage"] = "The request appears after the first message is sent.";
                return RedirectToAction(nameof(Details), new { token });
            }

            if (conversation.OrganizerProfileId.HasValue
                && conversation.OrganizerProfile?.OwnerId == userId
                && !await _permissions.CanActAsOrganizerPageAsync(User, conversation.OrganizerProfileId.Value))
            {
                return Forbid();
            }

            conversation.Status = ConversationStatus.Accepted;
            conversation.RespondedAt = DateTime.UtcNow;
            conversation.UpdatedAt = conversation.RespondedAt.Value;
            await _db.SaveChangesAsync();

            TempData["StatusMessage"] = "Заявката е одобрена. Вече можете да си пишете.";
            return RedirectToAction(nameof(Details), new { token });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Decline(Guid token)
        {
            var userId = _userManager.GetUserId(User)!;
            var conversation = await _db.Conversations
                .Include(c => c.OrganizerProfile)
                .FirstOrDefaultAsync(c => c.Token == token && (c.ParticipantOneId == userId || c.ParticipantTwoId == userId));

            if (conversation == null)
            {
                return NotFound();
            }

            if (conversation.Status != ConversationStatus.Pending || conversation.RequestedByUserId == userId)
            {
                return Forbid();
            }

            if (conversation.OrganizerProfileId.HasValue
                && conversation.OrganizerProfile?.OwnerId == userId
                && !await _permissions.CanActAsOrganizerPageAsync(User, conversation.OrganizerProfileId.Value))
            {
                return Forbid();
            }

            conversation.Status = ConversationStatus.Declined;
            conversation.RespondedAt = DateTime.UtcNow;
            conversation.UpdatedAt = conversation.RespondedAt.Value;
            await _db.SaveChangesAsync();

            TempData["StatusMessage"] = "Заявката за съобщение е отказана.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Send(Guid token, string content, string? actingIdentityKey, int? replyToMessageId)
        {
            var userId = _userManager.GetUserId(User)!;
            var conversation = await _db.Conversations
                .Include(c => c.OrganizerProfile)
                .FirstOrDefaultAsync(c => c.Token == token && (c.ParticipantOneId == userId || c.ParticipantTwoId == userId));

            if (conversation == null)
            {
                return NotFound();
            }

            var hasMessages = await _db.Messages.AnyAsync(m => m.ConversationId == conversation.Id);
            var isInitialRequestMessage = conversation.Status == ConversationStatus.Pending
                && conversation.RequestedByUserId == userId
                && !hasMessages;

            if (conversation.Status != ConversationStatus.Accepted && !isInitialRequestMessage)
            {
                TempData["StatusMessage"] = conversation.Status == ConversationStatus.Pending
                    ? "Първо заявката за съобщение трябва да бъде одобрена."
                    : "Този разговор не е активен.";
                return RedirectToAction(nameof(Details), new { token });
            }

            var otherUserId = conversation.ParticipantOneId == userId
                ? conversation.ParticipantTwoId
                : conversation.ParticipantOneId;

            if (!conversation.OrganizerProfileId.HasValue
                && !isInitialRequestMessage
                && !await _permissions.CanMessageUserAsync(User, otherUserId))
            {
                TempData["StatusMessage"] = "Messaging is limited to organizer questions, organizer replies, or mutual follows.";
                return Forbid();
            }

            if (string.IsNullOrWhiteSpace(content))
            {
                TempData["StatusMessage"] = "Message cannot be empty.";
                return RedirectToAction(nameof(Details), new { token });
            }

            content = content.Trim();
            if (content.Length > GlobalConstants.Social.MessageContentMaxLength)
            {
                content = content[..GlobalConstants.Social.MessageContentMaxLength];
            }

            var now = DateTime.UtcNow;
            var allowedIdentities = await GetConversationIdentityOptionsAsync(conversation, userId);
            var identityKey = allowedIdentities.FirstOrDefault(i => i.Key == actingIdentityKey)?.Key
                ?? allowedIdentities.FirstOrDefault(i => i.IsDefault)?.Key
                ?? allowedIdentities.FirstOrDefault()?.Key;

            if (string.IsNullOrWhiteSpace(identityKey))
            {
                TempData["StatusMessage"] = "You cannot reply from this identity anymore.";
                return RedirectToAction(nameof(Details), new { token });
            }

            var identity = await _actingIdentity.ResolveAsync(HttpContext, identityKey, conversation.OrganizerProfileId);
            if (identity == null)
            {
                return Forbid();
            }

            var canSendAsIdentity = isInitialRequestMessage
                ? await _permissions.CanCommentAsIdentityAsync(User, identity.Type, identity.OrganizerProfileId)
                : await _permissions.CanMessageAsIdentityAsync(User, conversation.Id, identity.Type, identity.OrganizerProfileId);

            if (!canSendAsIdentity)
            {
                return Forbid();
            }

            if (replyToMessageId.HasValue)
            {
                var canReplyToMessage = await _db.Messages
                    .AsNoTracking()
                    .AnyAsync(m => m.Id == replyToMessageId.Value
                        && m.ConversationId == conversation.Id
                        && !m.IsDeleted);
                if (!canReplyToMessage)
                {
                    replyToMessageId = null;
                }
            }

            var message = new Message
            {
                ConversationId = conversation.Id,
                SenderId = userId,
                AuthorType = identity.Type,
                AuthorOrganizerProfileId = identity.OrganizerProfileId,
                BusinessWorkspaceId = identity.BusinessWorkspaceId,
                Content = content,
                ReplyToMessageId = replyToMessageId,
                CreatedAt = now,
            };

            _db.Messages.Add(message);

            conversation.UpdatedAt = now;
            await _db.SaveChangesAsync();

            // Load saved message with related sender/profile for broadcasting
            var saved = await _db.Messages
                .Include(m => m.Sender)
                .Include(m => m.AuthorOrganizerProfile)
                .Include(m => m.ReplyToMessage)
                    .ThenInclude(m => m!.Sender)
                .Include(m => m.ReplyToMessage)
                    .ThenInclude(m => m!.AuthorOrganizerProfile)
                .Include(m => m.Likes)
                .FirstOrDefaultAsync(m => m.Id == message.Id);

            if (saved != null)
            {
                var payload = BuildMessagePayload(saved, userId);
                await _hubContext.Clients.Group(conversation.Token.ToString()).SendAsync("NewMessage", payload);
                await NotifyConversationParticipantsAsync(conversation.Id, saved);
            }

            if (isInitialRequestMessage)
            {
                TempData["StatusMessage"] = "Message request sent. The conversation will unlock after approval.";
            }

            return RedirectToAction(nameof(Details), new { token });
        }

        [HttpGet]
        [DisableRateLimiting]
        public async Task<IActionResult> Poll(Guid token, int afterId)
        {
            var userId = _userManager.GetUserId(User)!;
            var conversation = await _db.Conversations
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Token == token && (c.ParticipantOneId == userId || c.ParticipantTwoId == userId));

            if (conversation == null) return NotFound();
            if (conversation.Status != ConversationStatus.Accepted)
                return Ok(new { messages = Array.Empty<object>() });

            var messages = await _db.Messages
                .Where(m => m.ConversationId == conversation.Id && m.Id > afterId)
                .Include(m => m.Sender)
                .Include(m => m.AuthorOrganizerProfile)
                .Include(m => m.SharedEvent)
                .Include(m => m.SharedPost)
                    .ThenInclude(p => p!.Images)
                .Include(m => m.ReplyToMessage)
                    .ThenInclude(m => m!.Sender)
                .Include(m => m.ReplyToMessage)
                    .ThenInclude(m => m!.AuthorOrganizerProfile)
                .Include(m => m.Likes)
                .OrderBy(m => m.CreatedAt)
                .ToListAsync();

            var unseenIds = messages
                .Where(m => m.SenderId != userId && m.SeenAt == null)
                .Select(m => m.Id)
                .ToList();

            if (unseenIds.Count > 0)
            {
                await _db.Messages
                    .Where(m => unseenIds.Contains(m.Id))
                    .ExecuteUpdateAsync(s => s.SetProperty(m => m.SeenAt, DateTime.UtcNow));
            }

            var result = messages.Select(m => new
            {
                id = m.Id,
                isMine = m.SenderId == userId,
                senderName = GetMessageDisplayName(m, userId),
                senderImageUrl = m.AuthorType == AuthorIdentityType.OrganizerPage
                    ? m.AuthorOrganizerProfile?.AvatarImageUrl
                    : m.Sender.ProfileImageUrl,
                senderBadgeText = GetAuthorBadgeText(m.AuthorType, m.SenderId == userId),
                content = m.Content,
                createdAt = m.CreatedAt.ToString("dd.MM.yyyy HH:mm"),
                seenAt = (string?)null,
                sharedEventId = m.SharedEventId,
                sharedEventTitle = m.SharedEvent?.Title,
                sharedEventImageUrl = m.SharedEvent?.ImageUrl,
                sharedEventMeta = m.SharedEvent == null ? null : $"{m.SharedEvent.City} · {m.SharedEvent.StartTime:dd.MM HH:mm}",
                sharedPostId = m.SharedPostId,
                sharedPostTitle = m.SharedPost == null
                    ? null
                    : m.SharedPost.Content.Length > 90
                        ? m.SharedPost.Content[..90] + "..."
                        : m.SharedPost.Content,
                sharedPostImageUrl = m.SharedPost?.Images.Select(i => i.ImageUrl).FirstOrDefault(),
                sharedPostMeta = m.SharedPost == null ? null : $"Post · {m.SharedPost.CreatedAt:dd.MM HH:mm}",
                replyToMessageId = m.ReplyToMessageId,
                replyToSenderName = GetReplySenderName(m.ReplyToMessage, userId),
                replyToPreview = GetReplyPreview(m.ReplyToMessage),
                replyToSharedLabel = GetReplySharedLabel(m.ReplyToMessage),
                likesCount = m.Likes.Count,
                currentUserLiked = m.Likes.Any(l => l.UserId == userId),
            });

            return Ok(new { messages = result });
        }

        private async Task<IActionResult> SendSharedCardAsync(
            int conversationId,
            int? sharedEventId,
            int? sharedPostId,
            string? note,
            string fallbackContent)
        {
            var userId = _userManager.GetUserId(User)!;
            var conversation = await _db.Conversations
                .Include(c => c.OrganizerProfile)
                .FirstOrDefaultAsync(c => c.Id == conversationId && (c.ParticipantOneId == userId || c.ParticipantTwoId == userId));

            if (conversation == null)
            {
                return NotFound();
            }

            var hasMessages = await _db.Messages.AnyAsync(m => m.ConversationId == conversationId);
            var isInitialRequestMessage = conversation.Status == ConversationStatus.Pending
                && conversation.RequestedByUserId == userId
                && !hasMessages;

            if (conversation.Status != ConversationStatus.Accepted && !isInitialRequestMessage)
            {
                TempData["StatusMessage"] = conversation.Status == ConversationStatus.Pending
                    ? "The request must be approved before you can send more messages."
                    : "This conversation is not active.";
                return RedirectToAction(nameof(Details), new { token = conversation.Token });
            }

            var otherUserId = conversation.ParticipantOneId == userId
                ? conversation.ParticipantTwoId
                : conversation.ParticipantOneId;

            if (!conversation.OrganizerProfileId.HasValue
                && !isInitialRequestMessage
                && !await _permissions.CanMessageUserAsync(User, otherUserId))
            {
                return Forbid();
            }

            var content = string.IsNullOrWhiteSpace(note) ? fallbackContent : note.Trim();
            if (content.Length > GlobalConstants.Social.MessageContentMaxLength)
            {
                content = content[..GlobalConstants.Social.MessageContentMaxLength];
            }

            var allowedIdentities = await GetConversationIdentityOptionsAsync(conversation, userId);
            var identityKey = allowedIdentities.FirstOrDefault(i => i.IsDefault)?.Key
                ?? allowedIdentities.FirstOrDefault()?.Key;
            if (string.IsNullOrWhiteSpace(identityKey))
            {
                TempData["StatusMessage"] = "You cannot reply from this identity anymore.";
                return RedirectToAction(nameof(Details), new { token = conversation.Token });
            }

            var identity = await _actingIdentity.ResolveAsync(HttpContext, identityKey, conversation.OrganizerProfileId);
            if (identity == null)
            {
                return Forbid();
            }

            var canSendAsIdentity = isInitialRequestMessage
                ? await _permissions.CanCommentAsIdentityAsync(User, identity.Type, identity.OrganizerProfileId)
                : await _permissions.CanMessageAsIdentityAsync(User, conversationId, identity.Type, identity.OrganizerProfileId);

            if (!canSendAsIdentity)
            {
                return Forbid();
            }

            var now = DateTime.UtcNow;
            var message = new Message
            {
                ConversationId = conversationId,
                SenderId = userId,
                AuthorType = identity.Type,
                AuthorOrganizerProfileId = identity.OrganizerProfileId,
                BusinessWorkspaceId = identity.BusinessWorkspaceId,
                Content = content,
                SharedEventId = sharedEventId,
                SharedPostId = sharedPostId,
                CreatedAt = now,
            };

            _db.Messages.Add(message);

            conversation.UpdatedAt = now;
            await _db.SaveChangesAsync();

            // Broadcast saved message
            var saved = await _db.Messages
                .Include(m => m.Sender)
                .Include(m => m.AuthorOrganizerProfile)
                .Include(m => m.SharedEvent)
                .Include(m => m.SharedPost)
                    .ThenInclude(p => p!.Images)
                .Include(m => m.Likes)
                .FirstOrDefaultAsync(m => m.Id == message.Id);

            if (saved != null)
            {
                var payload = BuildMessagePayload(saved, userId);
                await _hubContext.Clients.Group(conversation.Token.ToString()).SendAsync("NewMessage", payload);
                await NotifyConversationParticipantsAsync(conversation.Id, saved);
            }

            TempData["StatusMessage"] = isInitialRequestMessage
                ? "Message request sent. The conversation will unlock after approval."
                : "Sent in chat.";

            if (IsAjaxRequest())
            {
                return Json(new
                {
                    ok = true,
                    token = conversation.Token,
                    message = isInitialRequestMessage
                        ? "Message request sent."
                        : "Sent in chat.",
                });
            }

            return RedirectToAction(nameof(Details), new { token = conversation.Token });
        }

        private object BuildMessagePayload(Message message, string viewerUserId)
        {
            return new
            {
                id = message.Id,
                senderId = message.SenderId,
                isMine = message.SenderId == viewerUserId,
                senderName = message.AuthorType == AuthorIdentityType.OrganizerPage && message.AuthorOrganizerProfile != null
                    ? message.AuthorOrganizerProfile.DisplayName
                    : (message.Sender != null ? GetDisplayName(message.Sender) : string.Empty),
                senderImageUrl = message.AuthorType == AuthorIdentityType.OrganizerPage && message.AuthorOrganizerProfile != null
                    ? message.AuthorOrganizerProfile.AvatarImageUrl
                    : message.Sender?.ProfileImageUrl,
                senderBadgeText = GetAuthorBadgeText(message.AuthorType, message.SenderId == viewerUserId),
                content = message.Content,
                createdAt = message.CreatedAt.ToString("dd.MM.yyyy HH:mm"),
                sharedEventId = message.SharedEventId,
                sharedEventTitle = message.SharedEvent?.Title,
                sharedEventImageUrl = message.SharedEvent?.ImageUrl,
                sharedEventMeta = message.SharedEvent == null ? null : $"{message.SharedEvent.City} · {message.SharedEvent.StartTime:dd.MM HH:mm}",
                sharedPostId = message.SharedPostId,
                sharedPostTitle = message.SharedPost == null
                    ? null
                    : message.SharedPost.Content.Length > 90
                        ? message.SharedPost.Content[..90] + "..."
                        : message.SharedPost.Content,
                sharedPostImageUrl = message.SharedPost?.Images.Select(i => i.ImageUrl).FirstOrDefault(),
                sharedPostMeta = message.SharedPost == null ? null : $"Post · {message.SharedPost.CreatedAt:dd.MM HH:mm}",
                replyToMessageId = message.ReplyToMessageId,
                replyToSenderName = GetReplySenderName(message.ReplyToMessage, viewerUserId),
                replyToPreview = GetReplyPreview(message.ReplyToMessage),
                replyToSharedLabel = GetReplySharedLabel(message.ReplyToMessage),
                likesCount = message.Likes.Count,
                currentUserLiked = message.Likes.Any(l => l.UserId == viewerUserId),
            };
        }

        private async Task NotifyConversationParticipantsAsync(int conversationId, Message message)
        {
            var conversation = await _db.Conversations
                .AsNoTracking()
                .Include(c => c.ParticipantOne)
                .Include(c => c.ParticipantTwo)
                .Include(c => c.OrganizerProfile)
                .FirstOrDefaultAsync(c => c.Id == conversationId);

            if (conversation == null)
            {
                return;
            }

            var participantIds = new[] { conversation.ParticipantOneId, conversation.ParticipantTwoId }
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct()
                .ToList();

            foreach (var viewerUserId in participantIds)
            {
                var payload = await BuildConversationUpdatePayloadAsync(conversation, message, viewerUserId);
                await _hubContext.Clients.User(viewerUserId).SendAsync("ConversationUpdated", payload);

                if (viewerUserId != message.SenderId)
                {
                    var senderName = message.AuthorType == AuthorIdentityType.OrganizerPage && message.AuthorOrganizerProfile != null
                        ? message.AuthorOrganizerProfile.DisplayName
                        : message.Sender != null ? GetDisplayName(message.Sender) : "Evento";
                    var body = message.Content.Length > 120 ? message.Content[..120] + "..." : message.Content;
                    var url = Url.Action(nameof(Details), "Messages", new { token = conversation.Token }) ?? "/inbox";
                    var badgeCount = await CountUnreadMessagesForUserAsync(viewerUserId);
                    await _pushNotifications.SendMessageNotificationAsync(
                        viewerUserId,
                        $"Ново съобщение от {senderName}",
                        body,
                        url,
                        badgeCount);
                }
            }
        }

        private async Task<object> BuildConversationUpdatePayloadAsync(Conversation conversation, Message message, string viewerUserId)
        {
            var other = conversation.ParticipantOneId == viewerUserId
                ? conversation.ParticipantTwo
                : conversation.ParticipantOne;
            var currentUserOwnsPage = conversation.OrganizerProfile?.OwnerId == viewerUserId;
            var displayName = conversation.OrganizerProfileId.HasValue && !currentUserOwnsPage
                ? conversation.OrganizerProfile?.DisplayName ?? GetDisplayName(other)
                : GetDisplayName(other);
            var imageUrl = conversation.OrganizerProfileId.HasValue && !currentUserOwnsPage
                ? conversation.OrganizerProfile?.AvatarImageUrl ?? other.ProfileImageUrl
                : other.ProfileImageUrl;
            var listKey = conversation.Status == ConversationStatus.Pending && conversation.RequestedByUserId != viewerUserId
                ? "requests"
                : conversation.OrganizerProfileId.HasValue ? "page" : "personal";
            var unseenCount = await _db.Messages
                .AsNoTracking()
                .CountAsync(m => m.ConversationId == conversation.Id && m.SenderId != viewerUserId && m.SeenAt == null);
            var totalUnreadCount = await CountUnreadMessagesForUserAsync(viewerUserId);
            var url = Url.Action(nameof(Details), "Messages", new { token = conversation.Token }) ?? "/inbox";
            var initial = string.IsNullOrWhiteSpace(displayName) ? "?" : displayName[..1].ToUpperInvariant();

            return new
            {
                conversationId = conversation.Id,
                token = conversation.Token,
                listKey,
                url,
                name = displayName,
                imageUrl,
                initial,
                pageName = conversation.OrganizerProfile?.DisplayName,
                lastMessage = message.Content,
                updatedAt = message.CreatedAt.ToString("dd.MM HH:mm"),
                unseenCount,
                totalUnreadCount,
                senderId = message.SenderId,
                isMine = message.SenderId == viewerUserId,
            };
        }

        private Task<int> CountUnreadMessagesForUserAsync(string userId)
        {
            return _db.Messages
                .AsNoTracking()
                .CountAsync(m => m.SenderId != userId
                    && m.SeenAt == null
                    && (m.Conversation.ParticipantOneId == userId || m.Conversation.ParticipantTwoId == userId));
        }

        private async Task<IReadOnlyList<ConversationListItemViewModel>> BuildShareTargetsAsync(string userId)
        {
            var canUseOrganizerIdentity = User.IsInRole(GlobalConstants.Roles.Organizer);
            var rows = await _db.Conversations
                .AsNoTracking()
                .Where(c => c.ParticipantOneId == userId || c.ParticipantTwoId == userId)
                .OrderByDescending(c => c.UpdatedAt)
                .Select(c => new
                {
                    c.Id,
                    c.Token,
                    c.UpdatedAt,
                    c.Status,
                    c.RequestedByUserId,
                    c.OrganizerProfileId,
                    PageName = c.OrganizerProfile != null ? c.OrganizerProfile.DisplayName : null,
                    PageImageUrl = c.OrganizerProfile != null ? c.OrganizerProfile.AvatarImageUrl : null,
                    PageOwnerId = c.OrganizerProfile != null ? c.OrganizerProfile.OwnerId : null,
                    PageCanActAsCurrentUser = c.OrganizerProfile != null
                        && c.OrganizerProfile.OwnerId == userId
                        && canUseOrganizerIdentity
                        && c.OrganizerProfile.IsActive
                        && c.OrganizerProfile.IsApproved
                        && c.OrganizerProfile.Owner.OrganizerData != null
                        && c.OrganizerProfile.Owner.OrganizerData.Approved
                        && (c.OrganizerProfile.BusinessWorkspace == null
                            || c.OrganizerProfile.BusinessWorkspace.Status == BusinessWorkspaceStatus.Active),
                    Other = c.ParticipantOneId == userId ? c.ParticipantTwo : c.ParticipantOne,
                    LastMessage = c.Messages
                        .OrderByDescending(m => m.CreatedAt)
                        .Select(m => m.Content)
                        .FirstOrDefault(),
                    HasMessages = c.Messages.Any(),
                    UnseenCount = c.Messages.Count(m => m.SenderId != userId && m.SeenAt == null),
                })
                .ToListAsync();

            return rows
                .Where(c => c.Status == ConversationStatus.Accepted
                    || (c.Status == ConversationStatus.Pending && c.RequestedByUserId == userId && !c.HasMessages))
                .Where(c => !c.OrganizerProfileId.HasValue || c.PageOwnerId != userId || c.PageCanActAsCurrentUser)
                .Select(c =>
                {
                    var currentUserOwnsPage = c.OrganizerProfileId.HasValue && c.PageOwnerId == userId;
                    var currentUserCanActAsPage = currentUserOwnsPage && c.PageCanActAsCurrentUser;
                    var displayName = c.OrganizerProfileId.HasValue && !currentUserOwnsPage
                        ? c.PageName ?? GetDisplayName(c.Other)
                        : GetDisplayName(c.Other);
                    var imageUrl = c.OrganizerProfileId.HasValue && !currentUserOwnsPage
                        ? c.PageImageUrl ?? c.Other.ProfileImageUrl
                        : c.Other.ProfileImageUrl;

                    return new ConversationListItemViewModel
                    {
                        Id = c.Id,
                        Token = c.Token,
                        OtherUserId = c.Other.Id,
                        OtherUserName = displayName,
                        OtherUserImageUrl = imageUrl,
                        LastMessage = c.LastMessage,
                        HasMessages = c.HasMessages,
                        UpdatedAt = c.UpdatedAt,
                        UnseenCount = c.UnseenCount,
                        Status = c.Status,
                        IsRequestedByCurrentUser = c.RequestedByUserId == userId,
                        OrganizerProfileId = c.OrganizerProfileId,
                        PageName = c.PageName,
                        PageImageUrl = c.PageImageUrl,
                        CurrentUserOwnsPage = currentUserOwnsPage,
                        CurrentUserCanActAsPage = currentUserCanActAsPage,
                    };
                })
                .ToList();
        }

        private async Task<IReadOnlyList<ActingIdentityOptionViewModel>> GetConversationIdentityOptionsAsync(Conversation conversation, string userId)
        {
            if (!conversation.OrganizerProfileId.HasValue)
            {
                var personal = await BuildPersonalIdentityOptionAsync(userId);
                return personal == null
                    ? Array.Empty<ActingIdentityOptionViewModel>()
                    : new[] { personal };
            }

            var page = conversation.OrganizerProfile
                ?? await _db.OrganizerProfiles
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p => p.Id == conversation.OrganizerProfileId.Value);
            if (page == null)
            {
                return Array.Empty<ActingIdentityOptionViewModel>();
            }

            if (page.OwnerId == userId)
            {
                if (!await _permissions.CanActAsOrganizerPageAsync(User, page.Id))
                {
                    return Array.Empty<ActingIdentityOptionViewModel>();
                }

                return new[]
                {
                    new ActingIdentityOptionViewModel
                    {
                        Key = $"page:{page.Id}",
                        Label = "Page: " + page.DisplayName,
                        DisplayName = page.DisplayName,
                        ImageUrl = page.AvatarImageUrl,
                        BadgeKey = "identity.page",
                        IsDefault = true,
                    },
                };
            }

            var userIdentity = await BuildPersonalIdentityOptionAsync(userId);
            return userIdentity == null
                ? Array.Empty<ActingIdentityOptionViewModel>()
                : new[] { userIdentity };
        }

        private async Task<ActingIdentityOptionViewModel?> BuildPersonalIdentityOptionAsync(string userId)
        {
            var user = await _db.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null)
            {
                return null;
            }

            return new ActingIdentityOptionViewModel
            {
                Key = "user",
                Label = "Personal profile: " + GetDisplayName(user),
                DisplayName = GetDisplayName(user),
                ImageUrl = user.ProfileImageUrl,
                BadgeKey = User.IsInRole(GlobalConstants.Roles.Admin) ? "identity.admin" : "identity.user",
                IsDefault = true,
            };
        }

        private async Task<bool> CurrentUserHasActiveOrganizerPageAsync(string userId)
        {
            if (!User.IsInRole(GlobalConstants.Roles.Organizer))
            {
                return false;
            }

            var hasApprovedOrganizerData = await _db.OrganizerData
                .AsNoTracking()
                .AnyAsync(o => o.OrganizerId == userId && o.Approved);
            if (!hasApprovedOrganizerData)
            {
                return false;
            }

            return await _db.OrganizerProfiles
                .AsNoTracking()
                .AnyAsync(p =>
                    p.OwnerId == userId &&
                    p.IsActive &&
                    p.IsApproved &&
                    (p.BusinessWorkspace == null || p.BusinessWorkspace.Status == BusinessWorkspaceStatus.Active));
        }

        private static (string One, string Two) SortParticipants(string first, string second)
        {
            return string.CompareOrdinal(first, second) <= 0 ? (first, second) : (second, first);
        }

        private static string GetDisplayName(ApplicationUser user)
        {
            var name = string.Join(" ", new[] { user.FirstName, user.LastName }.Where(v => !string.IsNullOrWhiteSpace(v)));
            return string.IsNullOrWhiteSpace(name) ? user.UserName ?? string.Empty : name;
        }

        private static string GetMessageDisplayName(Message message, string currentUserId)
        {
            if (message.AuthorType == AuthorIdentityType.OrganizerPage && message.AuthorOrganizerProfile != null)
            {
                return message.AuthorOrganizerProfile.DisplayName;
            }

            return message.SenderId == currentUserId ? "You" : GetDisplayName(message.Sender);
        }

        private static string? GetReplySenderName(Message? message, string currentUserId)
        {
            if (message == null)
            {
                return null;
            }

            return GetMessageDisplayName(message, currentUserId);
        }

        private static string? GetReplyPreview(Message? message)
        {
            if (message == null)
            {
                return null;
            }

            if (message.IsDeleted)
            {
                return "Изтрито съобщение";
            }

            var content = string.IsNullOrWhiteSpace(message.Content)
                ? GetReplySharedLabel(message) ?? "Съобщение"
                : message.Content.Trim();

            return content.Length > 96 ? content[..96] + "..." : content;
        }

        private static string? GetReplySharedLabel(Message? message)
        {
            if (message == null)
            {
                return null;
            }

            if (message.SharedEventId.HasValue)
            {
                return "Споделено събитие";
            }

            if (message.SharedPostId.HasValue)
            {
                return "Споделен пост";
            }

            return null;
        }

        private static string GetAuthorBadgeKey(AuthorIdentityType type)
        {
            return type switch
            {
                AuthorIdentityType.OrganizerPage => "identity.page",
                AuthorIdentityType.Admin => "identity.admin",
                AuthorIdentityType.System => "identity.system",
                _ => "identity.user",
            };
        }

        private static string GetAuthorBadgeText(AuthorIdentityType type, bool isYou)
        {
            if (isYou)
            {
                return "You";
            }

            return type switch
            {
                AuthorIdentityType.OrganizerPage => "Organizer Page",
                AuthorIdentityType.Admin => "Admin",
                AuthorIdentityType.System => "System",
                _ => "User",
            };
        }

        private bool IsAjaxRequest()
        {
            return string.Equals(
                Request.Headers["X-Requested-With"].ToString(),
                "XMLHttpRequest",
                StringComparison.OrdinalIgnoreCase);
        }
    }
}

using EventsApp.Data;
using EventsApp.Hubs;
using EventsApp.Models;
using EventsApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace EventsApp.Controllers.Api
{
    [ApiController]
    [Route("api/messages")]
    [IgnoreAntiforgeryToken]
    [Authorize(Policy = "ApiAuth")]
    public class MessagesApiController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IHubContext<ChatHub> _hub;
        private readonly IPushNotificationService _pushNotifications;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<MessagesApiController> _logger;
        private static readonly HashSet<string> AllowedReactionEmoji = new(StringComparer.Ordinal)
        {
            "👍", "😂", "🔥", "👏", "😍", "😮", "😢",
        };

        public MessagesApiController(
            ApplicationDbContext db,
            UserManager<ApplicationUser> userManager,
            IHubContext<ChatHub> hub,
            IPushNotificationService pushNotifications,
            IHttpClientFactory httpClientFactory,
            ILogger<MessagesApiController> logger)
        {
            _db = db;
            _userManager = userManager;
            _hub = hub;
            _pushNotifications = pushNotifications;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        // GET /api/messages/summary
        // Lightweight unread-message counter used by the organizer dashboard
        // attention card. Counts messages addressed to the current user that
        // are not yet seen, across conversations the user has not archived.
        [HttpGet("summary")]
        public async Task<IActionResult> Summary()
        {
            var userId = _userManager.GetUserId(User)!;

            var unreadCount = await _db.Messages
                .AsNoTracking()
                .Where(m => !m.IsDeleted
                            && m.SenderId != userId
                            && m.SeenAt == null
                            && ((m.Conversation.ParticipantOneId == userId && m.Conversation.ArchivedByP1At == null) ||
                                (m.Conversation.ParticipantTwoId == userId && m.Conversation.ArchivedByP2At == null)))
                .CountAsync();

            return Ok(new { unreadCount });
        }

        [HttpGet("conversations")]
        public async Task<IActionResult> Conversations()
        {
            var userId = _userManager.GetUserId(User)!;
            var convos = await _db.Conversations
                .AsNoTracking()
                .Where(c => (c.ParticipantOneId == userId && c.ArchivedByP1At == null) ||
                            (c.ParticipantTwoId == userId && c.ArchivedByP2At == null))
                .Include(c => c.ParticipantOne)
                .Include(c => c.ParticipantTwo)
                .Include(c => c.OrganizerProfile)
                .Include(c => c.Messages.OrderByDescending(m => m.CreatedAt).Take(1))
                .OrderByDescending(c => c.UpdatedAt)
                .ToListAsync();

            var conversationIds = convos.Select(c => c.Id).ToArray();
            var unreadCounts = await _db.Messages
                .AsNoTracking()
                .Where(m => conversationIds.Contains(m.ConversationId)
                            && m.SenderId != userId
                            && m.SeenAt == null
                            && !m.IsDeleted)
                .GroupBy(m => m.ConversationId)
                .Select(g => new { ConversationId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.ConversationId, x => x.Count);

            return Ok(convos.Select(c => MapConversation(
                c,
                userId,
                unreadCounts.GetValueOrDefault(c.Id))));
        }

        [HttpDelete("conversations/{token}")]
        public async Task<IActionResult> ArchiveConversation(Guid token)
        {
            var userId = _userManager.GetUserId(User)!;
            var convo = await _db.Conversations.AsTracking()
                .FirstOrDefaultAsync(c => c.Token == token &&
                    (c.ParticipantOneId == userId || c.ParticipantTwoId == userId));
            if (convo == null) return NotFound();

            if (convo.ParticipantOneId == userId) convo.ArchivedByP1At = DateTime.UtcNow;
            else convo.ArchivedByP2At = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            return Ok(new { archived = true });
        }

        [HttpPost("conversations")]
        public async Task<IActionResult> FindOrCreateConversation([FromBody] StartConversationDto? dto)
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrWhiteSpace(userId) || !await _db.Users.AnyAsync(u => u.Id == userId))
                return Unauthorized(new { error = "Сесията е невалидна. Влез отново." });

            var otherUserId = (dto?.UserId ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(otherUserId) || otherUserId == userId)
                return BadRequest(new { error = "Невалиден получател." });

            if (!await _db.Users.AnyAsync(u => u.Id == otherUserId))
                return NotFound(new { error = "Потребителят не е намерен." });

            var convo = await FindPersonalConversationAsync(userId, otherUserId);

            if (convo == null)
            {
                var (participantOneId, participantTwoId) = OrderParticipantIds(userId, otherUserId);
                convo = new Conversation
                {
                    ParticipantOneId = participantOneId,
                    ParticipantTwoId = participantTwoId,
                    Status = ConversationStatus.Accepted,
                    RequestedByUserId = userId,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                };
                _db.Conversations.Add(convo);

                try
                {
                    await _db.SaveChangesAsync();
                }
                catch (DbUpdateException)
                {
                    _db.Entry(convo).State = EntityState.Detached;
                    convo = await FindPersonalConversationAsync(userId, otherUserId);
                    if (convo == null)
                        return StatusCode(StatusCodes.Status409Conflict, new { error = "Разговорът не можа да бъде създаден. Опитай отново." });
                }
            }

            if (convo.Status != ConversationStatus.Accepted)
            {
                convo.Status = ConversationStatus.Accepted;
                convo.RespondedAt = DateTime.UtcNow;
                convo.UpdatedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync();
            }

            return Ok(new { token = convo.Token.ToString() });
        }

        [HttpPost("conversations/page")]
        public async Task<IActionResult> FindOrCreatePageConversation([FromBody] StartPageConversationDto? dto)
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrWhiteSpace(userId) || !await _db.Users.AnyAsync(u => u.Id == userId))
                return Unauthorized(new { error = "Сесията е невалидна. Влез отново." });

            if (dto == null || dto.OrganizerProfileId <= 0)
                return BadRequest(new { error = "Невалидна страница." });

            var page = await _db.OrganizerProfiles
                .AsNoTracking()
                .Include(p => p.BusinessWorkspace)
                .FirstOrDefaultAsync(p => p.Id == dto.OrganizerProfileId && p.IsActive && p.IsApproved);

            if (page == null) return NotFound(new { error = "Public page не е намерена." });
            if (page.OwnerId == userId) return BadRequest(new { error = "Не можеш да пишеш на собствената си страница." });
            if (page.BusinessWorkspace != null && page.BusinessWorkspace.Status != BusinessWorkspaceStatus.Active)
                return BadRequest(new { error = "Тази страница не приема съобщения." });

            var convo = await _db.Conversations.FirstOrDefaultAsync(c =>
                c.OrganizerProfileId == page.Id &&
                ((c.ParticipantOneId == userId && c.ParticipantTwoId == page.OwnerId) ||
                 (c.ParticipantOneId == page.OwnerId && c.ParticipantTwoId == userId)));

            if (convo == null)
            {
                var (participantOneId, participantTwoId) = OrderParticipantIds(userId, page.OwnerId);
                convo = new Conversation
                {
                    ParticipantOneId = participantOneId,
                    ParticipantTwoId = participantTwoId,
                    OrganizerProfileId = page.Id,
                    Status = ConversationStatus.Accepted,
                    RequestedByUserId = userId,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                };
                _db.Conversations.Add(convo);

                try
                {
                    await _db.SaveChangesAsync();
                }
                catch (DbUpdateException)
                {
                    _db.Entry(convo).State = EntityState.Detached;
                    convo = await _db.Conversations.FirstOrDefaultAsync(c =>
                        c.OrganizerProfileId == page.Id &&
                        ((c.ParticipantOneId == userId && c.ParticipantTwoId == page.OwnerId) ||
                         (c.ParticipantOneId == page.OwnerId && c.ParticipantTwoId == userId)));
                    if (convo == null)
                        return StatusCode(StatusCodes.Status409Conflict, new { error = "Разговорът не можа да бъде създаден. Опитай отново." });
                }
            }

            if (convo.Status != ConversationStatus.Accepted)
            {
                convo.Status = ConversationStatus.Accepted;
                convo.RespondedAt = DateTime.UtcNow;
                convo.UpdatedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync();
            }

            return Ok(new { token = convo.Token.ToString() });
        }

        [HttpGet("conversations/{token}")]
        public async Task<IActionResult> ConversationDetails(Guid token)
        {
            var userId = _userManager.GetUserId(User)!;
            var convo = await _db.Conversations
                .Where(c => c.Token == token)
                .Include(c => c.ParticipantOne)
                .Include(c => c.ParticipantTwo)
                .Include(c => c.OrganizerProfile)
                .Include(c => c.Messages.OrderByDescending(m => m.CreatedAt).Take(80))
                    .ThenInclude(m => m.Sender)
                .Include(c => c.Messages.OrderByDescending(m => m.CreatedAt).Take(80))
                    .ThenInclude(m => m.AuthorOrganizerProfile)
                .Include(c => c.Messages.OrderByDescending(m => m.CreatedAt).Take(80))
                    .ThenInclude(m => m.Likes)
                .Include(c => c.Messages.OrderByDescending(m => m.CreatedAt).Take(80))
                    .ThenInclude(m => m.Reactions)
                .Include(c => c.Messages.OrderByDescending(m => m.CreatedAt).Take(80))
                    .ThenInclude(m => m.ReplyToMessage)
                        .ThenInclude(m => m!.Sender)
                .Include(c => c.Messages.OrderByDescending(m => m.CreatedAt).Take(80))
                    .ThenInclude(m => m.ReplyToMessage)
                        .ThenInclude(m => m!.AuthorOrganizerProfile)
                .Include(c => c.PinnedMessage)
                    .ThenInclude(m => m!.Sender)
                .Include(c => c.PinnedMessage)
                    .ThenInclude(m => m!.Likes)
                .Include(c => c.PinnedMessage)
                    .ThenInclude(m => m!.Reactions)
                .FirstOrDefaultAsync();

            if (convo == null) return NotFound();
            if (convo.ParticipantOneId != userId && convo.ParticipantTwoId != userId) return Forbid();

            var firstUnreadMessageId = convo.Messages
                .Where(m => m.SenderId != userId && m.SeenAt == null && !m.IsDeleted)
                .OrderBy(m => m.CreatedAt)
                .Select(m => (int?)m.Id)
                .FirstOrDefault();

            foreach (var message in convo.Messages.Where(m => m.SenderId != userId && m.SeenAt == null))
            {
                message.SeenAt = DateTime.UtcNow;
            }
            await _db.SaveChangesAsync();

            var summary = MapConversation(
                convo,
                userId,
                await GetUnreadCountAsync(convo.Id, userId));
            var isOutgoingRequest = convo.Status == ConversationStatus.Pending && convo.RequestedByUserId == userId;
            var isIncomingRequest = convo.Status == ConversationStatus.Pending && convo.RequestedByUserId != null && convo.RequestedByUserId != userId;
            var currentUserMutedUntil = convo.ParticipantOneId == userId
                ? convo.MutedByP1Until
                : convo.MutedByP2Until;

            // Initial peer login/logout snapshot so the chat header can pick
            // the right "Active now" / "Active X ago" label without waiting
            // for the SignalR presence query.
            var otherUserId = convo.ParticipantOneId == userId ? convo.ParticipantTwoId : convo.ParticipantOneId;
            var peer = await _db.Users
                .Where(u => u.Id == otherUserId)
                .Select(u => new { u.LastLoginAt, u.LastLogoutAt, u.LastSeenAt })
                .FirstOrDefaultAsync();
            var otherUserLastLoginAt = peer?.LastLoginAt;
            var otherUserLastLogoutAt = peer?.LastLogoutAt ?? peer?.LastSeenAt;

            return Ok(new
            {
                summary.token,
                summary.otherUserId,
                summary.otherUserName,
                summary.otherUserImageUrl,
                summary.organizerProfileId,
                summary.pageName,
                summary.pageImageUrl,
                summary.currentUserOwnsPage,
                summary.isPageConversation,
                otherUserLastLoginAt,
                otherUserLastLogoutAt,
                // Back-compat: older clients still read this field.
                otherUserLastSeenAt = otherUserLastLogoutAt,
                status = convo.Status.ToString(),
                requestedByUserId = convo.RequestedByUserId,
                isOutgoingRequest,
                isIncomingRequest,
                firstUnreadMessageId,
                currentUserMutedUntil,
                pinnedMessage = convo.PinnedMessage == null ? null : MapMessage(convo.PinnedMessage, userId),
                messages = convo.Messages.OrderBy(m => m.CreatedAt).Select(m => MapMessage(m, userId)),
            });
        }

        [HttpPost("conversations/{token}/approve")]
        public Task<IActionResult> Approve(Guid token) => SetStatus(token, ConversationStatus.Accepted);

        [HttpPost("conversations/{token}/decline")]
        public Task<IActionResult> Decline(Guid token) => SetStatus(token, ConversationStatus.Declined);

        [HttpPost("conversations/{token}")]
        public async Task<IActionResult> SendMessage(Guid token, [FromBody] SendMessageDto dto)
        {
            var userId = _userManager.GetUserId(User)!;
            var convo = await _db.Conversations
                .Include(c => c.OrganizerProfile)
                .FirstOrDefaultAsync(c => c.Token == token);

            if (convo == null) return NotFound();
            if (convo.ParticipantOneId != userId && convo.ParticipantTwoId != userId) return Forbid();
            if (convo.Status == ConversationStatus.Declined) return BadRequest(new { error = "Разговорът е отказан." });
            if (string.IsNullOrWhiteSpace(dto.Content) && string.IsNullOrWhiteSpace(dto.AttachmentUrl))
                return BadRequest(new { error = "Съобщението не може да е празно." });

            var authorType = AuthorIdentityType.User;
            int? authorProfileId = null;
            int? workspaceId = null;
            if (convo.OrganizerProfile != null && convo.OrganizerProfile.OwnerId == userId)
            {
                authorType = AuthorIdentityType.OrganizerPage;
                authorProfileId = convo.OrganizerProfile.Id;
                workspaceId = convo.OrganizerProfile.BusinessWorkspaceId;
            }

            // Multi-image bubbles carry extras in attachmentUrls. Only accept
            // image MIME — the new chat UI is image-only by design.
            var extraImages = (dto.AttachmentUrls ?? new List<string>())
                .Select(NormalizeNullable)
                .Where(u => !string.IsNullOrEmpty(u))
                .Cast<string>()
                .ToList();

            var message = new Message
            {
                ConversationId = convo.Id,
                SenderId = userId,
                AuthorType = authorType,
                AuthorOrganizerProfileId = authorProfileId,
                BusinessWorkspaceId = workspaceId,
                ReplyToMessageId = dto.ReplyToMessageId,
                SharedEventId = dto.SharedEventId,
                SharedPostId = dto.SharedPostId,
                Content = dto.Content.Trim(),
                AttachmentUrl = NormalizeNullable(dto.AttachmentUrl),
                AttachmentName = NormalizeNullable(dto.AttachmentName),
                AttachmentMediaType = NormalizeNullable(dto.AttachmentMediaType),
                AttachmentUrlsJson = extraImages.Count > 0 ? JsonSerializer.Serialize(extraImages) : null,
                CreatedAt = DateTime.UtcNow,
            };

            _db.Messages.Add(message);
            convo.UpdatedAt = DateTime.UtcNow;
            if (convo.Status == ConversationStatus.Pending && convo.RequestedByUserId != userId)
            {
                convo.Status = ConversationStatus.Accepted;
                convo.RespondedAt = DateTime.UtcNow;
            }
            await _db.SaveChangesAsync();

            message.Sender = (await _userManager.FindByIdAsync(userId))!;
            message.AuthorOrganizerProfile = authorProfileId.HasValue
                ? await _db.OrganizerProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.Id == authorProfileId.Value)
                : null;

            var mapped = MapMessage(message, userId, token);
            await _hub.Clients.Group(token.ToString()).SendAsync("ReceiveMessage", mapped);
            await BroadcastConversationChangedAsync(convo.Id);
            await SendPushNotificationAsync(convo, message, userId, token);

            return Ok(mapped);
        }

        [HttpPut("messages/{id:int}")]
        public async Task<IActionResult> EditMessage(int id, [FromBody] SendMessageDto dto)
        {
            var userId = _userManager.GetUserId(User)!;
            var message = await _db.Messages.FirstOrDefaultAsync(m => m.Id == id);
            if (message == null) return NotFound();
            if (message.SenderId != userId) return Forbid();
            if (message.IsDeleted) return BadRequest(new { error = "Съобщението е изтрито." });
            if (message.SharedEventId.HasValue || message.SharedPostId.HasValue) return BadRequest(new { error = "Споделените картички не се редактират." });
            if (string.IsNullOrWhiteSpace(dto.Content)) return BadRequest(new { error = "Съобщението не може да е празно." });

            message.Content = dto.Content.Trim();
            message.EditedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return Ok(new { id = message.Id, content = message.Content, editedAt = message.EditedAt });
        }

        [HttpDelete("messages/{id:int}")]
        public async Task<IActionResult> DeleteMessage(int id)
        {
            var userId = _userManager.GetUserId(User)!;
            var message = await _db.Messages
                .Include(m => m.Conversation)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (message == null) return NotFound();
            if (message.SenderId != userId) return Forbid();
            message.Content = string.Empty;
            message.SharedEventId = null;
            message.SharedPostId = null;
            message.AttachmentUrl = null;
            message.AttachmentName = null;
            message.AttachmentMediaType = null;
            message.AttachmentUrlsJson = null;
            message.IsDeleted = true;
            message.DeletedAt = DateTime.UtcNow;
            var wasPinned = message.Conversation.PinnedMessageId == message.Id;
            if (wasPinned)
            {
                message.Conversation.PinnedMessageId = null;
            }
            await _db.SaveChangesAsync();
            if (wasPinned)
            {
                await _hub.Clients.Group(message.Conversation.Token.ToString()).SendAsync("PinnedMessageChanged", null);
            }
            return Ok(new { deleted = true });
        }

        [HttpPost("messages/{id:int}/like")]
        public async Task<IActionResult> LikeMessage(int id)
        {
            var userId = _userManager.GetUserId(User)!;
            var message = await _db.Messages.Include(m => m.Conversation).Include(m => m.Likes).FirstOrDefaultAsync(m => m.Id == id);
            if (message == null) return NotFound();
            if (message.Conversation.ParticipantOneId != userId && message.Conversation.ParticipantTwoId != userId) return Forbid();
            if (!message.Likes.Any(l => l.UserId == userId))
            {
                message.Likes.Add(new MessageLike { MessageId = id, UserId = userId });
                await _db.SaveChangesAsync();
            }
            var count = message.Likes.Count;
            var convoToken = message.Conversation.Token.ToString();
            await _hub.Clients.Group(convoToken).SendAsync("MessageLiked", new { messageId = id, likesCount = count });
            return Ok(new { likesCount = count, currentUserLiked = true });
        }

        [HttpDelete("messages/{id:int}/like")]
        public async Task<IActionResult> UnlikeMessage(int id)
        {
            var userId = _userManager.GetUserId(User)!;
            var message = await _db.Messages.Include(m => m.Conversation).FirstOrDefaultAsync(m => m.Id == id);
            if (message == null) return NotFound();
            var like = await _db.MessageLikes.FirstOrDefaultAsync(l => l.MessageId == id && l.UserId == userId);
            if (like != null)
            {
                _db.MessageLikes.Remove(like);
                await _db.SaveChangesAsync();
            }
            var count = await _db.MessageLikes.CountAsync(l => l.MessageId == id);
            var convoToken = message.Conversation.Token.ToString();
            await _hub.Clients.Group(convoToken).SendAsync("MessageLiked", new { messageId = id, likesCount = count });
            return Ok(new { likesCount = count, currentUserLiked = false });
        }

        [HttpPost("messages/{id:int}/reactions")]
        public async Task<IActionResult> AddReaction(int id, [FromBody] MessageReactionDto dto)
        {
            var userId = _userManager.GetUserId(User)!;
            var emoji = NormalizeReactionEmoji(dto.Emoji);
            if (emoji == null) return BadRequest(new { error = "Невалидна реакция." });

            var message = await _db.Messages
                .Include(m => m.Conversation)
                .Include(m => m.Reactions)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (message == null) return NotFound();
            if (!CanAccessConversation(message.Conversation, userId)) return Forbid();
            if (message.IsDeleted) return BadRequest(new { error = "Съобщението е изтрито." });

            if (!message.Reactions.Any(r => r.UserId == userId && r.Emoji == emoji))
            {
                message.Reactions.Add(new MessageReaction { MessageId = id, UserId = userId, Emoji = emoji });
                await _db.SaveChangesAsync();
            }

            var summary = BuildReactionSummary(message.Reactions, userId);
            await _hub.Clients.Group(message.Conversation.Token.ToString()).SendAsync("MessageReactionsChanged", new
            {
                messageId = id,
                reactions = BuildReactionSummary(message.Reactions, null),
            });
            return Ok(new { reactions = summary });
        }

        [HttpDelete("messages/{id:int}/reactions")]
        public async Task<IActionResult> RemoveReaction(int id, [FromQuery] string emoji)
        {
            var userId = _userManager.GetUserId(User)!;
            var normalizedEmoji = NormalizeReactionEmoji(emoji);
            if (normalizedEmoji == null) return BadRequest(new { error = "Невалидна реакция." });

            var message = await _db.Messages
                .Include(m => m.Conversation)
                .Include(m => m.Reactions)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (message == null) return NotFound();
            if (!CanAccessConversation(message.Conversation, userId)) return Forbid();

            var reaction = message.Reactions.FirstOrDefault(r => r.UserId == userId && r.Emoji == normalizedEmoji);
            if (reaction != null)
            {
                _db.MessageReactions.Remove(reaction);
                await _db.SaveChangesAsync();
            }

            var summary = BuildReactionSummary(message.Reactions, userId);
            await _hub.Clients.Group(message.Conversation.Token.ToString()).SendAsync("MessageReactionsChanged", new
            {
                messageId = id,
                reactions = BuildReactionSummary(message.Reactions, null),
            });
            return Ok(new { reactions = summary });
        }

        [HttpPost("conversations/{token}/mute")]
        public async Task<IActionResult> SetMute(Guid token, [FromBody] MuteConversationDto dto)
        {
            var userId = _userManager.GetUserId(User)!;
            var convo = await _db.Conversations.FirstOrDefaultAsync(c => c.Token == token);
            if (convo == null) return NotFound();
            if (!CanAccessConversation(convo, userId)) return Forbid();

            var mutedUntil = dto.Muted ? DateTime.UtcNow.AddYears(10) : (DateTime?)null;
            if (convo.ParticipantOneId == userId) convo.MutedByP1Until = mutedUntil;
            else convo.MutedByP2Until = mutedUntil;
            await _db.SaveChangesAsync();

            return Ok(new { currentUserMutedUntil = mutedUntil });
        }

        [HttpPost("messages/{id:int}/pin")]
        public async Task<IActionResult> PinMessage(int id)
        {
            var userId = _userManager.GetUserId(User)!;
            var message = await _db.Messages
                .Include(m => m.Conversation)
                .Include(m => m.Sender)
                .Include(m => m.Likes)
                .Include(m => m.Reactions)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (message == null) return NotFound();
            if (!CanAccessConversation(message.Conversation, userId)) return Forbid();
            if (message.IsDeleted) return BadRequest(new { error = "Изтрито съобщение не може да се закачи." });

            message.Conversation.PinnedMessageId = message.Id;
            await _db.SaveChangesAsync();
            var mapped = MapMessage(message, userId);
            await _hub.Clients.Group(message.Conversation.Token.ToString()).SendAsync("PinnedMessageChanged", mapped);
            return Ok(mapped);
        }

        [HttpDelete("conversations/{token}/pin")]
        public async Task<IActionResult> UnpinMessage(Guid token)
        {
            var userId = _userManager.GetUserId(User)!;
            var convo = await _db.Conversations.FirstOrDefaultAsync(c => c.Token == token);
            if (convo == null) return NotFound();
            if (!CanAccessConversation(convo, userId)) return Forbid();

            convo.PinnedMessageId = null;
            await _db.SaveChangesAsync();
            await _hub.Clients.Group(token.ToString()).SendAsync("PinnedMessageChanged", null);
            return Ok(new { pinned = false });
        }

        [HttpGet("link-preview")]
        public async Task<IActionResult> LinkPreview([FromQuery] string url, CancellationToken cancellationToken)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) ||
                await IsPrivateOrLoopbackAsync(uri))
            {
                return BadRequest(new { error = "Невалиден линк." });
            }

            try
            {
                using var response = await _httpClientFactory.CreateClient("link-preview").GetAsync(uri, cancellationToken);
                if (!response.IsSuccessStatusCode) return NoContent();

                var mediaType = response.Content.Headers.ContentType?.MediaType;
                if (mediaType == null || !mediaType.Contains("html", StringComparison.OrdinalIgnoreCase)) return NoContent();

                var html = await response.Content.ReadAsStringAsync(cancellationToken);
                var title = ReadMeta(html, "og:title") ?? ReadTitle(html) ?? uri.Host;
                var description = ReadMeta(html, "og:description") ?? ReadMeta(html, "description");
                var imageUrl = NormalizePreviewUrl(uri, ReadMeta(html, "og:image"));

                return Ok(new
                {
                    url = uri.ToString(),
                    host = uri.Host,
                    title = WebUtility.HtmlDecode(title),
                    description = WebUtility.HtmlDecode(description),
                    imageUrl,
                });
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Could not load preview for {Url}", uri);
                return NoContent();
            }
        }

        private async Task<IActionResult> SetStatus(Guid token, ConversationStatus status)
        {
            var userId = _userManager.GetUserId(User)!;
            var convo = await _db.Conversations.FirstOrDefaultAsync(c => c.Token == token);
            if (convo == null) return NotFound();
            if (convo.ParticipantOneId != userId && convo.ParticipantTwoId != userId) return Forbid();
            if (convo.RequestedByUserId == userId && !User.IsInRole("Admin")) return Forbid();
            convo.Status = status;
            convo.RespondedAt = DateTime.UtcNow;
            convo.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return Ok(new { status = convo.Status.ToString() });
        }

        private Task<Conversation?> FindPersonalConversationAsync(string userId, string otherUserId)
            => _db.Conversations.FirstOrDefaultAsync(c =>
                c.OrganizerProfileId == null &&
                ((c.ParticipantOneId == userId && c.ParticipantTwoId == otherUserId) ||
                 (c.ParticipantOneId == otherUserId && c.ParticipantTwoId == userId)));

        private static (string ParticipantOneId, string ParticipantTwoId) OrderParticipantIds(string firstUserId, string secondUserId)
            => string.CompareOrdinal(firstUserId, secondUserId) <= 0
                ? (firstUserId, secondUserId)
                : (secondUserId, firstUserId);

        private async Task SendPushNotificationAsync(Conversation convo, Message message, string senderUserId, Guid token)
        {
            var recipientUserId = convo.ParticipantOneId == senderUserId
                ? convo.ParticipantTwoId
                : convo.ParticipantOneId;

            if (string.IsNullOrWhiteSpace(recipientUserId) || recipientUserId == senderUserId)
            {
                return;
            }

            var mutedUntil = convo.ParticipantOneId == recipientUserId
                ? convo.MutedByP1Until
                : convo.MutedByP2Until;
            if (mutedUntil.HasValue && mutedUntil.Value > DateTime.UtcNow)
            {
                return;
            }

            var title = message.AuthorType == AuthorIdentityType.OrganizerPage && message.AuthorOrganizerProfile != null
                ? message.AuthorOrganizerProfile.DisplayName
                : message.Sender?.UserName ?? "Evento";

            try
            {
                await _pushNotifications.SendMessageNotificationAsync(
                    recipientUserId,
                    title,
                    BuildMessagePreview(message),
                    $"/inbox/{token}",
                    cancellationToken: HttpContext.RequestAborted);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to queue push notification for conversation {ConversationToken}", token);
            }
        }

        private static string BuildMessagePreview(Message message)
        {
            var raw = string.IsNullOrWhiteSpace(message.Content)
                ? message.AttachmentName ?? "Прикачен файл"
                : message.Content;
            var text = string.Join(' ', raw.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
            return text.Length <= 120 ? text : $"{text[..117]}...";
        }

        private async Task BroadcastConversationChangedAsync(int conversationId)
        {
            var convo = await _db.Conversations
                .AsNoTracking()
                .Include(c => c.ParticipantOne)
                .Include(c => c.ParticipantTwo)
                .Include(c => c.OrganizerProfile)
                .Include(c => c.Messages.OrderByDescending(m => m.CreatedAt).Take(1))
                .FirstAsync(c => c.Id == conversationId);

            var p1Summary = MapConversation(
                convo,
                convo.ParticipantOneId,
                await GetUnreadCountAsync(convo.Id, convo.ParticipantOneId));
            var p2Summary = MapConversation(
                convo,
                convo.ParticipantTwoId,
                await GetUnreadCountAsync(convo.Id, convo.ParticipantTwoId));

            await _hub.Clients.Group(ChatHub.UserGroup(convo.ParticipantOneId)).SendAsync("ConversationChanged", (object)p1Summary);
            await _hub.Clients.Group(ChatHub.UserGroup(convo.ParticipantTwoId)).SendAsync("ConversationChanged", (object)p2Summary);
        }

        private Task<int> GetUnreadCountAsync(int conversationId, string userId)
            => _db.Messages
                .AsNoTracking()
                .Where(m => m.ConversationId == conversationId
                            && m.SenderId != userId
                            && m.SeenAt == null
                            && !m.IsDeleted)
                .CountAsync();

        private static bool CanAccessConversation(Conversation conversation, string userId)
            => conversation.ParticipantOneId == userId || conversation.ParticipantTwoId == userId;

        private static string? NormalizeNullable(string? value)
            => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        private static string? NormalizeReactionEmoji(string? emoji)
        {
            var normalized = emoji?.Trim();
            return normalized != null && AllowedReactionEmoji.Contains(normalized) ? normalized : null;
        }

        private static object[] BuildReactionSummary(IEnumerable<MessageReaction> reactions, string? currentUserId)
            => reactions
                .GroupBy(r => r.Emoji)
                .OrderByDescending(g => g.Count())
                .ThenBy(g => g.Key)
                .Select(g => new
                {
                    emoji = g.Key,
                    count = g.Count(),
                    currentUserReacted = currentUserId != null && g.Any(r => r.UserId == currentUserId),
                })
                .Cast<object>()
                .ToArray();

        private static dynamic MapConversation(Conversation c, string userId, int? unreadCountOverride = null)
        {
            var other = c.ParticipantOneId == userId ? c.ParticipantTwo : c.ParticipantOne;
            var last = c.Messages.OrderByDescending(m => m.CreatedAt).FirstOrDefault();
            var currentUserOwnsPage = c.OrganizerProfile?.OwnerId == userId;
            var displayName = c.OrganizerProfileId.HasValue && !currentUserOwnsPage
                ? c.OrganizerProfile?.DisplayName ?? other.UserName
                : other.UserName;
            var imageUrl = c.OrganizerProfileId.HasValue && !currentUserOwnsPage
                ? c.OrganizerProfile?.AvatarImageUrl ?? other.ProfileImageUrl
                : other.ProfileImageUrl;
            var isIncomingRequest = c.Status == ConversationStatus.Pending && c.RequestedByUserId != userId;

            return new
            {
                token = c.Token.ToString(),
                otherUserId = other.Id,
                otherUserName = displayName,
                otherUserImageUrl = imageUrl,
                organizerProfileId = c.OrganizerProfileId,
                pageName = c.OrganizerProfile?.DisplayName,
                pageImageUrl = c.OrganizerProfile?.AvatarImageUrl,
                currentUserOwnsPage,
                isPageConversation = c.OrganizerProfileId.HasValue,
                isIncomingRequest,
                listKey = isIncomingRequest ? "requests" : c.OrganizerProfileId.HasValue ? "page" : "personal",
                lastMessage = string.IsNullOrWhiteSpace(last?.Content)
                    ? last?.AttachmentName
                    : last.Content,
                lastMessageAt = last?.CreatedAt,
                unreadCount = unreadCountOverride ?? c.Messages.Count(m => m.SenderId != userId && m.SeenAt == null && !m.IsDeleted),
                status = c.Status.ToString(),
            };
        }

        private static object MapMessage(Message m, string userId, Guid? conversationToken = null)
        {
            List<string>? extras = null;
            if (!string.IsNullOrEmpty(m.AttachmentUrlsJson))
            {
                try { extras = JsonSerializer.Deserialize<List<string>>(m.AttachmentUrlsJson); }
                catch { extras = null; }
            }

            return new
            {
                id = m.Id,
                content = m.Content,
                senderId = m.SenderId,
                senderName = m.AuthorType == AuthorIdentityType.OrganizerPage && m.AuthorOrganizerProfile != null
                    ? m.AuthorOrganizerProfile.DisplayName
                    : m.Sender?.UserName,
                senderImageUrl = m.AuthorType == AuthorIdentityType.OrganizerPage && m.AuthorOrganizerProfile != null
                    ? m.AuthorOrganizerProfile.AvatarImageUrl
                    : m.Sender?.ProfileImageUrl,
                senderBadgeText = m.AuthorType == AuthorIdentityType.OrganizerPage ? "Page" : m.SenderId == userId ? "You" : "User",
                createdAt = m.CreatedAt,
                editedAt = m.EditedAt,
                seenAt = m.SeenAt,
                isDeleted = m.IsDeleted,
                likesCount = m.Likes.Count,
                currentUserLiked = m.Likes.Any(l => l.UserId == userId),
                reactions = BuildReactionSummary(m.Reactions, userId),
                attachmentUrl = m.AttachmentUrl,
                attachmentName = m.AttachmentName,
                attachmentMediaType = m.AttachmentMediaType,
                attachmentUrls = extras,
                replyToId = m.ReplyToMessageId,
                replyToContent = m.ReplyToMessage?.Content,
                replyToSenderName = m.ReplyToMessage == null
                    ? null
                    : m.ReplyToMessage.AuthorType == AuthorIdentityType.OrganizerPage && m.ReplyToMessage.AuthorOrganizerProfile != null
                        ? m.ReplyToMessage.AuthorOrganizerProfile.DisplayName
                        : m.ReplyToMessage.Sender?.UserName,
                canEdit = m.SenderId == userId && !m.IsDeleted && !m.SharedEventId.HasValue && !m.SharedPostId.HasValue,
                canDelete = m.SenderId == userId && !m.IsDeleted,
                // Echoed so list rows that listen for ReceiveMessage know
                // which conversation the new bubble belongs to.
                conversationToken = conversationToken?.ToString(),
            };
        }

        private static string? ReadMeta(string html, string key)
        {
            var escaped = Regex.Escape(key);
            var patterns = new[]
            {
                $"""<meta[^>]+(?:property|name)\s*=\s*["']{escaped}["'][^>]+content\s*=\s*["'](?<value>.*?)["'][^>]*>""",
                $"""<meta[^>]+content\s*=\s*["'](?<value>.*?)["'][^>]+(?:property|name)\s*=\s*["']{escaped}["'][^>]*>""",
            };

            foreach (var pattern in patterns)
            {
                var match = Regex.Match(html, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
                if (match.Success) return match.Groups["value"].Value.Trim();
            }

            return null;
        }

        private static string? ReadTitle(string html)
        {
            var match = Regex.Match(html, """<title[^>]*>(?<value>.*?)</title>""", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            return match.Success ? match.Groups["value"].Value.Trim() : null;
        }

        private static string? NormalizePreviewUrl(Uri pageUri, string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            return Uri.TryCreate(pageUri, raw, out var absolute) ? absolute.ToString() : null;
        }

        private static async Task<bool> IsPrivateOrLoopbackAsync(Uri uri)
        {
            if (uri.IsLoopback) return true;

            try
            {
                var addresses = await Dns.GetHostAddressesAsync(uri.Host);
                return addresses.Any(IsPrivateOrLoopback);
            }
            catch
            {
                return true;
            }
        }

        private static bool IsPrivateOrLoopback(IPAddress address)
        {
            if (IPAddress.IsLoopback(address)) return true;
            if (address.AddressFamily == AddressFamily.InterNetwork)
            {
                var bytes = address.GetAddressBytes();
                return bytes[0] == 10
                       || bytes[0] == 127
                       || (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
                       || (bytes[0] == 192 && bytes[1] == 168)
                       || (bytes[0] == 169 && bytes[1] == 254);
            }

            return address.AddressFamily == AddressFamily.InterNetworkV6 &&
                   (address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || address.IsIPv6Multicast);
        }
    }

    public record StartConversationDto(string? UserId);
    public record StartPageConversationDto(int OrganizerProfileId);
    public record SendMessageDto(
        string Content,
        int? ReplyToMessageId,
        int? SharedEventId,
        int? SharedPostId,
        string? AttachmentUrl,
        string? AttachmentName,
        string? AttachmentMediaType,
        List<string>? AttachmentUrls = null);
    public record MessageReactionDto(string? Emoji);
    public record MuteConversationDto(bool Muted);
}

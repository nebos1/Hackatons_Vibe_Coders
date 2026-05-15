using EventsApp.Data;
using EventsApp.Hubs;
using EventsApp.Models;
using EventsApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

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
        private readonly ILogger<MessagesApiController> _logger;

        public MessagesApiController(
            ApplicationDbContext db,
            UserManager<ApplicationUser> userManager,
            IHubContext<ChatHub> hub,
            IPushNotificationService pushNotifications,
            ILogger<MessagesApiController> logger)
        {
            _db = db;
            _userManager = userManager;
            _hub = hub;
            _pushNotifications = pushNotifications;
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

            return Ok(convos.Select(c => MapConversation(c, userId)));
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
                .FirstOrDefaultAsync();

            if (convo == null) return NotFound();
            if (convo.ParticipantOneId != userId && convo.ParticipantTwoId != userId) return Forbid();

            foreach (var message in convo.Messages.Where(m => m.SenderId != userId && m.SeenAt == null))
            {
                message.SeenAt = DateTime.UtcNow;
            }
            await _db.SaveChangesAsync();

            var summary = MapConversation(convo, userId);
            var isOutgoingRequest = convo.Status == ConversationStatus.Pending && convo.RequestedByUserId == userId;
            var isIncomingRequest = convo.Status == ConversationStatus.Pending && convo.RequestedByUserId != null && convo.RequestedByUserId != userId;

            // Initial peer last-seen so the chat header can show "Active X ago"
            // immediately — the SignalR hub will keep it live after that.
            var otherUserId = convo.ParticipantOneId == userId ? convo.ParticipantTwoId : convo.ParticipantOneId;
            var otherUserLastSeenAt = await _db.Users
                .Where(u => u.Id == otherUserId)
                .Select(u => u.LastSeenAt)
                .FirstOrDefaultAsync();

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
                otherUserLastSeenAt,
                status = convo.Status.ToString(),
                requestedByUserId = convo.RequestedByUserId,
                isOutgoingRequest,
                isIncomingRequest,
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
            if (string.IsNullOrWhiteSpace(dto.Content)) return BadRequest(new { error = "Съобщението не може да е празно." });

            var authorType = AuthorIdentityType.User;
            int? authorProfileId = null;
            int? workspaceId = null;
            if (convo.OrganizerProfile != null && convo.OrganizerProfile.OwnerId == userId)
            {
                authorType = AuthorIdentityType.OrganizerPage;
                authorProfileId = convo.OrganizerProfile.Id;
                workspaceId = convo.OrganizerProfile.BusinessWorkspaceId;
            }

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

            var mapped = MapMessage(message, userId);
            await _hub.Clients.Group(token.ToString()).SendAsync("ReceiveMessage", mapped);
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
            var message = await _db.Messages.FirstOrDefaultAsync(m => m.Id == id);
            if (message == null) return NotFound();
            if (message.SenderId != userId) return Forbid();
            message.Content = string.Empty;
            message.SharedEventId = null;
            message.SharedPostId = null;
            message.IsDeleted = true;
            message.DeletedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
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

            var title = message.AuthorType == AuthorIdentityType.OrganizerPage && message.AuthorOrganizerProfile != null
                ? message.AuthorOrganizerProfile.DisplayName
                : message.Sender?.UserName ?? "Evento";

            try
            {
                await _pushNotifications.SendMessageNotificationAsync(
                    recipientUserId,
                    title,
                    BuildMessagePreview(message.Content),
                    $"/inbox/{token}",
                    cancellationToken: HttpContext.RequestAborted);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to queue push notification for conversation {ConversationToken}", token);
            }
        }

        private static string BuildMessagePreview(string content)
        {
            var text = string.Join(' ', content.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
            return text.Length <= 120 ? text : $"{text[..117]}...";
        }

        private static dynamic MapConversation(Conversation c, string userId)
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
                lastMessage = last?.Content,
                lastMessageAt = last?.CreatedAt,
                unreadCount = c.Messages.Count(m => m.SenderId != userId && m.SeenAt == null && !m.IsDeleted),
                status = c.Status.ToString(),
            };
        }

        private static object MapMessage(Message m, string userId) => new
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
            canEdit = m.SenderId == userId && !m.IsDeleted && !m.SharedEventId.HasValue && !m.SharedPostId.HasValue,
            canDelete = m.SenderId == userId && !m.IsDeleted,
        };
    }

    public record StartConversationDto(string? UserId);
    public record StartPageConversationDto(int OrganizerProfileId);
    public record SendMessageDto(string Content, int? ReplyToMessageId, int? SharedEventId, int? SharedPostId);
}

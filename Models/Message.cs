using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using EventsApp.Common;

namespace EventsApp.Models
{
    public class Message
    {
        public Message()
        {
            this.CreatedAt = DateTime.UtcNow;
        }

        [Key]
        public int Id { get; set; }

        [Required]
        [ForeignKey(nameof(Conversation))]
        public int ConversationId { get; set; }

        public Conversation Conversation { get; set; } = null!;

        [Required]
        [ForeignKey(nameof(Sender))]
        public string SenderId { get; set; } = null!;

        public ApplicationUser Sender { get; set; } = null!;

        public AuthorIdentityType AuthorType { get; set; } = AuthorIdentityType.User;

        [ForeignKey(nameof(AuthorOrganizerProfile))]
        public int? AuthorOrganizerProfileId { get; set; }

        public OrganizerProfile? AuthorOrganizerProfile { get; set; }

        [ForeignKey(nameof(BusinessWorkspace))]
        public int? BusinessWorkspaceId { get; set; }

        public BusinessWorkspace? BusinessWorkspace { get; set; }

        [ForeignKey(nameof(SharedEvent))]
        public int? SharedEventId { get; set; }

        public Event? SharedEvent { get; set; }

        [ForeignKey(nameof(SharedPost))]
        public int? SharedPostId { get; set; }

        public Post? SharedPost { get; set; }

        [ForeignKey(nameof(ReplyToMessage))]
        public int? ReplyToMessageId { get; set; }

        public Message? ReplyToMessage { get; set; }

        public ICollection<Message> Replies { get; set; } = new HashSet<Message>();

        public ICollection<MessageLike> Likes { get; set; } = new HashSet<MessageLike>();

        public ICollection<MessageReaction> Reactions { get; set; } = new HashSet<MessageReaction>();

        [Required]
        [MinLength(GlobalConstants.Social.MessageContentMinLength)]
        [MaxLength(GlobalConstants.Social.MessageContentMaxLength)]
        public string Content { get; set; } = null!;

        public string? AttachmentUrl { get; set; }

        public string? AttachmentName { get; set; }

        public string? AttachmentMediaType { get; set; }

        // Additional image URLs for multi-image messages. Stored as a JSON
        // array so the schema stays a simple text column — the API layer
        // serializes/deserializes through System.Text.Json.
        public string? AttachmentUrlsJson { get; set; }

        [Required]
        public DateTime CreatedAt { get; set; }

        public DateTime? SeenAt { get; set; }

        public DateTime? EditedAt { get; set; }

        public bool IsDeleted { get; set; }

        public DateTime? DeletedAt { get; set; }

        // TODO: Add MessageStatus and report metadata when message moderation moves
        // beyond the current MVP permission/rate-limit checks.
    }
}

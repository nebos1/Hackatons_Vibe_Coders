namespace EventsApp.ViewModels.Social
{
    using EventsApp.Models;

    public class ConversationListItemViewModel
    {
        public int Id { get; set; }
        public Guid Token { get; set; }
        public string OtherUserId { get; set; } = null!;
        public string OtherUserName { get; set; } = null!;
        public string? OtherUserImageUrl { get; set; }
        public string? LastMessage { get; set; }
        public bool HasMessages { get; set; }
        public DateTime UpdatedAt { get; set; }
        public int UnseenCount { get; set; }
        public ConversationStatus Status { get; set; }
        public bool IsRequestedByCurrentUser { get; set; }
        public bool IsIncomingRequest => Status == ConversationStatus.Pending && !IsRequestedByCurrentUser;
        public bool IsOutgoingRequest => Status == ConversationStatus.Pending && IsRequestedByCurrentUser;
        public int? OrganizerProfileId { get; set; }
        public bool IsPageConversation => OrganizerProfileId.HasValue;
        public string? PageName { get; set; }
        public string? PageImageUrl { get; set; }
        public bool CurrentUserOwnsPage { get; set; }
        public bool CurrentUserCanActAsPage { get; set; }
        public string ScopeLabel => IsPageConversation
            ? CurrentUserOwnsPage
                ? $"Page inbox: {PageName}"
                : $"To page: {PageName}"
            : "Personal";
    }

    public class MessagesIndexViewModel
    {
        public IReadOnlyList<ConversationListItemViewModel> RequestConversations { get; set; } = Array.Empty<ConversationListItemViewModel>();
        public IReadOnlyList<ConversationListItemViewModel> PersonalConversations { get; set; } = Array.Empty<ConversationListItemViewModel>();
        public IReadOnlyList<ConversationListItemViewModel> PageConversations { get; set; } = Array.Empty<ConversationListItemViewModel>();
        public bool HasOrganizerPages { get; set; }
    }

    public class MessageBubbleViewModel
    {
        public int Id { get; set; }
        public string SenderId { get; set; } = null!;
        public string SenderName { get; set; } = null!;
        public string? SenderImageUrl { get; set; }
        public string SenderBadgeKey { get; set; } = "identity.user";
        public string SenderBadgeText { get; set; } = "User";
        public string Content { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
        public DateTime? SeenAt { get; set; }
        public bool IsMine { get; set; }
        public int? SharedEventId { get; set; }
        public string? SharedEventTitle { get; set; }
        public string? SharedEventImageUrl { get; set; }
        public string? SharedEventMeta { get; set; }
        public int? SharedPostId { get; set; }
        public string? SharedPostTitle { get; set; }
        public string? SharedPostImageUrl { get; set; }
        public string? SharedPostMeta { get; set; }
        public bool HasSharedCard => SharedEventId.HasValue || SharedPostId.HasValue;
    }

    public class ConversationDetailsViewModel
    {
        public int Id { get; set; }
        public Guid Token { get; set; }
        public string OtherUserId { get; set; } = null!;
        public string OtherUserName { get; set; } = null!;
        public string? OtherUserImageUrl { get; set; }
        public ConversationStatus Status { get; set; }
        public bool IsRequestedByCurrentUser { get; set; }
        public bool CanRespondToRequest { get; set; }
        public bool HasMessages { get; set; }
        public bool CanSendInitialRequestMessage { get; set; }
        public bool CanSendMessage { get; set; }
        public bool IsWaitingForApproval => Status == ConversationStatus.Pending && IsRequestedByCurrentUser && HasMessages;
        public int? OrganizerProfileId { get; set; }
        public bool IsPageConversation => OrganizerProfileId.HasValue;
        public string? PageName { get; set; }
        public string? PageImageUrl { get; set; }
        public bool CurrentUserOwnsPage { get; set; }
        public IReadOnlyList<MessageBubbleViewModel> Messages { get; set; } = Array.Empty<MessageBubbleViewModel>();
        public IReadOnlyList<ActingIdentityOptionViewModel> ActingIdentities { get; set; } = Array.Empty<ActingIdentityOptionViewModel>();
    }

    public class ShareToChatViewModel
    {
        public string ShareType { get; set; } = null!;
        public int ShareId { get; set; }
        public string Title { get; set; } = null!;
        public string? ImageUrl { get; set; }
        public string? Meta { get; set; }
        public string ActionName => ShareType == "event" ? "SendEvent" : "SendPost";
        public IReadOnlyList<ConversationListItemViewModel> Conversations { get; set; } = Array.Empty<ConversationListItemViewModel>();
    }
}

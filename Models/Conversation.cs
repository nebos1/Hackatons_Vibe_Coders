using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EventsApp.Models
{
    public class Conversation
    {
        public Conversation()
        {
            this.CreatedAt = DateTime.UtcNow;
            this.UpdatedAt = this.CreatedAt;
            this.Messages = new HashSet<Message>();
        }

        [Key]
        public int Id { get; set; }

        [Required]
        [ForeignKey(nameof(ParticipantOne))]
        public string ParticipantOneId { get; set; } = null!;

        public ApplicationUser ParticipantOne { get; set; } = null!;

        [Required]
        [ForeignKey(nameof(ParticipantTwo))]
        public string ParticipantTwoId { get; set; } = null!;

        public ApplicationUser ParticipantTwo { get; set; } = null!;

        [ForeignKey(nameof(OrganizerProfile))]
        public int? OrganizerProfileId { get; set; }

        public OrganizerProfile? OrganizerProfile { get; set; }

        [Required]
        public Guid Token { get; set; } = Guid.NewGuid();

        [Required]
        public DateTime CreatedAt { get; set; }

        [Required]
        public DateTime UpdatedAt { get; set; }

        [Required]
        public ConversationStatus Status { get; set; } = ConversationStatus.Accepted;

        [ForeignKey(nameof(RequestedByUser))]
        public string? RequestedByUserId { get; set; }

        public ApplicationUser? RequestedByUser { get; set; }

        public DateTime? RespondedAt { get; set; }

        public ICollection<Message> Messages { get; set; }
    }
}

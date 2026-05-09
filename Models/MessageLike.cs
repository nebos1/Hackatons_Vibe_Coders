using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EventsApp.Models
{
    public class MessageLike
    {
        [Required]
        [ForeignKey(nameof(Message))]
        public int MessageId { get; set; }

        public Message Message { get; set; } = null!;

        [Required]
        [ForeignKey(nameof(User))]
        public string UserId { get; set; } = null!;

        public ApplicationUser User { get; set; } = null!;

        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EventsApp.Models
{
    public class UserPushSubscription
    {
        public int Id { get; set; }

        [Required]
        [ForeignKey(nameof(User))]
        public string UserId { get; set; } = null!;

        public ApplicationUser User { get; set; } = null!;

        [Required]
        [MaxLength(2048)]
        public string Endpoint { get; set; } = null!;

        [Required]
        [MaxLength(256)]
        public string P256DH { get; set; } = null!;

        [Required]
        [MaxLength(256)]
        public string Auth { get; set; } = null!;

        [MaxLength(512)]
        public string? UserAgent { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;
    }
}

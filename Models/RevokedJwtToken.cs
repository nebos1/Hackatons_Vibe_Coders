namespace EventsApp.Models
{
    public class RevokedJwtToken
    {
        public string Jti { get; set; } = string.Empty;

        public string UserId { get; set; } = string.Empty;

        public ApplicationUser User { get; set; } = null!;

        public DateTime RevokedAt { get; set; } = DateTime.UtcNow;

        public DateTime ExpiresAt { get; set; }
    }
}

using EventsApp.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace EventsApp.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<OrganizerData> OrganizerData { get; set; } = null!;

        public DbSet<OrganizerProfile> OrganizerProfiles { get; set; } = null!;

        public DbSet<Event> Events { get; set; } = null!;

        public DbSet<Post> Posts { get; set; } = null!;

        public DbSet<PostImage> PostImages { get; set; } = null!;

        public DbSet<PostComment> PostComments { get; set; } = null!;

        public DbSet<PostLike> PostLikes { get; set; } = null!;

        public DbSet<PostSave> PostSaves { get; set; } = null!;

        public DbSet<EventComment> EventComments { get; set; } = null!;

        public DbSet<EventLike> EventLikes { get; set; } = null!;

        public DbSet<EventSave> EventSaves { get; set; } = null!;

        public DbSet<EventAttendance> EventAttendances { get; set; } = null!;

        public DbSet<EventImage> EventImages { get; set; } = null!;

        public DbSet<UserPreferences> UserPreferences { get; set; } = null!;

        public DbSet<Follow> Follows { get; set; } = null!;

        public DbSet<Story> Stories { get; set; } = null!;

        public DbSet<Conversation> Conversations { get; set; } = null!;

        public DbSet<Message> Messages { get; set; } = null!;

        public DbSet<UserActivity> UserActivities { get; set; } = null!;

        public DbSet<Ticket> Tickets { get; set; } = null!;

        public DbSet<Transaction> Transactions { get; set; } = null!;

        public DbSet<UserTicket> UserTickets { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<OrganizerData>(entity =>
            {
                entity.HasKey(o => o.OrganizerId);

                entity.HasOne(o => o.Organizer)
                      .WithOne(u => u.OrganizerData)
                      .HasForeignKey<OrganizerData>(o => o.OrganizerId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<OrganizerProfile>(entity =>
            {
                entity.HasOne(p => p.Owner)
                      .WithMany(u => u.OrganizerProfiles)
                      .HasForeignKey(p => p.OwnerId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(p => new { p.OwnerId, p.DisplayName });
                entity.HasIndex(p => new { p.OwnerId, p.IsDefault });
            });

            builder.Entity<UserPreferences>(entity =>
            {
                entity.HasOne(p => p.User)
                      .WithOne(u => u.UserPreferences)
                      .HasForeignKey<UserPreferences>(p => p.UserId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(p => p.UserId).IsUnique();
            });

            builder.Entity<Event>(entity =>
            {
                entity.HasOne(e => e.Organizer)
                      .WithMany(u => u.Events)
                      .HasForeignKey(e => e.OrganizerId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.OrganizerProfile)
                      .WithMany(p => p.Events)
                      .HasForeignKey(e => e.OrganizerProfileId)
                      .OnDelete(DeleteBehavior.SetNull);
            });

            builder.Entity<Post>(entity =>
            {
                entity.HasOne(p => p.Organizer)
                      .WithMany(u => u.Posts)
                      .HasForeignKey(p => p.OrganizerId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(p => p.Event)
                      .WithMany(e => e.Posts)
                      .HasForeignKey(p => p.EventId)
                      .OnDelete(DeleteBehavior.SetNull);
            });

            builder.Entity<PostImage>(entity =>
            {
                entity.HasOne(pi => pi.Post)
                      .WithMany(p => p.Images)
                      .HasForeignKey(pi => pi.PostId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<PostComment>(entity =>
            {
                entity.HasOne(pc => pc.Post)
                      .WithMany(p => p.Comments)
                      .HasForeignKey(pc => pc.PostId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(pc => pc.User)
                      .WithMany(u => u.PostComments)
                      .HasForeignKey(pc => pc.UserId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<PostLike>(entity =>
            {
                entity.HasOne(pl => pl.Post)
                      .WithMany(p => p.Likes)
                      .HasForeignKey(pl => pl.PostId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(pl => pl.User)
                      .WithMany(u => u.PostLikes)
                      .HasForeignKey(pl => pl.UserId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(pl => new { pl.PostId, pl.UserId }).IsUnique();
            });

            builder.Entity<PostSave>(entity =>
            {
                entity.HasOne(ps => ps.Post)
                      .WithMany(p => p.Saves)
                      .HasForeignKey(ps => ps.PostId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(ps => ps.User)
                      .WithMany(u => u.PostSaves)
                      .HasForeignKey(ps => ps.UserId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(ps => new { ps.PostId, ps.UserId }).IsUnique();
            });

            builder.Entity<EventComment>(entity =>
            {
                entity.HasOne(ec => ec.Event)
                      .WithMany(e => e.Comments)
                      .HasForeignKey(ec => ec.EventId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(ec => ec.User)
                      .WithMany(u => u.EventComments)
                      .HasForeignKey(ec => ec.UserId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<EventLike>(entity =>
            {
                entity.HasOne(el => el.Event)
                      .WithMany(e => e.Likes)
                      .HasForeignKey(el => el.EventId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(el => el.User)
                      .WithMany(u => u.EventLikes)
                      .HasForeignKey(el => el.UserId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(el => new { el.EventId, el.UserId }).IsUnique();
            });

            builder.Entity<EventSave>(entity =>
            {
                entity.HasOne(es => es.Event)
                      .WithMany(e => e.Saves)
                      .HasForeignKey(es => es.EventId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(es => es.User)
                      .WithMany(u => u.EventSaves)
                      .HasForeignKey(es => es.UserId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(es => new { es.EventId, es.UserId }).IsUnique();
            });

            builder.Entity<EventAttendance>(entity =>
            {
                entity.HasOne(ea => ea.Event)
                      .WithMany(e => e.Attendances)
                      .HasForeignKey(ea => ea.EventId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(ea => ea.User)
                      .WithMany(u => u.EventAttendances)
                      .HasForeignKey(ea => ea.UserId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(ea => new { ea.EventId, ea.UserId }).IsUnique();
            });

            builder.Entity<EventImage>(entity =>
            {
                entity.HasOne(ei => ei.Event)
                      .WithMany(e => e.Images)
                      .HasForeignKey(ei => ei.EventId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<Follow>(entity =>
            {
                entity.HasOne(f => f.Follower)
                      .WithMany(u => u.Following)
                      .HasForeignKey(f => f.FollowerId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(f => f.Following)
                      .WithMany(u => u.Followers)
                      .HasForeignKey(f => f.FollowingId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(f => new { f.FollowerId, f.FollowingId }).IsUnique();
            });

            builder.Entity<Story>(entity =>
            {
                entity.HasOne(s => s.Author)
                      .WithMany(u => u.Stories)
                      .HasForeignKey(s => s.AuthorId)
                      .OnDelete(DeleteBehavior.Restrict);

            });

            builder.Entity<Conversation>(entity =>
            {
                entity.HasOne(c => c.ParticipantOne)
                      .WithMany()
                      .HasForeignKey(c => c.ParticipantOneId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(c => c.ParticipantTwo)
                      .WithMany()
                      .HasForeignKey(c => c.ParticipantTwoId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(c => new { c.ParticipantOneId, c.ParticipantTwoId }).IsUnique();
            });

            builder.Entity<Message>(entity =>
            {
                entity.HasOne(m => m.Conversation)
                      .WithMany(c => c.Messages)
                      .HasForeignKey(m => m.ConversationId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(m => m.Sender)
                      .WithMany(u => u.SentMessages)
                      .HasForeignKey(m => m.SenderId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(m => new { m.ConversationId, m.CreatedAt });
            });

            builder.Entity<UserActivity>(entity =>
            {
                entity.HasOne(a => a.User)
                      .WithMany(u => u.UserActivities)
                      .HasForeignKey(a => a.UserId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(a => a.Event)
                      .WithMany(e => e.UserActivities)
                      .HasForeignKey(a => a.EventId)
                      .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(a => a.Post)
                      .WithMany(p => p.UserActivities)
                      .HasForeignKey(a => a.PostId)
                      .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(a => a.TargetUser)
                      .WithMany()
                      .HasForeignKey(a => a.TargetUserId)
                      .OnDelete(DeleteBehavior.SetNull);

                entity.HasIndex(a => new { a.UserId, a.ActivityType, a.CreatedAt });
            });

            builder.Entity<Ticket>(entity =>
            {
                entity.HasOne(t => t.Event)
                      .WithMany(e => e.Tickets)
                      .HasForeignKey(t => t.EventId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<Transaction>(entity =>
            {
                entity.HasOne(t => t.User)
                      .WithMany()
                      .HasForeignKey(t => t.UserId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<UserTicket>(entity =>
            {
                entity.HasOne(ut => ut.Ticket)
                      .WithMany(t => t.UserTickets)
                      .HasForeignKey(ut => ut.TicketId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(ut => ut.Transaction)
                      .WithMany(t => t.UserTickets)
                      .HasForeignKey(ut => ut.TransactionId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(ut => ut.UsedByOrganizer)
                      .WithMany()
                      .HasForeignKey(ut => ut.UsedByOrganizerId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(ut => ut.QrCode).IsUnique();
            });
        }
    }
}

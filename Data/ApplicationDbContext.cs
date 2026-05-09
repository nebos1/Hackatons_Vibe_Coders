using EventsApp.Models;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace EventsApp.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>, IDataProtectionKeyContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<OrganizerData> OrganizerData { get; set; } = null!;

        public DbSet<OrganizerProfile> OrganizerProfiles { get; set; } = null!;

        public DbSet<Event> Events { get; set; } = null!;

        public DbSet<EventChangeRequest> EventChangeRequests { get; set; } = null!;

        public DbSet<EventBoost> EventBoosts { get; set; } = null!;

        public DbSet<Post> Posts { get; set; } = null!;

        public DbSet<PostImage> PostImages { get; set; } = null!;

        public DbSet<PostComment> PostComments { get; set; } = null!;

        public DbSet<PostCommentLike> PostCommentLikes { get; set; } = null!;

        public DbSet<PostLike> PostLikes { get; set; } = null!;

        public DbSet<PostSave> PostSaves { get; set; } = null!;

        public DbSet<EventComment> EventComments { get; set; } = null!;

        public DbSet<EventCommentLike> EventCommentLikes { get; set; } = null!;

        public DbSet<EventLike> EventLikes { get; set; } = null!;

        public DbSet<EventSave> EventSaves { get; set; } = null!;

        public DbSet<EventAttendance> EventAttendances { get; set; } = null!;

        public DbSet<EventImage> EventImages { get; set; } = null!;

        public DbSet<UserPreferences> UserPreferences { get; set; } = null!;

        public DbSet<Follow> Follows { get; set; } = null!;

        public DbSet<Conversation> Conversations { get; set; } = null!;

        public DbSet<Message> Messages { get; set; } = null!;

        public DbSet<MessageLike> MessageLikes { get; set; } = null!;

        public DbSet<UserActivity> UserActivities { get; set; } = null!;

        public DbSet<Ticket> Tickets { get; set; } = null!;

        public DbSet<TicketSectionPrice> TicketSectionPrices { get; set; } = null!;

        public DbSet<Transaction> Transactions { get; set; } = null!;

        public DbSet<UserTicket> UserTickets { get; set; } = null!;

        public DbSet<EventSeries> EventSeries { get; set; } = null!;

        public DbSet<EventOccurrence> EventOccurrences { get; set; } = null!;

        public DbSet<VenueLayout> VenueLayouts { get; set; } = null!;

        public DbSet<LayoutSection> LayoutSections { get; set; } = null!;

        public DbSet<Seat> Seats { get; set; } = null!;

        public DbSet<EventSeatInventory> EventSeatInventories { get; set; } = null!;

        public DbSet<UserProfileSharedEvent> UserProfileSharedEvents { get; set; } = null!;

        public DbSet<BusinessWorkspace> BusinessWorkspaces { get; set; } = null!;

        public DbSet<OrganizerValidatorAssignment> OrganizerValidatorAssignments { get; set; } = null!;

        public DbSet<DayPlan> DayPlans { get; set; } = null!;

        public DbSet<DayPlanItem> DayPlanItems { get; set; } = null!;

        public DbSet<UserPushSubscription> UserPushSubscriptions { get; set; } = null!;

        public DbSet<DataProtectionKey> DataProtectionKeys { get; set; } = null!;

        public DbSet<PasswordResetRequest> PasswordResetRequests { get; set; } = null!;

        public DbSet<EmailConfirmationRequest> EmailConfirmationRequests { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<IdentityUserLogin<string>>(entity =>
            {
                entity.Property(l => l.LoginProvider).HasMaxLength(128);
                entity.Property(l => l.ProviderKey).HasMaxLength(128);
            });

            builder.Entity<IdentityUserToken<string>>(entity =>
            {
                entity.Property(t => t.LoginProvider).HasMaxLength(128);
                entity.Property(t => t.Name).HasMaxLength(128);
            });

            builder.Entity<OrganizerData>(entity =>
            {
                entity.HasKey(o => o.OrganizerId);
                entity.Property(o => o.VipBoostCreditsAvailable).HasDefaultValue(1);
                entity.Property(o => o.VipBoostCreditsUsed).HasDefaultValue(0);

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

                entity.HasOne(p => p.BusinessWorkspace)
                      .WithMany(w => w.OrganizerProfiles)
                      .HasForeignKey(p => p.BusinessWorkspaceId)
                      .OnDelete(DeleteBehavior.NoAction);

                entity.HasIndex(p => new { p.OwnerId, p.DisplayName });
                entity.HasIndex(p => new { p.OwnerId, p.IsDefault });
                entity.HasIndex(p => new { p.BusinessWorkspaceId, p.IsDefaultForWorkspace });
            });

            builder.Entity<BusinessWorkspace>(entity =>
            {
                entity.HasOne(w => w.Owner)
                      .WithMany(u => u.BusinessWorkspaces)
                      .HasForeignKey(w => w.OwnerId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(w => new { w.OwnerId, w.IsDefault });
                entity.HasIndex(w => w.StripeConnectedAccountId);
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

                entity.HasOne(e => e.BusinessWorkspace)
                      .WithMany(w => w.Events)
                      .HasForeignKey(e => e.BusinessWorkspaceId)
                      .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(e => e.VenueLayout)
                      .WithMany(l => l.Events)
                      .HasForeignKey(e => e.VenueLayoutId)
                      .OnDelete(DeleteBehavior.SetNull);

            });

            builder.Entity<OrganizerValidatorAssignment>(entity =>
            {
                entity.HasOne(v => v.Organizer)
                      .WithMany()
                      .HasForeignKey(v => v.OrganizerId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(v => v.ValidatorUser)
                      .WithMany()
                      .HasForeignKey(v => v.ValidatorUserId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(v => v.OrganizerProfile)
                      .WithMany()
                      .HasForeignKey(v => v.OrganizerProfileId)
                      .OnDelete(DeleteBehavior.NoAction);

                entity.HasIndex(v => new { v.OrganizerId, v.ValidatorUserId }).IsUnique();
                entity.HasIndex(v => new { v.OrganizerId, v.IsActive });
                entity.HasIndex(v => new { v.OrganizerProfileId, v.IsActive });
                entity.HasIndex(v => new { v.ValidatorUserId, v.IsActive });
            });

            builder.Entity<EventChangeRequest>(entity =>
            {
                entity.HasOne(r => r.Event)
                      .WithMany(e => e.ChangeRequests)
                      .HasForeignKey(r => r.EventId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(r => r.Organizer)
                      .WithMany()
                      .HasForeignKey(r => r.OrganizerId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(r => r.ReviewedByAdmin)
                      .WithMany()
                      .HasForeignKey(r => r.ReviewedByAdminId)
                      .OnDelete(DeleteBehavior.SetNull);

                entity.HasIndex(r => new { r.EventId, r.Status });
                entity.HasIndex(r => new { r.OrganizerId, r.Status });
            });

            builder.Entity<EventBoost>(entity =>
            {
                entity.HasOne(b => b.Event)
                      .WithMany(e => e.Boosts)
                      .HasForeignKey(b => b.EventId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(b => b.Organizer)
                      .WithMany()
                      .HasForeignKey(b => b.OrganizerId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(b => new { b.EventId, b.CreatedAt });
                entity.HasIndex(b => new { b.OrganizerId, b.CreatedAt });
            });

            builder.Entity<EventSeries>(entity =>
            {
                entity.HasOne(s => s.Event)
                      .WithOne(e => e.EventSeries)
                      .HasForeignKey<EventSeries>(s => s.EventId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(s => s.Organizer)
                      .WithMany(u => u.EventSeries)
                      .HasForeignKey(s => s.OrganizerId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(s => s.EventId).IsUnique();
                entity.HasIndex(s => new { s.OrganizerId, s.Status });
            });

            builder.Entity<EventOccurrence>(entity =>
            {
                entity.HasOne(o => o.EventSeries)
                      .WithMany(s => s.Occurrences)
                      .HasForeignKey(o => o.EventSeriesId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(o => new { o.EventSeriesId, o.StartDateTime }).IsUnique();
                entity.HasIndex(o => o.Status);
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
                      .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(p => p.OrganizerProfile)
                      .WithMany(op => op.Posts)
                      .HasForeignKey(p => p.OrganizerProfileId)
                      .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(p => p.BusinessWorkspace)
                      .WithMany(w => w.Posts)
                      .HasForeignKey(p => p.BusinessWorkspaceId)
                      .OnDelete(DeleteBehavior.NoAction);
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

                entity.HasOne(pc => pc.AuthorOrganizerProfile)
                      .WithMany()
                      .HasForeignKey(pc => pc.AuthorOrganizerProfileId)
                      .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(pc => pc.BusinessWorkspace)
                      .WithMany()
                      .HasForeignKey(pc => pc.BusinessWorkspaceId)
                      .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(pc => pc.ParentComment)
                      .WithMany(pc => pc.Replies)
                      .HasForeignKey(pc => pc.ParentCommentId)
                      .OnDelete(DeleteBehavior.NoAction);

                entity.HasIndex(pc => pc.ParentCommentId);
            });

            builder.Entity<PostCommentLike>(entity =>
            {
                entity.HasOne(pcl => pcl.PostComment)
                      .WithMany(pc => pc.Likes)
                      .HasForeignKey(pcl => pcl.PostCommentId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(pcl => pcl.User)
                      .WithMany(u => u.PostCommentLikes)
                      .HasForeignKey(pcl => pcl.UserId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(pcl => new { pcl.PostCommentId, pcl.UserId }).IsUnique();
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

                entity.HasOne(ec => ec.AuthorOrganizerProfile)
                      .WithMany()
                      .HasForeignKey(ec => ec.AuthorOrganizerProfileId)
                      .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(ec => ec.BusinessWorkspace)
                      .WithMany()
                      .HasForeignKey(ec => ec.BusinessWorkspaceId)
                      .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(ec => ec.ParentComment)
                      .WithMany(ec => ec.Replies)
                      .HasForeignKey(ec => ec.ParentCommentId)
                      .OnDelete(DeleteBehavior.NoAction);

                entity.HasIndex(ec => ec.ParentCommentId);
            });

            builder.Entity<EventCommentLike>(entity =>
            {
                entity.HasOne(ecl => ecl.EventComment)
                      .WithMany(ec => ec.Likes)
                      .HasForeignKey(ecl => ecl.EventCommentId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(ecl => ecl.User)
                      .WithMany(u => u.EventCommentLikes)
                      .HasForeignKey(ecl => ecl.UserId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(ecl => new { ecl.EventCommentId, ecl.UserId }).IsUnique();
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
                entity.Property(ea => ea.Status).HasDefaultValue(EventAttendanceStatus.Interested);
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

                entity.HasOne(c => c.RequestedByUser)
                      .WithMany()
                      .HasForeignKey(c => c.RequestedByUserId)
                      .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(c => c.OrganizerProfile)
                      .WithMany()
                      .HasForeignKey(c => c.OrganizerProfileId)
                      .OnDelete(DeleteBehavior.NoAction);

                entity.HasIndex(c => new { c.ParticipantOneId, c.ParticipantTwoId })
                      .IsUnique()
                      .HasFilter("\"OrganizerProfileId\" IS NULL");

                entity.HasIndex(c => new { c.ParticipantOneId, c.ParticipantTwoId, c.OrganizerProfileId })
                      .IsUnique()
                      .HasFilter("\"OrganizerProfileId\" IS NOT NULL");
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

                entity.HasOne(m => m.AuthorOrganizerProfile)
                      .WithMany()
                      .HasForeignKey(m => m.AuthorOrganizerProfileId)
                      .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(m => m.BusinessWorkspace)
                      .WithMany()
                      .HasForeignKey(m => m.BusinessWorkspaceId)
                      .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(m => m.SharedEvent)
                      .WithMany()
                      .HasForeignKey(m => m.SharedEventId)
                      .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(m => m.SharedPost)
                      .WithMany()
                      .HasForeignKey(m => m.SharedPostId)
                      .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(m => m.ReplyToMessage)
                      .WithMany(m => m.Replies)
                      .HasForeignKey(m => m.ReplyToMessageId)
                      .OnDelete(DeleteBehavior.NoAction);

                entity.HasIndex(m => new { m.ConversationId, m.CreatedAt });
                entity.HasIndex(m => m.SharedEventId);
                entity.HasIndex(m => m.SharedPostId);
                entity.HasIndex(m => m.ReplyToMessageId);
            });

            builder.Entity<MessageLike>(entity =>
            {
                entity.HasKey(l => new { l.MessageId, l.UserId });

                entity.HasOne(l => l.Message)
                      .WithMany(m => m.Likes)
                      .HasForeignKey(l => l.MessageId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(l => l.User)
                      .WithMany()
                      .HasForeignKey(l => l.UserId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(l => l.UserId);
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
                entity.HasIndex(a => new { a.EventId, a.ActivityType, a.CreatedAt });
                entity.HasIndex(a => new { a.PostId, a.ActivityType, a.CreatedAt });
            });

            builder.Entity<ApplicationUser>(entity =>
            {
                entity.HasOne(u => u.PinnedEvent)
                      .WithMany()
                      .HasForeignKey(u => u.PinnedEventId)
                      .OnDelete(DeleteBehavior.SetNull);
            });

            builder.Entity<UserProfileSharedEvent>(entity =>
            {
                entity.HasOne(s => s.User)
                      .WithMany(u => u.ProfileSharedEvents)
                      .HasForeignKey(s => s.UserId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(s => s.Event)
                      .WithMany()
                      .HasForeignKey(s => s.EventId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(s => new { s.UserId, s.EventId }).IsUnique();
                entity.HasIndex(s => new { s.UserId, s.CreatedAt });
            });

            builder.Entity<UserPushSubscription>(entity =>
            {
                entity.HasOne(s => s.User)
                      .WithMany()
                      .HasForeignKey(s => s.UserId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(s => s.Endpoint).IsUnique();
                entity.HasIndex(s => new { s.UserId, s.LastSeenAt });
            });

            builder.Entity<PasswordResetRequest>(entity =>
            {
                entity.HasKey(r => r.Id);
                entity.Property(r => r.Id).HasMaxLength(32);
                entity.Property(r => r.Email).HasMaxLength(256);
                entity.Property(r => r.Code).IsRequired();

                entity.HasOne(r => r.User)
                      .WithMany()
                      .HasForeignKey(r => r.UserId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(r => r.UserId);
                entity.HasIndex(r => r.ExpiresAt);
                entity.HasIndex(r => r.UsedAt);
            });

            builder.Entity<EmailConfirmationRequest>(entity =>
            {
                entity.HasKey(r => r.Id);
                entity.Property(r => r.Id).HasMaxLength(32);
                entity.Property(r => r.Email).HasMaxLength(256);
                entity.Property(r => r.Code).IsRequired();

                entity.HasOne(r => r.User)
                      .WithMany()
                      .HasForeignKey(r => r.UserId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(r => r.UserId);
                entity.HasIndex(r => r.ExpiresAt);
                entity.HasIndex(r => r.UsedAt);
            });

            builder.Entity<Ticket>(entity =>
            {
                entity.Property(t => t.RequiresAttendeeNames)
                      .HasDefaultValue(false);

                entity.HasOne(t => t.Event)
                      .WithMany(e => e.Tickets)
                      .HasForeignKey(t => t.EventId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<TicketSectionPrice>(entity =>
            {
                entity.Property(p => p.Price)
                      .HasColumnType("numeric(18,2)");

                entity.HasOne(p => p.Ticket)
                      .WithMany(t => t.SectionPrices)
                      .HasForeignKey(p => p.TicketId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(p => p.Section)
                      .WithMany()
                      .HasForeignKey(p => p.SectionId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(p => new { p.TicketId, p.SectionId }).IsUnique();
            });

            builder.Entity<Transaction>(entity =>
            {
                entity.HasOne(t => t.User)
                      .WithMany()
                      .HasForeignKey(t => t.UserId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(t => t.BusinessWorkspace)
                      .WithMany(w => w.Transactions)
                      .HasForeignKey(t => t.BusinessWorkspaceId)
                      .OnDelete(DeleteBehavior.NoAction);
            });

            builder.Entity<UserTicket>(entity =>
            {
                entity.Property(ut => ut.AttendeeName)
                      .HasMaxLength(120);

                entity.HasOne(ut => ut.Ticket)
                      .WithMany(t => t.UserTickets)
                      .HasForeignKey(ut => ut.TicketId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(ut => ut.EventOccurrence)
                      .WithMany(o => o.UserTickets)
                      .HasForeignKey(ut => ut.EventOccurrenceId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(ut => ut.Seat)
                      .WithMany()
                      .HasForeignKey(ut => ut.SeatId)
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
                entity.HasIndex(ut => ut.PurchaseGroupId);
            });

            builder.Entity<VenueLayout>(entity =>
            {
                entity.HasOne(l => l.Organizer)
                      .WithMany(u => u.VenueLayouts)
                      .HasForeignKey(l => l.OrganizerId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(l => new { l.OrganizerId, l.Name, l.Version });
            });

            builder.Entity<LayoutSection>(entity =>
            {
                entity.Property(s => s.FloorName)
                      .HasMaxLength(80)
                      .HasDefaultValue("Floor 1");

                entity.Property(s => s.Shape)
                      .HasMaxLength(32)
                      .HasDefaultValue("Rectangle");

                entity.Property(s => s.ColorHex)
                      .HasMaxLength(16)
                      .HasDefaultValue("#2456ff");

                entity.HasOne(s => s.VenueLayout)
                      .WithMany(l => l.Sections)
                      .HasForeignKey(s => s.VenueLayoutId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(s => new { s.VenueLayoutId, s.Name });
            });

            builder.Entity<Seat>(entity =>
            {
                entity.Property(s => s.Label)
                      .HasMaxLength(48);

                entity.Property(s => s.Radius)
                      .HasDefaultValue(16d);

                entity.Property(s => s.Capacity)
                      .HasDefaultValue(1);

                entity.Property(s => s.IsCapacityUnlimited)
                      .HasDefaultValue(false);

                entity.HasOne(s => s.VenueLayout)
                      .WithMany(l => l.Seats)
                      .HasForeignKey(s => s.VenueLayoutId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(s => s.Section)
                      .WithMany(sec => sec.Seats)
                      .HasForeignKey(s => s.SectionId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(s => new { s.SectionId, s.Row, s.Number }).IsUnique();
            });

            builder.Entity<DayPlan>(entity =>
            {
                entity.HasOne(p => p.User)
                      .WithMany()
                      .HasForeignKey(p => p.UserId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(p => new { p.UserId, p.PlannedFor });
                entity.HasIndex(p => p.ShareToken).IsUnique().HasFilter("\"ShareToken\" IS NOT NULL");
            });

            builder.Entity<DayPlanItem>(entity =>
            {
                entity.HasOne(i => i.DayPlan)
                      .WithMany(p => p.Items)
                      .HasForeignKey(i => i.DayPlanId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(i => i.Event)
                      .WithMany()
                      .HasForeignKey(i => i.EventId)
                      .OnDelete(DeleteBehavior.SetNull);

                entity.HasIndex(i => new { i.DayPlanId, i.Order });
            });

            builder.Entity<EventSeatInventory>(entity =>
            {
                entity.HasOne(i => i.Event)
                      .WithMany(e => e.SeatInventories)
                      .HasForeignKey(i => i.EventId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(i => i.EventOccurrence)
                      .WithMany(o => o.SeatInventories)
                      .HasForeignKey(i => i.EventOccurrenceId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(i => i.Seat)
                      .WithMany(s => s.Inventories)
                      .HasForeignKey(i => i.SeatId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(i => i.ReservedByUser)
                      .WithMany()
                      .HasForeignKey(i => i.ReservedByUserId)
                      .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(i => i.UserTicket)
                      .WithOne(ut => ut.SeatInventory)
                      .HasForeignKey<EventSeatInventory>(i => i.TicketId)
                      .OnDelete(DeleteBehavior.SetNull);

                entity.HasIndex(i => new { i.EventId, i.SeatId })
                      .IsUnique()
                      .HasFilter("\"EventId\" IS NOT NULL AND \"EventOccurrenceId\" IS NULL");

                entity.HasIndex(i => new { i.EventOccurrenceId, i.SeatId })
                      .IsUnique()
                      .HasFilter("\"EventOccurrenceId\" IS NOT NULL");

                entity.HasIndex(i => new { i.Status, i.ReservedUntil });
            });
        }
    }
}

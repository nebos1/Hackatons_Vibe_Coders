using EventsApp.Common;
using EventsApp.Data;
using EventsApp.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace EventsApp.Controllers.Api
{
    [ApiController]
    [Route("api/organizer")]
    [IgnoreAntiforgeryToken]
    [Authorize(Policy = "ApiAuth")]
    public class OrganizerApiController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public OrganizerApiController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        private bool IsOrganizer => User.IsInRole(GlobalConstants.Roles.Organizer) || User.IsInRole(GlobalConstants.Roles.Admin);

        // GET /api/organizer/dashboard
        [HttpGet("dashboard")]
        public async Task<IActionResult> Dashboard()
        {
            if (!IsOrganizer) return Forbid();
            var userId = UserId;

            var orgData = await _db.OrganizerData.AsNoTracking().FirstOrDefaultAsync(o => o.OrganizerId == userId);
            if (orgData == null) return NotFound(new { error = "Нямаш организаторски профил." });

            var paid = GlobalConstants.TransactionStatuses.Paid;
            var now = DateTime.UtcNow;
            var since30 = now.AddDays(-29).Date;

            var ticketsSoldCount = await _db.UserTickets.CountAsync(ut => ut.Ticket.Event.OrganizerId == userId && ut.Transaction.Status == paid);
            var totalRevenue = await _db.UserTickets.Where(ut => ut.Ticket.Event.OrganizerId == userId && ut.Transaction.Status == paid).SumAsync(ut => (decimal?)ut.Ticket.Price) ?? 0m;
            var totalLikes = await _db.EventLikes.CountAsync(l => l.Event.OrganizerId == userId);
            var totalViews = await _db.UserActivities.CountAsync(a => a.ActivityType == UserActivityType.EventViewed && a.Event != null && a.Event.OrganizerId == userId);
            var last30Views = await _db.UserActivities.CountAsync(a => a.ActivityType == UserActivityType.EventViewed && a.CreatedAt >= since30 && a.Event != null && a.Event.OrganizerId == userId);
            var upcomingCount = await _db.Events.CountAsync(e => e.OrganizerId == userId && e.StartTime >= now);
            var pastCount = await _db.Events.CountAsync(e => e.OrganizerId == userId && e.EndTime < now);
            var eventsCount = await _db.Events.CountAsync(e => e.OrganizerId == userId);
            var postsCount = await _db.Posts.CountAsync(p => p.OrganizerId == userId);

            var last30Sold = await _db.UserTickets.CountAsync(ut => ut.Ticket.Event.OrganizerId == userId && ut.Transaction.Status == paid && ut.CreatedAt >= since30);
            var last30Revenue = await _db.UserTickets.Where(ut => ut.Ticket.Event.OrganizerId == userId && ut.Transaction.Status == paid && ut.CreatedAt >= since30).SumAsync(ut => (decimal?)ut.Ticket.Price) ?? 0m;

            var recentEvents = await _db.Events
                .AsNoTracking()
                .Where(e => e.OrganizerId == userId)
                .OrderByDescending(e => e.CreatedAt)
                .Take(5)
                .Select(e => new
                {
                    id = e.Id,
                    title = e.Title,
                    city = e.City,
                    startTime = e.StartTime,
                    isApproved = e.IsApproved,
                    likesCount = e.Likes.Count,
                    commentsCount = e.Comments.Count,
                })
                .ToListAsync();

            var recentPosts = await _db.Posts
                .AsNoTracking()
                .Where(p => p.OrganizerId == userId)
                .OrderByDescending(p => p.CreatedAt)
                .Take(5)
                .Select(p => new
                {
                    id = p.Id,
                    content = p.Content,
                    createdAt = p.CreatedAt,
                    likesCount = p.Likes.Count,
                    commentsCount = p.Comments.Count,
                })
                .ToListAsync();

            var eventTicketRows = await _db.Events
                .AsNoTracking()
                .Where(e => e.OrganizerId == userId)
                .OrderByDescending(e => e.CreatedAt)
                .Take(8)
                .Select(e => new
                {
                    eventId = e.Id,
                    eventTitle = e.Title,
                    startTime = e.StartTime,
                    isApproved = e.IsApproved,
                    hasPendingChanges = e.ChangeRequests.Any(r => r.Status == EventChangeRequestStatus.Pending),
                    sold = e.Tickets.SelectMany(t => t.UserTickets).Count(ut => ut.Transaction.Status == paid),
                    likes = e.Likes.Count,
                    views = e.UserActivities.Count(a => a.ActivityType == UserActivityType.EventViewed),
                    vipBoostScore = e.Boosts.Sum(b => (int?)b.CreditsSpent) ?? 0,
                    revenue = e.Tickets.SelectMany(t => t.UserTickets)
                        .Where(ut => ut.Transaction.Status == paid)
                        .Sum(ut => (decimal?)ut.PricePaid) ?? 0m,
                })
                .ToListAsync();

            return Ok(new
            {
                organizationName = orgData.OrganizationName,
                approved = orgData.Approved,
                vipBoostCreditsAvailable = orgData.VipBoostCreditsAvailable,
                eventsCount,
                postsCount,
                ticketsSoldCount,
                totalRevenue,
                totalLikes,
                totalViews,
                last30Views,
                last30Sold,
                last30Revenue,
                upcomingEventsCount = upcomingCount,
                pastEventsCount = pastCount,
                recentEvents,
                recentPosts,
                eventTicketRows,
            });
        }

        // GET /api/organizer/events
        [HttpGet("events")]
        public async Task<IActionResult> Events()
        {
            if (!IsOrganizer) return Forbid();
            var userId = UserId;

            var paid = GlobalConstants.TransactionStatuses.Paid;
            var rows = await _db.Events
                .AsNoTracking()
                .Where(e => e.OrganizerId == userId)
                .OrderByDescending(e => e.CreatedAt)
                .Select(e => new
                {
                    id = e.Id,
                    title = e.Title,
                    city = e.City,
                    startTime = e.StartTime,
                    isApproved = e.IsApproved,
                    hasPendingChanges = e.ChangeRequests.Any(r => r.Status == EventChangeRequestStatus.Pending),
                    organizerPageName = e.OrganizerProfile != null ? e.OrganizerProfile.DisplayName : "Публична страница",
                    ticketsCount = e.Tickets.Count,
                    soldTicketsCount = e.Tickets.SelectMany(t => t.UserTickets).Count(ut => ut.Transaction.Status == paid),
                    vipBoostScore = e.Boosts.Sum(b => (int?)b.CreditsSpent) ?? 0,
                    likesCount = e.Likes.Count,
                    commentsCount = e.Comments.Count,
                    imageUrl = e.ImageUrl,
                })
                .ToListAsync();

            return Ok(rows);
        }

        // GET /api/organizer/profiles
        [HttpGet("profiles")]
        public async Task<IActionResult> Profiles()
        {
            if (!IsOrganizer) return Forbid();
            var profiles = await _db.OrganizerProfiles
                .AsNoTracking()
                .Where(p => p.OwnerId == UserId && p.IsActive)
                .OrderByDescending(p => p.IsDefault)
                .Select(p => new
                {
                    id = p.Id,
                    displayName = p.DisplayName,
                    tagline = p.Tagline,
                    city = p.City,
                    avatarImageUrl = p.AvatarImageUrl,
                    coverImageUrl = p.CoverImageUrl,
                    website = p.Website,
                    isDefault = p.IsDefault,
                    isApproved = p.IsApproved,
                    eventsCount = p.Events.Count,
                    postsCount = p.Posts.Count,
                })
                .ToListAsync();
            return Ok(profiles);
        }

        // GET /api/organizer/validators
        [HttpGet("validators")]
        public async Task<IActionResult> Validators()
        {
            if (!IsOrganizer) return Forbid();
            var organizerId = UserId;

            var rows = await _db.OrganizerValidatorAssignments
                .AsNoTracking()
                .Include(a => a.ValidatorUser)
                .Include(a => a.OrganizerProfile)
                .Where(a => a.OrganizerId == organizerId)
                .OrderByDescending(a => a.IsActive)
                .ThenBy(a => a.ValidatorUser.UserName)
                .Select(a => new
                {
                    id = a.Id,
                    validatorUserId = a.ValidatorUserId,
                    validatorUserName = a.ValidatorUser.UserName ?? string.Empty,
                    validatorEmail = a.ValidatorUser.Email ?? string.Empty,
                    organizerProfileId = a.OrganizerProfileId,
                    organizerProfileName = a.OrganizerProfile != null ? a.OrganizerProfile.DisplayName : null,
                    isActive = a.IsActive,
                    createdAt = a.CreatedAt,
                    updatedAt = a.UpdatedAt,
                })
                .ToListAsync();

            return Ok(rows);
        }

        // POST /api/organizer/boost/{eventId}
        [HttpPost("boost/{eventId:int}")]
        public async Task<IActionResult> BoostEvent(int eventId)
        {
            if (!IsOrganizer) return Forbid();
            var userId = UserId;

            var orgData = await _db.OrganizerData.FirstOrDefaultAsync(o => o.OrganizerId == userId);
            if (orgData == null || !orgData.Approved) return Forbid();

            var ev = await _db.Events.FirstOrDefaultAsync(e => e.Id == eventId && e.OrganizerId == userId);
            if (ev == null) return NotFound();
            if (!ev.IsApproved) return BadRequest(new { error = "VIP boost може да се активира след одобрение на събитието." });
            if (orgData.VipBoostCreditsAvailable <= 0) return BadRequest(new { error = "Първо ти трябва наличен VIP boost кредит." });

            orgData.VipBoostCreditsAvailable -= 1;
            orgData.VipBoostCreditsUsed += 1;
            _db.EventBoosts.Add(new EventBoost { EventId = eventId, OrganizerId = userId, CreditsSpent = 1 });
            await _db.SaveChangesAsync();

            return Ok(new { vipBoostCreditsAvailable = orgData.VipBoostCreditsAvailable });
        }
    }
}

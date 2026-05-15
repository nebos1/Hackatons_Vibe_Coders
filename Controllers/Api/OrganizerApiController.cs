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
            var livePastCount = await _db.Events.CountAsync(e => e.OrganizerId == userId && e.EndTime < now);
            var archivedPastCount = await _db.OrganizerProfiles
                .Where(p => p.OwnerId == userId)
                .SumAsync(p => (int?)p.PastEventsCount) ?? 0;
            var pastCount = livePastCount + archivedPastCount;
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
                    city = e.City,
                    imageUrl = e.ImageUrl,
                    startTime = e.StartTime,
                    isApproved = e.IsApproved,
                    hasPendingChanges = e.ChangeRequests.Any(r => r.Status == EventChangeRequestStatus.Pending),
                    hasActiveTickets = e.Tickets.Any(t => t.IsActive),
                    capacity = e.Tickets.Where(t => t.IsActive).Sum(t => (int?)t.QuantityTotal) ?? 0,
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
                vipBoostCreditsUsed = orgData.VipBoostCreditsUsed,
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
                    businessWorkspaceId = p.BusinessWorkspaceId,
                    workspaceName = p.BusinessWorkspace != null ? p.BusinessWorkspace.DisplayName : null,
                    isDefault = p.IsDefault,
                    isApproved = p.IsApproved,
                    eventsCount = p.Events.Count,
                    postsCount = p.Posts.Count,
                })
                .ToListAsync();
            return Ok(profiles);
        }

        [HttpGet("profiles/{id:int}")]
        public async Task<IActionResult> ProfileDetails(int id)
        {
            if (!IsOrganizer) return Forbid();
            var profile = await _db.OrganizerProfiles
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == id && p.OwnerId == UserId && p.IsActive);
            if (profile == null) return NotFound();

            return Ok(new
            {
                profile.Id,
                profile.DisplayName,
                profile.Tagline,
                profile.Description,
                profile.City,
                profile.AvatarImageUrl,
                profile.CoverImageUrl,
                profile.Website,
                profile.PhoneNumber,
                profile.ContactEmail,
                profile.InstagramUrl,
                profile.FacebookUrl,
                profile.TikTokUrl,
                profile.BrandColor,
                profile.BusinessWorkspaceId,
                profile.IsDefault,
                profile.IsApproved,
            });
        }

        [HttpPost("profiles")]
        public async Task<IActionResult> CreateProfile([FromBody] OrganizerProfileRequest request)
        {
            if (!IsOrganizer) return Forbid();
            if (string.IsNullOrWhiteSpace(request.DisplayName))
                return BadRequest(new { error = "Името на страницата е задължително." });

            var userId = UserId;
            var hasDefault = await _db.OrganizerProfiles.AnyAsync(p => p.OwnerId == userId && p.IsDefault && p.IsActive);
            var profile = new OrganizerProfile
            {
                OwnerId = userId,
                DisplayName = request.DisplayName.Trim(),
                Tagline = request.Tagline?.Trim(),
                Description = request.Description?.Trim(),
                City = request.City?.Trim(),
                AvatarImageUrl = request.AvatarImageUrl?.Trim(),
                CoverImageUrl = request.CoverImageUrl?.Trim(),
                Website = request.Website?.Trim(),
                PhoneNumber = request.PhoneNumber?.Trim(),
                ContactEmail = request.ContactEmail?.Trim(),
                InstagramUrl = request.InstagramUrl?.Trim(),
                FacebookUrl = request.FacebookUrl?.Trim(),
                TikTokUrl = request.TikTokUrl?.Trim(),
                BrandColor = request.BrandColor?.Trim(),
                BusinessWorkspaceId = request.BusinessWorkspaceId,
                IsDefault = request.IsDefault || !hasDefault,
                IsApproved = User.IsInRole(GlobalConstants.Roles.Admin),
            };

            if (profile.IsDefault)
            {
                await _db.OrganizerProfiles
                    .Where(p => p.OwnerId == userId && p.IsDefault)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(p => p.IsDefault, false));
            }

            _db.OrganizerProfiles.Add(profile);
            await _db.SaveChangesAsync();
            return Ok(new { id = profile.Id });
        }

        [HttpPut("profiles/{id:int}")]
        public async Task<IActionResult> UpdateProfile(int id, [FromBody] OrganizerProfileRequest request)
        {
            if (!IsOrganizer) return Forbid();
            if (string.IsNullOrWhiteSpace(request.DisplayName))
                return BadRequest(new { error = "Името на страницата е задължително." });

            var profile = await _db.OrganizerProfiles.FirstOrDefaultAsync(p => p.Id == id && p.OwnerId == UserId && p.IsActive);
            if (profile == null) return NotFound();

            profile.DisplayName = request.DisplayName.Trim();
            profile.Tagline = request.Tagline?.Trim();
            profile.Description = request.Description?.Trim();
            profile.City = request.City?.Trim();
            profile.AvatarImageUrl = request.AvatarImageUrl?.Trim();
            profile.CoverImageUrl = request.CoverImageUrl?.Trim();
            profile.Website = request.Website?.Trim();
            profile.PhoneNumber = request.PhoneNumber?.Trim();
            profile.ContactEmail = request.ContactEmail?.Trim();
            profile.InstagramUrl = request.InstagramUrl?.Trim();
            profile.FacebookUrl = request.FacebookUrl?.Trim();
            profile.TikTokUrl = request.TikTokUrl?.Trim();
            profile.BrandColor = request.BrandColor?.Trim();
            profile.BusinessWorkspaceId = request.BusinessWorkspaceId;
            profile.IsDefault = request.IsDefault;
            profile.IsApproved = User.IsInRole(GlobalConstants.Roles.Admin) ? profile.IsApproved : false;

            if (profile.IsDefault)
            {
                await _db.OrganizerProfiles
                    .Where(p => p.OwnerId == UserId && p.Id != id && p.IsDefault)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(p => p.IsDefault, false));
            }

            await _db.SaveChangesAsync();
            return Ok(new { id = profile.Id });
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
                    validatorPhoneNumber = a.ValidatorUser.PhoneNumber,
                    organizerProfileId = a.OrganizerProfileId,
                    organizerProfileName = a.OrganizerProfile != null ? a.OrganizerProfile.DisplayName : null,
                    isActive = a.IsActive,
                    createdAt = a.CreatedAt,
                    updatedAt = a.UpdatedAt,
                })
                .ToListAsync();

            return Ok(rows);
        }

        [NonAction]
        public async Task<IActionResult> AddValidatorLegacy([FromBody] ValidatorRequest request)
        {
            if (!IsOrganizer) return Forbid();
            if (string.IsNullOrWhiteSpace(request.Email)) return BadRequest(new { error = "Имейлът е задължителен." });

            var user = await _userManager.FindByEmailAsync(request.Email.Trim());
            if (user == null) return NotFound(new { error = "Няма потребител с този имейл." });

            if (!await _userManager.IsInRoleAsync(user, GlobalConstants.Roles.Validator))
                await _userManager.AddToRoleAsync(user, GlobalConstants.Roles.Validator);

            var existing = await _db.OrganizerValidatorAssignments
                .FirstOrDefaultAsync(a => a.OrganizerId == UserId && a.ValidatorUserId == user.Id && a.OrganizerProfileId == request.OrganizerProfileId);

            if (existing == null)
            {
                _db.OrganizerValidatorAssignments.Add(new OrganizerValidatorAssignment
                {
                    OrganizerId = UserId,
                    ValidatorUserId = user.Id,
                    OrganizerProfileId = request.OrganizerProfileId,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                });
            }
            else
            {
                existing.IsActive = true;
                existing.UpdatedAt = DateTime.UtcNow;
            }

            await _db.SaveChangesAsync();
            return Ok(new { saved = true });
        }

        [HttpPost("validators")]
        public async Task<IActionResult> AddValidator([FromBody] ValidatorRequest request)
        {
            if (!IsOrganizer) return Forbid();

            var lookup = request.UserLookup ?? request.Email;
            if (string.IsNullOrWhiteSpace(lookup)) return BadRequest(new { error = "Имейл, потребителско име или телефон са задължителни." });

            var profileId = await GetOwnedProfileIdAsync(UserId, request.OrganizerProfileId);
            if (!profileId.HasValue) return BadRequest(new { error = "Избери публична организаторска страница." });

            var user = await FindValidatorUserAsync(lookup);
            if (user == null) return NotFound(new { error = "Няма потребител с този имейл, потребителско име или телефон." });
            if (user.Id == UserId) return BadRequest(new { error = "Не можеш да добавиш себе си като валидатор." });

            var existing = await _db.OrganizerValidatorAssignments
                .FirstOrDefaultAsync(a => a.OrganizerId == UserId && a.ValidatorUserId == user.Id);

            if ((existing == null || !existing.IsActive || existing.OrganizerProfileId != profileId.Value)
                && await CountActiveValidatorsAsync(UserId, profileId.Value, existing?.Id) >= 3)
            {
                return BadRequest(new { error = "Тази публична страница вече има максималните 3 активни валидатора." });
            }

            if (!await _userManager.IsInRoleAsync(user, GlobalConstants.Roles.Validator))
                await _userManager.AddToRoleAsync(user, GlobalConstants.Roles.Validator);

            if (existing == null)
            {
                _db.OrganizerValidatorAssignments.Add(new OrganizerValidatorAssignment
                {
                    OrganizerId = UserId,
                    ValidatorUserId = user.Id,
                    OrganizerProfileId = profileId.Value,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                });
            }
            else
            {
                existing.OrganizerProfileId = profileId.Value;
                existing.IsActive = true;
                existing.UpdatedAt = DateTime.UtcNow;
            }

            await _db.SaveChangesAsync();
            return Ok(new { saved = true });
        }

        [HttpPut("validators/{id:int}")]
        public async Task<IActionResult> UpdateValidatorPage(int id, [FromBody] ValidatorRequest request)
        {
            if (!IsOrganizer) return Forbid();
            var profileId = await GetOwnedProfileIdAsync(UserId, request.OrganizerProfileId);
            if (!profileId.HasValue) return BadRequest(new { error = "Избери публична организаторска страница." });

            var assignment = await _db.OrganizerValidatorAssignments
                .FirstOrDefaultAsync(a => a.Id == id && a.OrganizerId == UserId);
            if (assignment == null) return NotFound();

            if (await CountActiveValidatorsAsync(UserId, profileId.Value, assignment.Id) >= 3)
                return BadRequest(new { error = "Тази публична страница вече има максималните 3 активни валидатора." });

            assignment.OrganizerProfileId = profileId.Value;
            assignment.IsActive = true;
            assignment.UpdatedAt = DateTime.UtcNow;

            var user = await _userManager.FindByIdAsync(assignment.ValidatorUserId);
            if (user != null && !await _userManager.IsInRoleAsync(user, GlobalConstants.Roles.Validator))
                await _userManager.AddToRoleAsync(user, GlobalConstants.Roles.Validator);

            await _db.SaveChangesAsync();
            return Ok(new { saved = true });
        }

        [HttpDelete("validators/{id:int}")]
        public async Task<IActionResult> RemoveValidator(int id)
        {
            if (!IsOrganizer) return Forbid();
            var assignment = await _db.OrganizerValidatorAssignments.FirstOrDefaultAsync(a => a.Id == id && a.OrganizerId == UserId);
            if (assignment == null) return NotFound();
            assignment.IsActive = false;
            assignment.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            var hasOtherActiveAssignments = await _db.OrganizerValidatorAssignments
                .AsNoTracking()
                .AnyAsync(a => a.ValidatorUserId == assignment.ValidatorUserId && a.IsActive);
            if (!hasOtherActiveAssignments)
            {
                var validator = await _userManager.FindByIdAsync(assignment.ValidatorUserId);
                if (validator != null && await _userManager.IsInRoleAsync(validator, GlobalConstants.Roles.Validator))
                    await _userManager.RemoveFromRoleAsync(validator, GlobalConstants.Roles.Validator);
            }

            return Ok(new { removed = true });
        }

        private async Task<ApplicationUser?> FindValidatorUserAsync(string lookup)
        {
            var value = lookup.Trim();
            var user = await _userManager.FindByEmailAsync(value);
            if (user != null) return user;

            user = await _userManager.FindByNameAsync(value);
            if (user != null) return user;

            return await _db.Users.FirstOrDefaultAsync(u => u.PhoneNumber == value);
        }

        private async Task<int?> GetOwnedProfileIdAsync(string organizerId, int? organizerProfileId)
        {
            if (!organizerProfileId.HasValue || organizerProfileId.Value <= 0) return null;

            var exists = await _db.OrganizerProfiles
                .AsNoTracking()
                .AnyAsync(p => p.Id == organizerProfileId.Value && p.OwnerId == organizerId && p.IsActive);

            return exists ? organizerProfileId.Value : null;
        }

        private Task<int> CountActiveValidatorsAsync(string organizerId, int organizerProfileId, int? excludeAssignmentId = null)
        {
            return _db.OrganizerValidatorAssignments
                .AsNoTracking()
                .Where(a => a.OrganizerId == organizerId
                    && a.OrganizerProfileId == organizerProfileId
                    && a.IsActive
                    && (!excludeAssignmentId.HasValue || a.Id != excludeAssignmentId.Value))
                .CountAsync();
        }

        [HttpGet("workspaces")]
        public async Task<IActionResult> Workspaces()
        {
            if (!IsOrganizer) return Forbid();
            var userId = UserId;

            var rows = await _db.BusinessWorkspaces
                .AsNoTracking()
                .Where(w => w.OwnerId == userId)
                .OrderByDescending(w => w.IsDefault)
                .ThenBy(w => w.DisplayName)
                .Select(w => new
                {
                    id = w.Id,
                    displayName = w.DisplayName,
                    legalName = w.LegalName,
                    city = w.City,
                    billingEmail = w.BillingEmail,
                    status = w.Status.ToString(),
                    isDefault = w.IsDefault,
                    paymentProvider = w.PaymentProvider.ToString(),
                    stripeOnboardingStatus = w.StripeOnboardingStatus.ToString(),
                    payoutsEnabled = w.PayoutsEnabled,
                    chargesEnabled = w.ChargesEnabled,
                    profilesCount = w.OrganizerProfiles.Count,
                    eventsCount = w.Events.Count,
                    transactionsCount = w.Transactions.Count,
                    createdAt = w.CreatedAt,
                })
                .ToListAsync();

            return Ok(rows);
        }

        [HttpGet("workspaces/{id:int}")]
        public async Task<IActionResult> WorkspaceDetails(int id)
        {
            if (!IsOrganizer) return Forbid();
            var workspace = await _db.BusinessWorkspaces
                .AsNoTracking()
                .FirstOrDefaultAsync(w => w.Id == id && w.OwnerId == UserId);
            if (workspace == null) return NotFound();
            return Ok(workspace);
        }

        [HttpPost("workspaces")]
        public async Task<IActionResult> CreateWorkspace([FromBody] WorkspaceRequest request)
        {
            if (!IsOrganizer) return Forbid();
            if (string.IsNullOrWhiteSpace(request.DisplayName) || string.IsNullOrWhiteSpace(request.LegalName))
                return BadRequest(new { error = "Име и юридическо име са задължителни." });

            var userId = UserId;
            var hasDefault = await _db.BusinessWorkspaces.AnyAsync(w => w.OwnerId == userId && w.IsDefault);
            var workspace = new BusinessWorkspace
            {
                OwnerId = userId,
                DisplayName = request.DisplayName.Trim(),
                LegalName = request.LegalName.Trim(),
                CompanyNumber = request.CompanyNumber?.Trim(),
                BillingEmail = request.BillingEmail?.Trim(),
                PhoneNumber = request.PhoneNumber?.Trim(),
                Address = request.Address?.Trim(),
                City = request.City?.Trim(),
                Country = request.Country?.Trim(),
                IsDefault = request.IsDefault || !hasDefault,
            };

            if (workspace.IsDefault)
            {
                await _db.BusinessWorkspaces
                    .Where(w => w.OwnerId == userId && w.IsDefault)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(w => w.IsDefault, false));
            }

            _db.BusinessWorkspaces.Add(workspace);
            await _db.SaveChangesAsync();
            return Ok(new { id = workspace.Id });
        }

        [HttpPut("workspaces/{id:int}")]
        public async Task<IActionResult> UpdateWorkspace(int id, [FromBody] WorkspaceRequest request)
        {
            if (!IsOrganizer) return Forbid();
            if (string.IsNullOrWhiteSpace(request.DisplayName) || string.IsNullOrWhiteSpace(request.LegalName))
                return BadRequest(new { error = "Име и юридическо име са задължителни." });

            var workspace = await _db.BusinessWorkspaces.FirstOrDefaultAsync(w => w.Id == id && w.OwnerId == UserId);
            if (workspace == null) return NotFound();

            workspace.DisplayName = request.DisplayName.Trim();
            workspace.LegalName = request.LegalName.Trim();
            workspace.CompanyNumber = request.CompanyNumber?.Trim();
            workspace.BillingEmail = request.BillingEmail?.Trim();
            workspace.PhoneNumber = request.PhoneNumber?.Trim();
            workspace.Address = request.Address?.Trim();
            workspace.City = request.City?.Trim();
            workspace.Country = request.Country?.Trim();
            workspace.IsDefault = request.IsDefault;
            workspace.UpdatedAt = DateTime.UtcNow;

            if (workspace.IsDefault)
            {
                await _db.BusinessWorkspaces
                    .Where(w => w.OwnerId == UserId && w.Id != id && w.IsDefault)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(w => w.IsDefault, false));
            }

            await _db.SaveChangesAsync();
            return Ok(new { id = workspace.Id });
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

public record OrganizerProfileRequest(
    string DisplayName,
    string? Tagline,
    string? Description,
    string? City,
    string? AvatarImageUrl,
    string? CoverImageUrl,
    string? Website,
    string? PhoneNumber,
    string? ContactEmail,
    string? InstagramUrl,
    string? FacebookUrl,
    string? TikTokUrl,
    string? BrandColor,
    int? BusinessWorkspaceId,
    bool IsDefault);

public record WorkspaceRequest(
    string DisplayName,
    string LegalName,
    string? CompanyNumber,
    string? BillingEmail,
    string? PhoneNumber,
    string? Address,
    string? City,
    string? Country,
    bool IsDefault);

public record ValidatorRequest(string? Email, string? UserLookup, int? OrganizerProfileId);

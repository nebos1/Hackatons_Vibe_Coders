using EventsApp.Common;
using EventsApp.Data;
using EventsApp.Models;
using EventsApp.Services;
using EventsApp.ViewModels.Events;
using EventsApp.ViewModels.Organizer;
using EventsApp.ViewModels.Posts;
using System.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EventsApp.Controllers
{
    [Authorize(Roles = GlobalConstants.Roles.Admin + "," + GlobalConstants.Roles.Organizer)]
    public class OrganizerController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IMediaUploadService _mediaUpload;
        private readonly IBusinessContextService _businessContext;

        public OrganizerController(
            ApplicationDbContext db,
            UserManager<ApplicationUser> userManager,
            IMediaUploadService mediaUpload,
            IBusinessContextService businessContext)
        {
            _db = db;
            _userManager = userManager;
            _mediaUpload = mediaUpload;
            _businessContext = businessContext;
        }

        public async Task<IActionResult> Dashboard()
        {
            var userId = _userManager.GetUserId(User)!;

            var orgData = await _db.OrganizerData
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.OrganizerId == userId);

            if (orgData == null)
            {
                return RedirectToAction(nameof(Profile));
            }

            await EnsureDefaultProfileFromOrganizerDataAsync(userId);
            var context = await _businessContext.GetContextAsync(HttpContext);

            var recentEvents = await _db.Events
                .AsNoTracking()
                .Where(e => e.OrganizerId == userId)
                .OrderByDescending(e => e.CreatedAt)
                .Take(5)
                .Select(e => new EventCardViewModel
                {
                    Id = e.Id,
                    Title = e.Title,
                    ImageUrl = e.ImageUrl,
                    Address = e.Address,
                    City = e.City,
                    StartTime = e.StartTime,
                    Genre = e.Genre,
                    IsApproved = e.IsApproved,
                    OrganizerId = e.OrganizerId,
                    OrganizerProfileId = e.OrganizerProfileId,
                    OrganizerName = e.OrganizerProfile != null ? e.OrganizerProfile.DisplayName : e.Organizer.UserName ?? string.Empty,
                    LikesCount = e.Likes.Count,
                    CommentsCount = e.Comments.Count,
                    SavesCount = e.Saves.Count,
                    GoingCount = e.Attendances.Count(a => a.Status == EventAttendanceStatus.Going),
                    InterestedCount = e.Attendances.Count(a => a.Status == EventAttendanceStatus.Interested),
                    ViewsCount = e.UserActivities.Count(a => a.ActivityType == UserActivityType.EventViewed),
                    VipBoostScore = e.Boosts.Sum(b => (int?)b.CreditsSpent) ?? 0,
                    CurrentUserLiked = e.Likes.Any(l => l.UserId == userId),
                    CurrentUserSaved = e.Saves.Any(s => s.UserId == userId),
                    CurrentUserAttendanceStatus = e.Attendances
                        .Where(a => a.UserId == userId)
                        .Select(a => (EventAttendanceStatus?)a.Status)
                        .FirstOrDefault(),
                    HasActiveTickets = e.Tickets.Any(t => t.IsActive),
                    HasPaidTickets = e.Tickets.Any(t => t.IsActive && t.Price > 0m),
                    LowestPaidTicketPrice = e.Tickets
                        .Where(t => t.IsActive && t.Price > 0m)
                        .Min(t => (decimal?)t.Price),
                })
                .ToListAsync();

            var recentPosts = await _db.Posts
                .AsNoTracking()
                .Where(p => p.OrganizerId == userId)
                .OrderByDescending(p => p.CreatedAt)
                .Take(5)
                .Select(p => new PostCardViewModel
                {
                    Id = p.Id,
                    OrganizerId = p.OrganizerId,
                    OrganizerProfileId = p.OrganizerProfileId,
                    OrganizerName = p.OrganizerProfile != null ? p.OrganizerProfile.DisplayName : p.Organizer.UserName ?? string.Empty,
                    Content = p.Content,
                    CreatedAt = p.CreatedAt,
                    EventId = p.EventId,
                    EventTitle = p.Event != null ? p.Event.Title : null,
                    FirstMediaUrl = p.Images.Select(i => i.ImageUrl).FirstOrDefault(),
                    FirstMediaType = p.Images.Select(i => i.MediaType).FirstOrDefault(),
                    LikesCount = p.Likes.Count,
                    CommentsCount = p.Comments.Count,
                    SavesCount = p.Saves.Count,
                    CurrentUserLiked = p.Likes.Any(l => l.UserId == userId),
                    CurrentUserSaved = p.Saves.Any(s => s.UserId == userId),
                    AuthorImageUrl = p.Organizer.ProfileImageUrl,
                    AuthorIsOrganizer = p.Organizer.OrganizerData != null && p.Organizer.OrganizerData.Approved,
                })
                .ToListAsync();

            var ticketTypesCount = await _db.Tickets
                .CountAsync(t => t.Event.OrganizerId == userId);

            var paid = GlobalConstants.TransactionStatuses.Paid;
            var soldQuery = _db.UserTickets
                .AsNoTracking()
                .Where(ut => ut.Ticket.Event.OrganizerId == userId
                             && ut.Transaction.Status == paid);

            var ticketsSoldCount = await soldQuery.CountAsync();
            var ticketsUsedCount = await soldQuery.CountAsync(ut => ut.IsUsed);
            var totalRevenue = await soldQuery.SumAsync(ut => (decimal?)ut.Ticket.Price) ?? 0m;
            var avgTicketPrice = ticketsSoldCount > 0 ? totalRevenue / ticketsSoldCount : 0m;

            var eventsWithTickets = await _db.Events
                .CountAsync(e => e.OrganizerId == userId && e.Tickets.Any(t => t.IsActive));

            var now = DateTime.UtcNow;
            var upcomingCount = await _db.Events
                .CountAsync(e => e.OrganizerId == userId && e.StartTime >= now);
            var pastCount = await _db.Events
                .CountAsync(e => e.OrganizerId == userId && e.EndTime < now);

            var totalLikes = await _db.EventLikes
                .CountAsync(l => l.Event.OrganizerId == userId);
            var totalComments = await _db.EventComments
                .CountAsync(c => c.Event.OrganizerId == userId);
            var totalViews = await _db.UserActivities
                .AsNoTracking()
                .CountAsync(a => a.ActivityType == UserActivityType.EventViewed && a.Event != null && a.Event.OrganizerId == userId);

            var since30 = now.AddDays(-29).Date;
            var last30Views = await _db.UserActivities
                .AsNoTracking()
                .CountAsync(a => a.ActivityType == UserActivityType.EventViewed
                    && a.CreatedAt >= since30
                    && a.Event != null
                    && a.Event.OrganizerId == userId);

            var dailyRaw = await soldQuery
                .Where(ut => ut.CreatedAt >= since30)
                .GroupBy(ut => ut.CreatedAt.Date)
                .Select(g => new
                {
                    Date = g.Key,
                    Sold = g.Count(),
                    Revenue = g.Sum(x => (decimal?)x.Ticket.Price) ?? 0m,
                })
                .ToListAsync();

            var dailyMap = dailyRaw.ToDictionary(d => d.Date, d => (d.Sold, d.Revenue));
            var salesSeries = new List<DailySalesPoint>(30);
            for (int i = 0; i < 30; i++)
            {
                var day = since30.AddDays(i);
                if (dailyMap.TryGetValue(day, out var v))
                    salesSeries.Add(new DailySalesPoint { Date = day, Sold = v.Sold, Revenue = v.Revenue });
                else
                    salesSeries.Add(new DailySalesPoint { Date = day, Sold = 0, Revenue = 0m });
            }
            var last30Sold = salesSeries.Sum(p => p.Sold);
            var last30Revenue = salesSeries.Sum(p => p.Revenue);

            var perEventStats = await _db.UserTickets
                .AsNoTracking()
                .Where(ut => ut.Ticket.Event.OrganizerId == userId
                             && ut.Transaction.Status == paid)
                .GroupBy(ut => new { ut.Ticket.EventId, ut.Ticket.Event.Title })
                .Select(g => new TopEventStat
                {
                    EventId = g.Key.EventId,
                    Title = g.Key.Title,
                    Sold = g.Count(),
                    Revenue = g.Sum(x => x.Ticket.Price),
                })
                .ToListAsync();

            var topByTicketsSold = perEventStats
                .OrderByDescending(s => s.Sold)
                .Take(5)
                .ToList();

            var topByRevenue = perEventStats
                .OrderByDescending(s => s.Revenue)
                .Take(5)
                .ToList();

            var topByViews = await _db.UserActivities
                .AsNoTracking()
                .Where(a => a.ActivityType == UserActivityType.EventViewed
                    && a.EventId != null
                    && a.Event != null
                    && a.Event.OrganizerId == userId)
                .GroupBy(a => new { a.EventId, a.Event!.Title })
                .Select(g => new TopEventStat
                {
                    EventId = g.Key.EventId ?? 0,
                    Title = g.Key.Title,
                    Views = g.Count(),
                    UniqueViewers = g.Select(x => x.UserId).Distinct().Count(),
                    EngagementScore = g.Count() + g.Select(x => x.UserId).Distinct().Count() * 2,
                })
                .OrderByDescending(s => s.Views)
                .Take(5)
                .ToListAsync();

            var genreBreakdown = await _db.Events
                .AsNoTracking()
                .Where(e => e.OrganizerId == userId)
                .GroupBy(e => e.Genre)
                .Select(g => new GenreCountStat { Genre = g.Key, Count = g.Count() })
                .OrderByDescending(g => g.Count)
                .ToListAsync();

            var cityBreakdown = await _db.Events
                .AsNoTracking()
                .Where(e => e.OrganizerId == userId)
                .GroupBy(e => e.City)
                .Select(g => new CityCountStat { City = g.Key, Count = g.Count() })
                .OrderByDescending(g => g.Count)
                .Take(8)
                .ToListAsync();

            var eventTicketRows = await _db.Events
                .AsNoTracking()
                .Where(e => e.OrganizerId == userId)
                .OrderByDescending(e => e.CreatedAt)
                .Take(8)
                .Select(e => new OrganizerEventTicketRowViewModel
                {
                    EventId = e.Id,
                    EventTitle = e.Title,
                    StartTime = e.StartTime,
                    HasActiveTickets = e.Tickets.Any(t => t.IsActive),
                    Sold = e.Tickets.SelectMany(t => t.UserTickets)
                        .Count(ut => ut.Transaction.Status == paid),
                    Likes = e.Likes.Count,
                    Comments = e.Comments.Count,
                    Views = e.UserActivities.Count(a => a.ActivityType == UserActivityType.EventViewed),
                    UniqueViewers = e.UserActivities
                        .Where(a => a.ActivityType == UserActivityType.EventViewed)
                        .Select(a => a.UserId)
                        .Distinct()
                        .Count(),
                    VipBoostScore = e.Boosts.Sum(b => (int?)b.CreditsSpent) ?? 0,
                    CanBoost = e.IsApproved,
                })
                .ToListAsync();

            var vm = new OrganizerDashboardViewModel
            {
                HasProfile = true,
                OrganizationName = orgData.OrganizationName,
                Description = orgData.Description,
                PhoneNumber = orgData.PhoneNumber,
                Website = orgData.Website,
                CompanyNumber = orgData.CompanyNumber,
                Approved = orgData.Approved,
                ActiveWorkspaceId = context.Workspace?.Id,
                ActivePageId = context.Page?.Id,
                ActiveWorkspaceName = context.Workspace?.DisplayName,
                ActivePageName = context.Page?.DisplayName,
                PaymentStatus = context.Workspace == null
                    ? "Не е избран workspace"
                    : context.Workspace.ChargesEnabled && context.Workspace.PayoutsEnabled
                        ? "Плащанията са активни"
                        : "Плащанията не са напълно активни",
                Workspaces = context.Workspaces.Select(w => new OrganizerWorkspaceRowViewModel
                {
                    Id = w.Id,
                    DisplayName = w.DisplayName,
                    LegalName = w.LegalName,
                    CompanyNumber = w.CompanyNumber,
                    IsDefault = w.IsDefault,
                    ChargesEnabled = w.ChargesEnabled,
                    PayoutsEnabled = w.PayoutsEnabled,
                    PaymentProvider = w.PaymentProvider,
                }).ToList(),
                Pages = context.Pages.Select(p => new OrganizerPageContextRowViewModel
                {
                    Id = p.Id,
                    DisplayName = p.DisplayName,
                    BusinessWorkspaceId = p.BusinessWorkspaceId,
                    IsDefaultForWorkspace = p.IsDefaultForWorkspace,
                    IsActive = p.IsActive,
                }).ToList(),
                EventsCount = await _db.Events.CountAsync(e => e.OrganizerId == userId),
                PostsCount = await _db.Posts.CountAsync(p => p.OrganizerId == userId),
                TicketTypesCount = ticketTypesCount,
                TicketsSoldCount = ticketsSoldCount,
                EventsWithTicketsCount = eventsWithTickets,
                LayoutsCount = await _db.VenueLayouts.CountAsync(l => l.OrganizerId == userId && l.Status != VenueLayoutStatus.Archived),
                UpcomingEventsCount = upcomingCount,
                PastEventsCount = pastCount,
                TicketsUsedCount = ticketsUsedCount,
                TotalLikes = totalLikes,
                TotalComments = totalComments,
                TotalViews = totalViews,
                Last30DaysViews = last30Views,
                VipBoostCreditsAvailable = orgData.VipBoostCreditsAvailable,
                VipBoostCreditsUsed = orgData.VipBoostCreditsUsed,
                ShowFirstBoostNotice = orgData.Approved
                    && orgData.FirstApprovalBoostGranted
                    && !orgData.FirstApprovalBoostNoticeSeen,
                TotalRevenue = totalRevenue,
                AverageTicketPrice = avgTicketPrice,
                Last30DaysSold = last30Sold,
                Last30DaysRevenue = last30Revenue,
                TopByTicketsSold = topByTicketsSold,
                TopByRevenue = topByRevenue,
                TopByViews = topByViews,
                GenreBreakdown = genreBreakdown,
                CityBreakdown = cityBreakdown,
                SalesLast30Days = salesSeries,
                RecentEvents = recentEvents,
                RecentPosts = recentPosts,
                EventTicketRows = eventTicketRows,
            };

            return View(vm);
        }

        public async Task<IActionResult> Events()
        {
            var userId = _userManager.GetUserId(User)!;
            var orgData = await _db.OrganizerData.AsNoTracking().FirstOrDefaultAsync(o => o.OrganizerId == userId);
            if (orgData == null)
            {
                return RedirectToAction(nameof(Profile));
            }

            var paid = GlobalConstants.TransactionStatuses.Paid;
            var rows = await _db.Events
                .AsNoTracking()
                .Where(e => e.OrganizerId == userId)
                .OrderByDescending(e => e.CreatedAt)
                .Select(e => new OrganizerEventRowViewModel
                {
                    Id = e.Id,
                    Title = e.Title,
                    City = e.City,
                    StartTime = e.StartTime,
                    IsApproved = e.IsApproved,
                    HasPendingChanges = e.ChangeRequests.Any(r => r.Status == EventChangeRequestStatus.Pending),
                    OrganizerPageName = e.OrganizerProfile != null ? e.OrganizerProfile.DisplayName : "Публична страница",
                    TicketsCount = e.Tickets.Count,
                    SoldTicketsCount = e.Tickets.SelectMany(t => t.UserTickets).Count(ut => ut.Transaction.Status == paid),
                    VipBoostScore = e.Boosts.Sum(b => (int?)b.CreditsSpent) ?? 0,
                })
                .ToListAsync();

            return View(new OrganizerEventsViewModel
            {
                Events = rows,
                VipBoostCreditsAvailable = orgData.VipBoostCreditsAvailable,
                VipBoostCreditsUsed = orgData.VipBoostCreditsUsed,
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DismissFirstBoostNotice(string? returnUrl = null)
        {
            var userId = _userManager.GetUserId(User)!;
            var orgData = await _db.OrganizerData.FirstOrDefaultAsync(o => o.OrganizerId == userId);
            if (orgData == null)
            {
                return RedirectToAction(nameof(Profile));
            }

            orgData.FirstApprovalBoostNoticeSeen = true;
            await _db.SaveChangesAsync();

            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return LocalRedirect(returnUrl);
            }

            return RedirectToAction(nameof(Dashboard));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BuyVipBoostCredits(int quantity = 1, string? returnUrl = null)
        {
            var userId = _userManager.GetUserId(User)!;
            quantity = Math.Clamp(quantity, 1, 20);

            var orgData = await _db.OrganizerData.FirstOrDefaultAsync(o => o.OrganizerId == userId);
            if (orgData == null || !orgData.Approved)
            {
                return Forbid();
            }

            orgData.VipBoostCreditsAvailable += quantity;
            await _db.SaveChangesAsync();

            TempData["StatusMessage"] = $"Добавени са {quantity} VIP boost кредит(а). Това е локална тестова покупка до Stripe интеграцията.";
            return SafeLocalRedirect(returnUrl) ?? RedirectToAction(nameof(Dashboard));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BoostEvent(int eventId, string? returnUrl = null)
        {
            var userId = _userManager.GetUserId(User)!;
            await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable);

            var orgData = await _db.OrganizerData.FirstOrDefaultAsync(o => o.OrganizerId == userId);
            if (orgData == null || !orgData.Approved)
            {
                return Forbid();
            }

            var ev = await _db.Events.FirstOrDefaultAsync(e => e.Id == eventId && e.OrganizerId == userId);
            if (ev == null)
            {
                return NotFound();
            }

            if (!ev.IsApproved)
            {
                TempData["StatusMessage"] = "VIP boost може да се активира след одобрение на събитието.";
                return SafeLocalRedirect(returnUrl) ?? RedirectToAction(nameof(Events));
            }

            if (orgData.VipBoostCreditsAvailable <= 0)
            {
                TempData["StatusMessage"] = "Първо ти трябва наличен VIP boost кредит.";
                return SafeLocalRedirect(returnUrl) ?? RedirectToAction(nameof(Events));
            }

            orgData.VipBoostCreditsAvailable -= 1;
            orgData.VipBoostCreditsUsed += 1;
            _db.EventBoosts.Add(new EventBoost
            {
                EventId = eventId,
                OrganizerId = userId,
                CreditsSpent = 1,
            });

            await _db.SaveChangesAsync();
            await tx.CommitAsync();

            TempData["BoostedEventId"] = eventId;
            TempData["StatusMessage"] = "VIP boost е активиран. Събитието вече получава по-силен ranking в Home и препоръките.";
            return SafeLocalRedirect(returnUrl) ?? RedirectToAction(nameof(Events));
        }

        private IActionResult? SafeLocalRedirect(string? returnUrl)
        {
            return !string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl)
                ? Redirect(returnUrl)
                : null;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SwitchContext(int workspaceId, int? pageId)
        {
            await _businessContext.SetActiveContextAsync(HttpContext, workspaceId, pageId);
            TempData["StatusMessage"] = "Активният workspace/page е обновен.";
            return RedirectToAction(nameof(Dashboard));
        }

        [HttpGet]
        public async Task<IActionResult> Validators()
        {
            var userId = _userManager.GetUserId(User)!;
            if (!await _db.OrganizerData.AnyAsync(o => o.OrganizerId == userId))
            {
                return RedirectToAction(nameof(Profile));
            }

            return View(await BuildValidatorsModelAsync(userId));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddValidator(OrganizerValidatorCreateViewModel input)
        {
            var organizerId = _userManager.GetUserId(User)!;
            if (!await _db.OrganizerData.AnyAsync(o => o.OrganizerId == organizerId))
            {
                return RedirectToAction(nameof(Profile));
            }

            var organizerProfileId = await GetOwnedProfileIdAsync(organizerId, input.OrganizerProfileId);
            if (!organizerProfileId.HasValue)
            {
                TempData["StatusMessage"] = "Избери публична страница за този валидатор.";
                return RedirectToAction(nameof(Validators));
            }

            var lookup = input.UserLookup?.Trim();
            if (string.IsNullOrWhiteSpace(lookup))
            {
                TempData["StatusMessage"] = "Въведи имейл, потребителско име или телефон.";
                return RedirectToAction(nameof(Validators));
            }

            var validator = await FindValidatorUserAsync(lookup);
            if (validator == null)
            {
                TempData["StatusMessage"] = "Не е намерен такъв потребител. Първо създай служебен акаунт на телефона.";
                return RedirectToAction(nameof(Validators));
            }

            if (validator.Id == organizerId)
            {
                TempData["StatusMessage"] = "Използвай отделен служебен акаунт за валидиране.";
                return RedirectToAction(nameof(Validators));
            }

            var assignment = await _db.OrganizerValidatorAssignments
                .FirstOrDefaultAsync(a => a.OrganizerId == organizerId && a.ValidatorUserId == validator.Id);

            var isNewOrInactive = assignment == null || !assignment.IsActive;
            if (isNewOrInactive && await CountActiveValidatorsAsync(organizerId, organizerProfileId.Value) >= 3)
            {
                TempData["StatusMessage"] = "Можеш да имаш до 3 активни валидатора.";
                return RedirectToAction(nameof(Validators));
            }

            if (assignment == null)
            {
                assignment = new OrganizerValidatorAssignment
                {
                    OrganizerId = organizerId,
                    ValidatorUserId = validator.Id,
                };
                _db.OrganizerValidatorAssignments.Add(assignment);
            }

            if (assignment.IsActive &&
                assignment.OrganizerProfileId != organizerProfileId.Value &&
                await CountActiveValidatorsAsync(organizerId, organizerProfileId.Value) >= 3)
            {
                TempData["StatusMessage"] = "Тази публична страница вече има 3 активни валидатора.";
                return RedirectToAction(nameof(Validators));
            }

            assignment.IsActive = true;
            assignment.OrganizerProfileId = organizerProfileId.Value;
            assignment.UpdatedAt = DateTime.UtcNow;

            if (!await _userManager.IsInRoleAsync(validator, GlobalConstants.Roles.Validator))
            {
                await _userManager.AddToRoleAsync(validator, GlobalConstants.Roles.Validator);
            }

            await _db.SaveChangesAsync();
            TempData["StatusMessage"] = "Достъпът за валидатора е запазен.";
            return RedirectToAction(nameof(Validators));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateValidatorPage(int id, int organizerProfileId)
        {
            var organizerId = _userManager.GetUserId(User)!;
            var assignment = await _db.OrganizerValidatorAssignments
                .FirstOrDefaultAsync(a => a.Id == id && a.OrganizerId == organizerId);

            if (assignment == null)
            {
                return NotFound();
            }

            var ownedProfileId = await GetOwnedProfileIdAsync(organizerId, organizerProfileId);
            if (!ownedProfileId.HasValue)
            {
                TempData["StatusMessage"] = "Избери публична страница. Ако акаунтът вече не трябва да валидира, премахни достъпа.";
                return RedirectToAction(nameof(Validators));
            }

            if (assignment.IsActive &&
                assignment.OrganizerProfileId != ownedProfileId.Value &&
                await CountActiveValidatorsAsync(organizerId, ownedProfileId.Value) >= 3)
            {
                TempData["StatusMessage"] = "Тази публична страница вече има 3 активни валидатора.";
                return RedirectToAction(nameof(Validators));
            }

            assignment.OrganizerProfileId = ownedProfileId.Value;
            assignment.IsActive = true;
            assignment.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            TempData["StatusMessage"] = "Достъпът към публичната страница е обновен.";
            return RedirectToAction(nameof(Validators));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveValidator(int id)
        {
            var organizerId = _userManager.GetUserId(User)!;
            var assignment = await _db.OrganizerValidatorAssignments
                .FirstOrDefaultAsync(a => a.Id == id && a.OrganizerId == organizerId);

            if (assignment == null)
            {
                return NotFound();
            }

            assignment.IsActive = false;
            assignment.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            var validator = await _userManager.FindByIdAsync(assignment.ValidatorUserId);
            if (validator != null &&
                await _userManager.IsInRoleAsync(validator, GlobalConstants.Roles.Validator) &&
                !await _db.OrganizerValidatorAssignments.AnyAsync(a =>
                    a.ValidatorUserId == validator.Id &&
                    a.IsActive))
            {
                await _userManager.RemoveFromRoleAsync(validator, GlobalConstants.Roles.Validator);
            }

            TempData["StatusMessage"] = "Достъпът за валидатора е премахнат.";
            return RedirectToAction(nameof(Validators));
        }

        public async Task<IActionResult> Workspaces()
        {
            var userId = _userManager.GetUserId(User)!;
            await _businessContext.EnsureDefaultWorkspaceAsync(userId);

            var workspaces = await _db.BusinessWorkspaces
                .AsNoTracking()
                .Where(w => w.OwnerId == userId && w.Status != BusinessWorkspaceStatus.Archived)
                .OrderByDescending(w => w.IsDefault)
                .ThenBy(w => w.DisplayName)
                .ToListAsync();

            return View(workspaces);
        }

        [HttpGet]
        public async Task<IActionResult> Workspace(int? id)
        {
            var userId = _userManager.GetUserId(User)!;
            if (!id.HasValue)
            {
                return View(new BusinessWorkspace
                {
                    OwnerId = userId,
                    Country = "BG",
                    BillingEmail = User.Identity?.Name,
                    Status = BusinessWorkspaceStatus.Active,
                    PaymentProvider = PaymentProvider.Stripe,
                    StripeOnboardingStatus = StripeOnboardingStatus.NotStarted,
                });
            }

            var workspace = await _db.BusinessWorkspaces.FirstOrDefaultAsync(w =>
                w.Id == id.Value &&
                w.OwnerId == userId &&
                w.Status != BusinessWorkspaceStatus.Archived);
            return workspace == null ? NotFound() : View(workspace);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Workspace(int? id, BusinessWorkspace input)
        {
            var userId = _userManager.GetUserId(User)!;
            if (string.IsNullOrWhiteSpace(input.DisplayName) && string.IsNullOrWhiteSpace(input.LegalName))
            {
                ModelState.AddModelError(nameof(input.DisplayName), "Enter a workspace display name or legal name.");
            }

            if (string.IsNullOrWhiteSpace(input.DisplayName) && !string.IsNullOrWhiteSpace(input.LegalName))
            {
                ModelState.Remove(nameof(input.DisplayName));
            }

            if (string.IsNullOrWhiteSpace(input.LegalName) && !string.IsNullOrWhiteSpace(input.DisplayName))
            {
                ModelState.Remove(nameof(input.LegalName));
            }

            if (!ModelState.IsValid)
            {
                return View(input);
            }

            var workspace = id.HasValue
                ? await _db.BusinessWorkspaces.FirstOrDefaultAsync(w =>
                    w.Id == id.Value &&
                    w.OwnerId == userId &&
                    w.Status != BusinessWorkspaceStatus.Archived)
                : null;

            if (id.HasValue && workspace == null)
            {
                return NotFound();
            }

            if (workspace == null)
            {
                workspace = new BusinessWorkspace
                {
                    OwnerId = userId,
                    CreatedAt = DateTime.UtcNow,
                };
                _db.BusinessWorkspaces.Add(workspace);
            }

            workspace.DisplayName = string.IsNullOrWhiteSpace(input.DisplayName) ? input.LegalName : input.DisplayName.Trim();
            workspace.LegalName = string.IsNullOrWhiteSpace(input.LegalName) ? workspace.DisplayName : input.LegalName.Trim();
            workspace.CompanyNumber = string.IsNullOrWhiteSpace(input.CompanyNumber) ? null : input.CompanyNumber.Trim();
            workspace.BillingEmail = string.IsNullOrWhiteSpace(input.BillingEmail) ? null : input.BillingEmail.Trim();
            workspace.PhoneNumber = string.IsNullOrWhiteSpace(input.PhoneNumber) ? null : input.PhoneNumber.Trim();
            workspace.Address = string.IsNullOrWhiteSpace(input.Address) ? null : input.Address.Trim();
            workspace.City = string.IsNullOrWhiteSpace(input.City) ? null : input.City.Trim();
            workspace.Country = string.IsNullOrWhiteSpace(input.Country) ? "BG" : input.Country.Trim();
            workspace.Status = input.Status;
            workspace.PaymentProvider = input.PaymentProvider;
            workspace.StripeConnectedAccountId = string.IsNullOrWhiteSpace(input.StripeConnectedAccountId) ? null : input.StripeConnectedAccountId.Trim();
            workspace.StripeOnboardingStatus = input.StripeOnboardingStatus;
            workspace.ChargesEnabled = input.ChargesEnabled;
            workspace.PayoutsEnabled = input.PayoutsEnabled;
            workspace.UpdatedAt = DateTime.UtcNow;

            if (input.IsDefault || !await _db.BusinessWorkspaces.AnyAsync(w =>
                w.OwnerId == userId &&
                w.Id != workspace.Id &&
                w.IsDefault &&
                w.Status != BusinessWorkspaceStatus.Archived))
            {
                await _db.BusinessWorkspaces
                    .Where(w => w.OwnerId == userId && w.Id != workspace.Id && w.Status != BusinessWorkspaceStatus.Archived)
                    .ExecuteUpdateAsync(s => s.SetProperty(w => w.IsDefault, false));
                workspace.IsDefault = true;
            }

            await _db.SaveChangesAsync();
            TempData["StatusMessage"] = "Workspace saved.";
            return RedirectToAction(nameof(Workspaces));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteWorkspace(int id)
        {
            var userId = _userManager.GetUserId(User)!;
            var workspace = await _db.BusinessWorkspaces
                .Include(w => w.OrganizerProfiles)
                .FirstOrDefaultAsync(w =>
                    w.Id == id &&
                    w.OwnerId == userId &&
                    w.Status != BusinessWorkspaceStatus.Archived);

            if (workspace == null)
            {
                return NotFound();
            }

            var replacement = await _db.BusinessWorkspaces
                .Where(w =>
                    w.OwnerId == userId &&
                    w.Id != workspace.Id &&
                    w.Status != BusinessWorkspaceStatus.Archived)
                .OrderByDescending(w => w.IsDefault)
                .ThenBy(w => w.DisplayName)
                .FirstOrDefaultAsync();

            if (replacement == null)
            {
                TempData["StatusMessage"] = "Create another workspace before deleting this one.";
                return RedirectToAction(nameof(Workspaces));
            }

            workspace.Status = BusinessWorkspaceStatus.Archived;
            workspace.IsDefault = false;
            workspace.UpdatedAt = DateTime.UtcNow;

            foreach (var page in workspace.OrganizerProfiles)
            {
                page.IsActive = false;
                page.IsDefault = false;
                page.IsDefaultForWorkspace = false;
                page.Status = BusinessWorkspaceStatus.Archived;
            }

            if (!await _db.BusinessWorkspaces.AnyAsync(w =>
                w.OwnerId == userId &&
                w.Id != workspace.Id &&
                w.Status != BusinessWorkspaceStatus.Archived &&
                w.IsDefault))
            {
                replacement.IsDefault = true;
                replacement.UpdatedAt = DateTime.UtcNow;
            }

            await _db.SaveChangesAsync();

            var replacementPageId = await _db.OrganizerProfiles
                .Where(p =>
                    p.OwnerId == userId &&
                    p.BusinessWorkspaceId == replacement.Id &&
                    p.IsActive &&
                    p.Status != BusinessWorkspaceStatus.Archived)
                .OrderByDescending(p => p.IsDefaultForWorkspace)
                .ThenByDescending(p => p.IsDefault)
                .Select(p => (int?)p.Id)
                .FirstOrDefaultAsync();

            await _businessContext.SetActiveContextAsync(HttpContext, replacement.Id, replacementPageId);
            TempData["StatusMessage"] = "Workspace deleted safely. Existing reports and ticket/payment history are preserved.";
            return RedirectToAction(nameof(Workspaces));
        }

        public async Task<IActionResult> Profiles()
        {
            var userId = _userManager.GetUserId(User)!;
            await EnsureDefaultProfileFromOrganizerDataAsync(userId);
            await _businessContext.EnsureDefaultWorkspaceAsync(userId);

            var profiles = await _db.OrganizerProfiles
                .AsNoTracking()
                .Where(p => p.OwnerId == userId)
                .OrderByDescending(p => p.IsDefault)
                .ThenBy(p => p.DisplayName)
                .Select(p => new OrganizerProfileRowViewModel
                {
                    Id = p.Id,
                    DisplayName = p.DisplayName,
                    Tagline = p.Tagline,
                    City = p.City,
                    AvatarImageUrl = p.AvatarImageUrl,
                    CoverImageUrl = p.CoverImageUrl,
                    IsDefault = p.IsDefault,
                    IsActive = p.IsActive,
                    EventsCount = p.Events.Count,
                    CreatedAt = p.CreatedAt,
                })
                .ToListAsync();

            return View(new OrganizerProfileListViewModel { Profiles = profiles });
        }

        [HttpGet]
        public async Task<IActionResult> Profile(int? id)
        {
            var userId = _userManager.GetUserId(User)!;
            await EnsureDefaultProfileFromOrganizerDataAsync(userId);

            if (id.HasValue)
            {
                var profile = await _db.OrganizerProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id && p.OwnerId == userId);
                if (profile == null) return NotFound();
                var vm = ToProfileInput(profile, await GetApprovedStatusAsync(userId));
                vm.Workspaces = await GetWorkspaceOptionsAsync(userId);
                return View(vm);
            }

            var hasProfiles = await _db.OrganizerProfiles.AnyAsync(p => p.OwnerId == userId);
            var orgData = await _db.OrganizerData.AsNoTracking().FirstOrDefaultAsync(o => o.OrganizerId == userId);

            return View(new OrganizerProfileViewModel
            {
                OrganizationName = string.Empty,
                CompanyNumber = orgData?.CompanyNumber,
                Approved = orgData?.Approved ?? false,
                IsDefault = !hasProfiles,
                IsActive = true,
                BusinessWorkspaceId = (await _businessContext.EnsureDefaultWorkspaceAsync(userId)).Id,
                Workspaces = await GetWorkspaceOptionsAsync(userId),
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Profile(OrganizerProfileViewModel input)
        {
            var userId = _userManager.GetUserId(User)!;
            var approved = await GetApprovedStatusAsync(userId);
            input.Approved = approved;
            input.Workspaces = await GetWorkspaceOptionsAsync(userId);

            if (!ModelState.IsValid) return View(input);
            var workspace = input.BusinessWorkspaceId.HasValue
                ? await _db.BusinessWorkspaces.FirstOrDefaultAsync(w => w.Id == input.BusinessWorkspaceId && w.OwnerId == userId)
                : await _businessContext.EnsureDefaultWorkspaceAsync(userId);
            if (workspace == null)
            {
                ModelState.AddModelError(nameof(input.BusinessWorkspaceId), "Избери валиден workspace.");
                input.Workspaces = await GetWorkspaceOptionsAsync(userId);
                return View(input);
            }

            var profile = input.Id.HasValue
                ? await _db.OrganizerProfiles.FirstOrDefaultAsync(p => p.Id == input.Id && p.OwnerId == userId)
                : null;

            if (input.Id.HasValue && profile == null) return NotFound();

            var isNew = profile == null;
            profile ??= new OrganizerProfile { OwnerId = userId };

            profile.DisplayName = input.OrganizationName.Trim();
            profile.Tagline = string.IsNullOrWhiteSpace(input.Tagline) ? null : input.Tagline.Trim();
            profile.Description = string.IsNullOrWhiteSpace(input.Description) ? null : input.Description.Trim();
            profile.City = string.IsNullOrWhiteSpace(input.City) ? null : input.City.Trim();
            profile.PhoneNumber = string.IsNullOrWhiteSpace(input.PhoneNumber) ? null : input.PhoneNumber.Trim();
            profile.Website = string.IsNullOrWhiteSpace(input.Website) ? null : input.Website.Trim();
            profile.ContactEmail = string.IsNullOrWhiteSpace(input.ContactEmail) ? null : input.ContactEmail.Trim();
            profile.InstagramUrl = string.IsNullOrWhiteSpace(input.InstagramUrl) ? null : input.InstagramUrl.Trim();
            profile.FacebookUrl = string.IsNullOrWhiteSpace(input.FacebookUrl) ? null : input.FacebookUrl.Trim();
            profile.TikTokUrl = string.IsNullOrWhiteSpace(input.TikTokUrl) ? null : input.TikTokUrl.Trim();
            profile.BrandColor = string.IsNullOrWhiteSpace(input.BrandColor) ? null : input.BrandColor.Trim();
            profile.IsActive = input.IsActive;
            profile.BusinessWorkspaceId = workspace.Id;
            profile.ShowOwnerProfilePublicly = input.ShowOwnerProfilePublicly;
            profile.ShowLegalBusinessNamePublicly = input.ShowLegalBusinessNamePublicly;

            try
            {
                var avatar = await SaveOptionalImageAsync(input.AvatarFile, "organizers");
                if (avatar != null) profile.AvatarImageUrl = avatar.Url;

                var cover = await SaveOptionalImageAsync(input.CoverFile, "organizers");
                if (cover != null) profile.CoverImageUrl = cover.Url;
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                input.CurrentAvatarUrl = profile.AvatarImageUrl;
                input.CurrentCoverUrl = profile.CoverImageUrl;
                return View(input);
            }

            if (isNew)
            {
                _db.OrganizerProfiles.Add(profile);
            }

            var shouldBeDefault = input.IsDefault || !await _db.OrganizerProfiles.AnyAsync(p => p.OwnerId == userId && p.Id != profile.Id);
            if (shouldBeDefault)
            {
                await ClearDefaultProfileAsync(userId);
                profile.IsDefault = true;
            }
            if (!await _db.OrganizerProfiles.AnyAsync(p => p.OwnerId == userId && p.BusinessWorkspaceId == workspace.Id && p.Id != profile.Id && p.IsDefaultForWorkspace))
            {
                profile.IsDefaultForWorkspace = true;
            }

            await EnsureOrganizerDataAsync(userId, input, approved);
            await _db.SaveChangesAsync();

            TempData["StatusMessage"] = isNew ? "Публичната организаторска страница е създадена." : "Публичната организаторска страница е обновена.";
            return RedirectToAction(nameof(Profiles));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetDefaultProfile(int id)
        {
            var userId = _userManager.GetUserId(User)!;
            var profile = await _db.OrganizerProfiles.FirstOrDefaultAsync(p => p.Id == id && p.OwnerId == userId);
            if (profile == null) return NotFound();

            await ClearDefaultProfileAsync(userId);
            profile.IsDefault = true;
            profile.IsActive = true;
            await _db.SaveChangesAsync();
            TempData["StatusMessage"] = $"{profile.DisplayName} вече е основната ти организаторска страница.";
            return RedirectToAction(nameof(Profiles));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteProfile(int id)
        {
            var userId = _userManager.GetUserId(User)!;
            var profile = await _db.OrganizerProfiles
                .Include(p => p.Events)
                .FirstOrDefaultAsync(p => p.Id == id && p.OwnerId == userId);
            if (profile == null) return NotFound();

            if (profile.Events.Any())
            {
                profile.IsActive = false;
                TempData["StatusMessage"] = "Тази страница има събития, затова беше архивирана вместо изтрита.";
            }
            else
            {
                _db.OrganizerProfiles.Remove(profile);
                TempData["StatusMessage"] = "Публичната организаторска страница е изтрита.";
            }

            await _db.SaveChangesAsync();

            if (!await _db.OrganizerProfiles.AnyAsync(p => p.OwnerId == userId && p.IsDefault))
            {
                var fallback = await _db.OrganizerProfiles
                    .Where(p => p.OwnerId == userId && p.IsActive)
                    .OrderBy(p => p.DisplayName)
                    .FirstOrDefaultAsync();
                if (fallback != null)
                {
                    fallback.IsDefault = true;
                    await _db.SaveChangesAsync();
                }
            }

            return RedirectToAction(nameof(Profiles));
        }

        private async Task<OrganizerValidatorsViewModel> BuildValidatorsModelAsync(string organizerId)
        {
            var pages = await _db.OrganizerProfiles
                .AsNoTracking()
                .Where(p => p.OwnerId == organizerId && p.IsActive)
                .OrderByDescending(p => p.IsDefault)
                .ThenBy(p => p.DisplayName)
                .Select(p => new OrganizerValidatorPageOptionViewModel
                {
                    Id = p.Id,
                    DisplayName = p.DisplayName,
                    IsDefault = p.IsDefault,
                })
                .ToListAsync();

            var assignments = await _db.OrganizerValidatorAssignments
                .AsNoTracking()
                .Where(a => a.OrganizerId == organizerId)
                .Include(a => a.ValidatorUser)
                .Include(a => a.OrganizerProfile)
                .OrderByDescending(a => a.IsActive)
                .ThenBy(a => a.ValidatorUser.UserName)
                .ToListAsync();

            return new OrganizerValidatorsViewModel
            {
                ActiveValidatorsCount = assignments.Count(a => a.IsActive),
                Pages = pages,
                Validators = assignments.Select(a =>
                {
                    var fullName = string.Join(" ", new[] { a.ValidatorUser.FirstName, a.ValidatorUser.LastName }
                        .Where(s => !string.IsNullOrWhiteSpace(s)));

                    return new OrganizerValidatorRowViewModel
                    {
                        Id = a.Id,
                        ValidatorUserId = a.ValidatorUserId,
                        DisplayName = !string.IsNullOrWhiteSpace(fullName)
                            ? fullName
                            : a.ValidatorUser.UserName ?? a.ValidatorUser.Email ?? "Validator",
                        Email = a.ValidatorUser.Email,
                        PhoneNumber = a.ValidatorUser.PhoneNumber,
                        IsActive = a.IsActive,
                        CreatedAt = a.CreatedAt,
                        OrganizerProfileId = a.OrganizerProfileId,
                        OrganizerProfileName = a.OrganizerProfile?.DisplayName,
                    };
                }).ToList(),
            };
        }

        private async Task<ApplicationUser?> FindValidatorUserAsync(string lookup)
        {
            var user = await _userManager.FindByEmailAsync(lookup);
            if (user != null) return user;

            user = await _userManager.FindByNameAsync(lookup);
            if (user != null) return user;

            return await _db.Users.FirstOrDefaultAsync(u => u.PhoneNumber == lookup);
        }

        private async Task<int?> GetOwnedProfileIdAsync(string organizerId, int organizerProfileId)
        {
            if (organizerProfileId <= 0) return null;

            return await _db.OrganizerProfiles
                .Where(p => p.OwnerId == organizerId && p.IsActive && p.Id == organizerProfileId)
                .Select(p => (int?)p.Id)
                .FirstOrDefaultAsync();
        }

        private async Task<int> CountActiveValidatorsAsync(string organizerId, int organizerProfileId)
        {
            return await _db.OrganizerValidatorAssignments
                .CountAsync(a =>
                    a.OrganizerId == organizerId &&
                    a.OrganizerProfileId == organizerProfileId &&
                    a.IsActive);
        }

        private static OrganizerProfileViewModel ToProfileInput(OrganizerProfile profile, bool approved)
        {
            return new OrganizerProfileViewModel
            {
                Id = profile.Id,
                OrganizationName = profile.DisplayName,
                Tagline = profile.Tagline,
                Description = profile.Description,
                City = profile.City,
                CurrentAvatarUrl = profile.AvatarImageUrl,
                CurrentCoverUrl = profile.CoverImageUrl,
                PhoneNumber = profile.PhoneNumber,
                Website = profile.Website,
                ContactEmail = profile.ContactEmail,
                InstagramUrl = profile.InstagramUrl,
                FacebookUrl = profile.FacebookUrl,
                TikTokUrl = profile.TikTokUrl,
                BrandColor = profile.BrandColor,
                IsDefault = profile.IsDefault,
                IsActive = profile.IsActive,
                Approved = approved,
                BusinessWorkspaceId = profile.BusinessWorkspaceId,
                ShowOwnerProfilePublicly = profile.ShowOwnerProfilePublicly,
                ShowLegalBusinessNamePublicly = profile.ShowLegalBusinessNamePublicly,
            };
        }

        private async Task<MediaUploadResult?> SaveOptionalImageAsync(IFormFile? file, string folder)
        {
            if (file == null || file.Length == 0) return null;
            var media = await _mediaUpload.SaveAsync(file, folder);
            if (media?.MediaType != PostMediaType.Image)
            {
                throw new InvalidOperationException("Only image files are allowed here.");
            }
            return media;
        }

        private async Task EnsureDefaultProfileFromOrganizerDataAsync(string userId)
        {
            if (await _db.OrganizerProfiles.AnyAsync(p => p.OwnerId == userId)) return;

            var orgData = await _db.OrganizerData.AsNoTracking().FirstOrDefaultAsync(o => o.OrganizerId == userId);
            if (orgData == null) return;

            _db.OrganizerProfiles.Add(new OrganizerProfile
            {
                OwnerId = userId,
                BusinessWorkspaceId = (await _businessContext.EnsureDefaultWorkspaceAsync(userId)).Id,
                DisplayName = orgData.OrganizationName,
                Description = orgData.Description,
                PhoneNumber = orgData.PhoneNumber,
                Website = orgData.Website,
                IsDefault = true,
                IsDefaultForWorkspace = true,
                IsActive = true,
            });
            await _db.SaveChangesAsync();
        }

        private async Task<IEnumerable<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem>> GetWorkspaceOptionsAsync(string userId)
        {
            await _businessContext.EnsureDefaultWorkspaceAsync(userId);
            return await _db.BusinessWorkspaces
                .AsNoTracking()
                .Where(w => w.OwnerId == userId && w.Status != BusinessWorkspaceStatus.Archived)
                .OrderByDescending(w => w.IsDefault)
                .ThenBy(w => w.DisplayName)
                .Select(w => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem
                {
                    Value = w.Id.ToString(),
                    Text = w.IsDefault ? w.DisplayName + " (default)" : w.DisplayName,
                })
                .ToListAsync();
        }

        private async Task EnsureOrganizerDataAsync(string userId, OrganizerProfileViewModel input, bool approved)
        {
            var orgData = await _db.OrganizerData.FirstOrDefaultAsync(o => o.OrganizerId == userId);
            if (orgData == null)
            {
                _db.OrganizerData.Add(new OrganizerData
                {
                    OrganizerId = userId,
                    OrganizationName = input.OrganizationName.Trim(),
                    Description = input.Description,
                    PhoneNumber = input.PhoneNumber,
                    Website = input.Website,
                    CompanyNumber = input.CompanyNumber,
                    Approved = approved,
                });
            }
            else if (input.IsDefault)
            {
                orgData.OrganizationName = input.OrganizationName.Trim();
                orgData.Description = input.Description;
                orgData.PhoneNumber = input.PhoneNumber;
                orgData.Website = input.Website;
                orgData.CompanyNumber = input.CompanyNumber;
            }
        }

        private async Task ClearDefaultProfileAsync(string userId)
        {
            var defaults = await _db.OrganizerProfiles.Where(p => p.OwnerId == userId && p.IsDefault).ToListAsync();
            foreach (var item in defaults)
            {
                item.IsDefault = false;
            }
        }

        private async Task<bool> GetApprovedStatusAsync(string userId)
        {
            return await _db.OrganizerData
                .AsNoTracking()
                .Where(o => o.OrganizerId == userId)
                .Select(o => o.Approved)
                .FirstOrDefaultAsync();
        }
    }
}

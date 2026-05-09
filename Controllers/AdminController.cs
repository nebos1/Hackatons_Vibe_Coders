using EventsApp.Common;
using EventsApp.Data;
using EventsApp.Models;
using EventsApp.Services;
using EventsApp.ViewModels.Admin;
using EventsApp.ViewModels.Events;
using EventsApp.ViewModels.Posts;
using EventsApp.ViewModels.Tickets;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EventsApp.Controllers
{
    [Authorize(Roles = GlobalConstants.Roles.Admin)]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IRecurringEventService _recurringEvents;
        private readonly ILayoutService _layouts;
        private readonly IEventDeletionService _eventDeletion;

        public AdminController(
            ApplicationDbContext db,
            UserManager<ApplicationUser> userManager,
            IRecurringEventService recurringEvents,
            ILayoutService layouts,
            IEventDeletionService eventDeletion)
        {
            _db = db;
            _userManager = userManager;
            _recurringEvents = recurringEvents;
            _layouts = layouts;
            _eventDeletion = eventDeletion;
        }

        public async Task<IActionResult> Index()
        {
            var recentPosts = await _db.Posts
                .AsNoTracking()
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
                    AuthorImageUrl = p.Organizer.ProfileImageUrl,
                    AuthorIsOrganizer = p.Organizer.OrganizerData != null && p.Organizer.OrganizerData.Approved,
                })
                .ToListAsync();

            var vm = new AdminDashboardViewModel
            {
                UsersCount = await _db.Users.CountAsync(),
                OrganizersCount = await _db.OrganizerData.CountAsync(),
                EventsCount = await _db.Events.CountAsync(),
                PendingOrganizersCount = await _db.OrganizerData.CountAsync(o => !o.Approved),
                PendingEventsCount = await _db.Events.CountAsync(e => !e.IsApproved || e.ChangeRequests.Any(r => r.Status == EventChangeRequestStatus.Pending)),
                RecentPosts = recentPosts,
            };

            return View(vm);
        }

        public async Task<IActionResult> Organizers()
        {
            var organizers = await _db.OrganizerData
                .AsNoTracking()
                .Include(o => o.Organizer)
                .OrderBy(o => o.Approved)
                .ThenBy(o => o.CreatedAt)
                .Select(o => new AdminOrganizerRowViewModel
                {
                    OrganizerId = o.OrganizerId,
                    UserName = o.Organizer.UserName ?? string.Empty,
                    Email = o.Organizer.Email ?? string.Empty,
                    OrganizationName = o.OrganizationName,
                    PhoneNumber = o.PhoneNumber,
                    City = o.City,
                    Country = o.Country,
                    ReferralSource = o.ReferralSource,
                    Website = o.Website,
                    CompanyNumber = o.CompanyNumber,
                    Approved = o.Approved,
                    CreatedAt = o.CreatedAt,
                })
                .ToListAsync();

            return View(organizers);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveOrganizer(string id)
        {
            var org = await _db.OrganizerData.FirstOrDefaultAsync(o => o.OrganizerId == id);
            if (org == null) return NotFound();

            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            var wasApproved = org.Approved;
            org.Approved = !org.Approved;
            if (!wasApproved && org.Approved && !org.FirstApprovalBoostGranted)
            {
                org.VipBoostCreditsAvailable = Math.Max(1, org.VipBoostCreditsAvailable);
                org.FirstApprovalBoostGranted = true;
                org.FirstApprovalBoostNoticeSeen = false;
            }
            await _db.SaveChangesAsync();

            if (org.Approved)
            {
                // Премахни User роля, добави Organizer роля
                if (await _userManager.IsInRoleAsync(user, GlobalConstants.Roles.User))
                    await _userManager.RemoveFromRoleAsync(user, GlobalConstants.Roles.User);
                if (!await _userManager.IsInRoleAsync(user, GlobalConstants.Roles.Organizer))
                    await _userManager.AddToRoleAsync(user, GlobalConstants.Roles.Organizer);

                TempData["StatusMessage"] = $"{user.UserName} approved as Organizer.";
            }
            else
            {
                // Премахни Organizer роля, върни User роля
                if (await _userManager.IsInRoleAsync(user, GlobalConstants.Roles.Organizer))
                    await _userManager.RemoveFromRoleAsync(user, GlobalConstants.Roles.Organizer);
                if (!await _userManager.IsInRoleAsync(user, GlobalConstants.Roles.User))
                    await _userManager.AddToRoleAsync(user, GlobalConstants.Roles.User);

                TempData["StatusMessage"] = $"{user.UserName} reverted to User.";
            }

            return RedirectToAction(nameof(Organizers));
        }

        public async Task<IActionResult> Events(bool? pending)
        {
            var query = _db.Events.AsNoTracking().AsQueryable();
            if (pending == true)
                query = query.Where(e => !e.IsApproved || e.ChangeRequests.Any(r => r.Status == EventChangeRequestStatus.Pending));

            var events = await query
                .OrderByDescending(e => e.CreatedAt)
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
                    HasPendingChanges = e.ChangeRequests.Any(r => r.Status == EventChangeRequestStatus.Pending),
                    OrganizerId = e.OrganizerId,
                    OrganizerProfileId = e.OrganizerProfileId,
                    OrganizerName = e.OrganizerProfile != null ? e.OrganizerProfile.DisplayName : e.Organizer.UserName ?? string.Empty,
                    LikesCount = e.Likes.Count,
                    CommentsCount = e.Comments.Count,
                    SavesCount = e.Saves.Count,
                    GoingCount = e.Attendances.Count(a => a.Status == EventAttendanceStatus.Going),
                    InterestedCount = e.Attendances.Count(a => a.Status == EventAttendanceStatus.Interested),
                    HasActiveTickets = e.Tickets.Any(t => t.IsActive),
                    HasPaidTickets = e.Tickets.Any(t => t.IsActive && t.Price > 0m),
                    LowestPaidTicketPrice = e.Tickets
                        .Where(t => t.IsActive && t.Price > 0m)
                        .Min(t => (decimal?)t.Price),
                })
                .ToListAsync();

            ViewBag.PendingOnly = pending == true;
            return View(events);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveEvent(int id)
        {
            var ev = await _db.Events
                .Include(e => e.EventSeries)
                .FirstOrDefaultAsync(e => e.Id == id);
            if (ev == null) return NotFound();

            var pendingChange = await _db.EventChangeRequests
                .Where(r => r.EventId == id && r.Status == EventChangeRequestStatus.Pending)
                .OrderByDescending(r => r.SubmittedAt)
                .FirstOrDefaultAsync();

            if (pendingChange != null)
            {
                var payload = JsonSerializer.Deserialize<EventPendingChangePayload>(pendingChange.ChangeJson);
                if (payload == null)
                {
                    pendingChange.Status = EventChangeRequestStatus.Rejected;
                    pendingChange.ReviewedAt = DateTime.UtcNow;
                    pendingChange.ReviewedByAdminId = _userManager.GetUserId(User);
                    await _db.SaveChangesAsync();
                    TempData["StatusMessage"] = "Промените не могат да бъдат прочетени и бяха отхвърлени.";
                    return RedirectToAction(nameof(Events), new { pending = true });
                }

                await ApplyEventChangePayloadAsync(ev, payload);
                pendingChange.Status = EventChangeRequestStatus.Approved;
                pendingChange.ReviewedAt = DateTime.UtcNow;
                pendingChange.ReviewedByAdminId = _userManager.GetUserId(User);
                await _db.SaveChangesAsync();

                TempData["StatusMessage"] = "Промените по събитието са одобрени и публикувани.";
                return RedirectToAction(nameof(Events), new { pending = true });
            }

            ev.IsApproved = !ev.IsApproved;
            await _db.SaveChangesAsync();
            TempData["StatusMessage"] = ev.IsApproved ? "Event approved." : "Event unapproved.";
            return RedirectToAction(nameof(Events));
        }

        public async Task<IActionResult> EventChange(int id)
        {
            var request = await _db.EventChangeRequests
                .AsNoTracking()
                .Include(r => r.Event)
                .Where(r => r.EventId == id && r.Status == EventChangeRequestStatus.Pending)
                .OrderByDescending(r => r.SubmittedAt)
                .FirstOrDefaultAsync();

            if (request == null)
            {
                return NotFound();
            }

            var payload = JsonSerializer.Deserialize<EventPendingChangePayload>(request.ChangeJson);
            if (payload == null)
            {
                return NotFound();
            }

            var vm = new EventChangeReviewViewModel
            {
                EventId = request.EventId,
                CurrentTitle = request.Event.Title,
                CurrentCity = request.Event.City,
                CurrentAddress = request.Event.Address,
                CurrentStartTime = request.Event.StartTime,
                CurrentEndTime = request.Event.EndTime,
                CurrentGenre = request.Event.Genre,
                CurrentImageUrl = request.Event.ImageUrl,
                SubmittedAt = request.SubmittedAt,
                Pending = payload,
            };

            return View(vm);
        }

        private async Task ApplyEventChangePayloadAsync(Event ev, EventPendingChangePayload payload)
        {
            ev.Title = payload.Title;
            ev.Description = payload.Description;
            ev.City = payload.City;
            ev.Address = payload.Address;
            ev.StartTime = payload.StartTime;
            ev.EndTime = payload.EndTime;
            ev.Genre = payload.Genre;
            ev.GenreTags = payload.GenreTags;
            ev.OrganizerProfileId = payload.OrganizerProfileId;
            ev.BusinessWorkspaceId = payload.BusinessWorkspaceId;
            ev.ImageUrl = payload.ImageUrl;
            ev.Latitude = payload.Latitude;
            ev.Longitude = payload.Longitude;
            ev.TicketingMode = payload.TicketingMode;
            ev.VenueLayoutId = payload.TicketingMode == EventTicketingMode.GeneralAdmission ? null : payload.VenueLayoutId;
            ev.IsApproved = true;

            if (!string.IsNullOrWhiteSpace(payload.ImageUrl)
                && !await _db.EventImages.AnyAsync(i => i.EventId == ev.Id && i.ImageUrl == payload.ImageUrl))
            {
                _db.EventImages.Add(new EventImage { EventId = ev.Id, ImageUrl = payload.ImageUrl });
            }

            if (payload.RecurrenceType == EventRecurrenceType.None)
            {
                if (ev.EventSeries != null)
                {
                    ev.EventSeries.RecurrenceType = EventRecurrenceType.None;
                    ev.EventSeries.Status = EventSeriesStatus.Archived;
                    ev.EventSeries.UpdatedAt = DateTime.UtcNow;
                }
            }
            else if (payload.RecurrenceStartDate.HasValue
                     && payload.RecurrenceEndDate.HasValue
                     && payload.RecurrenceStartTime.HasValue
                     && payload.RecurrenceEndTime.HasValue)
            {
                if (ev.EventSeries == null)
                {
                    ev.EventSeries = new EventSeries
                    {
                        EventId = ev.Id,
                        OrganizerId = ev.OrganizerId,
                    };
                    _db.EventSeries.Add(ev.EventSeries);
                    await _db.SaveChangesAsync();
                }

                ApplySeriesPayload(ev.EventSeries, ev, payload);
                await _db.SaveChangesAsync();
                await _recurringEvents.RegenerateOccurrencesAsync(ev.EventSeries, payload.RecurringEditScope);
            }

            await EnsureSeatInventoriesForEventAsync(ev.Id);
        }

        private static void ApplySeriesPayload(EventSeries series, Event ev, EventPendingChangePayload payload)
        {
            series.Title = ev.Title;
            series.Description = ev.Description;
            series.Category = ev.Genre;
            series.GenreTags = ev.GenreTags;
            series.Location = ev.Address;
            series.City = ev.City;
            series.ImageUrl = ev.ImageUrl;
            series.RecurrenceType = payload.RecurrenceType;
            series.Interval = Math.Max(1, payload.RecurrenceInterval);
            series.DaysOfWeek = SerializeDays(payload.SelectedDaysOfWeek);
            series.OccurrenceDisplayMode = payload.OccurrenceDisplayMode;
            series.StartDate = payload.RecurrenceStartDate!.Value.Date;
            series.EndDate = payload.RecurrenceEndDate!.Value.Date;
            series.StartTime = payload.RecurrenceStartTime!.Value;
            series.EndTime = payload.RecurrenceEndTime!.Value;
            series.TimeZone = string.IsNullOrWhiteSpace(payload.TimeZone) ? "Europe/Sofia" : payload.TimeZone.Trim();
            series.Status = EventSeriesStatus.Published;
            series.UpdatedAt = DateTime.UtcNow;
        }

        private async Task EnsureSeatInventoriesForEventAsync(int eventId)
        {
            var ev = await _db.Events
                .AsNoTracking()
                .Include(e => e.EventSeries)
                    .ThenInclude(s => s!.Occurrences)
                .FirstOrDefaultAsync(e => e.Id == eventId);

            if (ev?.VenueLayoutId == null || ev.TicketingMode == EventTicketingMode.GeneralAdmission)
            {
                return;
            }

            if (ev.EventSeries?.Occurrences.Any() == true)
            {
                foreach (var occurrence in ev.EventSeries.Occurrences)
                {
                    await _layouts.EnsureInventoryAsync(eventId, occurrence.Id, ev.VenueLayoutId.Value);
                }
            }
            else
            {
                await _layouts.EnsureInventoryAsync(eventId, null, ev.VenueLayoutId.Value);
            }
        }

        private static string? SerializeDays(IEnumerable<DayOfWeek> days)
        {
            var value = string.Join(",", days.Distinct().OrderBy(d => d).Select(d => d.ToString()));
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }

        public async Task<IActionResult> Posts()
        {
            var posts = await _db.Posts
                .AsNoTracking()
                .OrderByDescending(p => p.CreatedAt)
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
                    AuthorImageUrl = p.Organizer.ProfileImageUrl,
                    AuthorIsOrganizer = p.Organizer.OrganizerData != null && p.Organizer.OrganizerData.Approved,
                })
                .ToListAsync();

            return View(posts);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeletePost(int id)
        {
            var post = await _db.Posts.FirstOrDefaultAsync(p => p.Id == id);
            if (post == null) return NotFound();

            _db.Posts.Remove(post);
            await _db.SaveChangesAsync();
            TempData["StatusMessage"] = "Post deleted.";
            return RedirectToAction(nameof(Posts));
        }

        public async Task<IActionResult> Users()
        {
            var users = await _db.Users.AsNoTracking().OrderBy(u => u.UserName).ToListAsync();
            var model = new List<(ApplicationUser User, IList<string> Roles)>();
            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                model.Add((user, roles));
            }
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteUser(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return BadRequest();
            }

            var currentAdminId = _userManager.GetUserId(User);
            if (string.Equals(id, currentAdminId, StringComparison.Ordinal))
            {
                TempData["StatusMessage"] = "Не можеш да изтриеш собствения си админ акаунт.";
                return RedirectToAction(nameof(Users));
            }

            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            if (await _userManager.IsInRoleAsync(user, GlobalConstants.Roles.Admin))
            {
                var admins = await _userManager.GetUsersInRoleAsync(GlobalConstants.Roles.Admin);
                if (admins.Count <= 1)
                {
                    TempData["StatusMessage"] = "Не можеш да изтриеш последния админ акаунт.";
                    return RedirectToAction(nameof(Users));
                }
            }

            var eventIds = await _db.Events
                .Where(e => e.OrganizerId == id)
                .Select(e => e.Id)
                .ToListAsync();
            foreach (var eventId in eventIds)
            {
                await _eventDeletion.DeleteEventAsync(eventId, preservePaidTickets: false);
            }

            await using var tx = await _db.Database.BeginTransactionAsync();

            var organizerProfileIds = await _db.OrganizerProfiles
                .Where(p => p.OwnerId == id)
                .Select(p => p.Id)
                .ToListAsync();
            var workspaceIds = await _db.BusinessWorkspaces
                .Where(w => w.OwnerId == id)
                .Select(w => w.Id)
                .ToListAsync();
            var venueLayoutIds = await _db.VenueLayouts
                .Where(l => l.OrganizerId == id)
                .Select(l => l.Id)
                .ToListAsync();
            var ownedPostIds = await _db.Posts
                .Where(p => p.OrganizerId == id)
                .Select(p => p.Id)
                .ToListAsync();

            if (ownedPostIds.Count > 0)
            {
                await _db.Messages
                    .Where(m => m.SharedPostId.HasValue && ownedPostIds.Contains(m.SharedPostId.Value))
                    .ExecuteUpdateAsync(s => s.SetProperty(m => m.SharedPostId, (int?)null));
                var ownedPostCommentIds = await _db.PostComments
                    .Where(c => ownedPostIds.Contains(c.PostId))
                    .Select(c => c.Id)
                    .ToListAsync();
                if (ownedPostCommentIds.Count > 0)
                {
                    await _db.PostComments
                        .Where(c => c.ParentCommentId.HasValue && ownedPostCommentIds.Contains(c.ParentCommentId.Value))
                        .ExecuteUpdateAsync(s => s.SetProperty(c => c.ParentCommentId, (int?)null));
                }

                await _db.Posts.Where(p => ownedPostIds.Contains(p.Id)).ExecuteDeleteAsync();
            }

            if (organizerProfileIds.Count > 0)
            {
                await _db.Messages
                    .Where(m => m.AuthorOrganizerProfileId.HasValue && organizerProfileIds.Contains(m.AuthorOrganizerProfileId.Value))
                    .ExecuteUpdateAsync(s => s.SetProperty(m => m.AuthorOrganizerProfileId, (int?)null));
                await _db.PostComments
                    .Where(c => c.AuthorOrganizerProfileId.HasValue && organizerProfileIds.Contains(c.AuthorOrganizerProfileId.Value))
                    .ExecuteUpdateAsync(s => s.SetProperty(c => c.AuthorOrganizerProfileId, (int?)null));
                await _db.EventComments
                    .Where(c => c.AuthorOrganizerProfileId.HasValue && organizerProfileIds.Contains(c.AuthorOrganizerProfileId.Value))
                    .ExecuteUpdateAsync(s => s.SetProperty(c => c.AuthorOrganizerProfileId, (int?)null));
                await _db.Conversations
                    .Where(c => c.OrganizerProfileId.HasValue && organizerProfileIds.Contains(c.OrganizerProfileId.Value))
                    .ExecuteDeleteAsync();
            }

            var postCommentIds = await _db.PostComments.Where(c => c.UserId == id).Select(c => c.Id).ToListAsync();
            if (postCommentIds.Count > 0)
            {
                await _db.PostComments
                    .Where(c => c.ParentCommentId.HasValue && postCommentIds.Contains(c.ParentCommentId.Value))
                    .ExecuteUpdateAsync(s => s.SetProperty(c => c.ParentCommentId, (int?)null));
            }

            var eventCommentIds = await _db.EventComments.Where(c => c.UserId == id).Select(c => c.Id).ToListAsync();
            if (eventCommentIds.Count > 0)
            {
                await _db.EventComments
                    .Where(c => c.ParentCommentId.HasValue && eventCommentIds.Contains(c.ParentCommentId.Value))
                    .ExecuteUpdateAsync(s => s.SetProperty(c => c.ParentCommentId, (int?)null));
            }

            await _db.PostCommentLikes.Where(l => l.UserId == id).ExecuteDeleteAsync();
            await _db.EventCommentLikes.Where(l => l.UserId == id).ExecuteDeleteAsync();
            await _db.PostComments.Where(c => c.UserId == id).ExecuteDeleteAsync();
            await _db.EventComments.Where(c => c.UserId == id).ExecuteDeleteAsync();
            await _db.PostLikes.Where(l => l.UserId == id).ExecuteDeleteAsync();
            await _db.PostSaves.Where(s => s.UserId == id).ExecuteDeleteAsync();
            await _db.EventLikes.Where(l => l.UserId == id).ExecuteDeleteAsync();
            await _db.EventSaves.Where(s => s.UserId == id).ExecuteDeleteAsync();
            await _db.EventAttendances.Where(a => a.UserId == id).ExecuteDeleteAsync();
            await _db.Follows.Where(f => f.FollowerId == id || f.FollowingId == id).ExecuteDeleteAsync();
            await _db.UserProfileSharedEvents.Where(s => s.UserId == id).ExecuteDeleteAsync();
            await _db.UserActivities.Where(a => a.UserId == id).ExecuteDeleteAsync();
            await _db.UserActivities
                .Where(a => a.TargetUserId == id)
                .ExecuteUpdateAsync(s => s.SetProperty(a => a.TargetUserId, (string?)null));
            await _db.UserPushSubscriptions.Where(s => s.UserId == id).ExecuteDeleteAsync();
            await _db.PasswordResetRequests.Where(r => r.UserId == id).ExecuteDeleteAsync();
            await _db.EmailConfirmationRequests.Where(r => r.UserId == id).ExecuteDeleteAsync();
            await _db.OrganizerValidatorAssignments
                .Where(a => a.OrganizerId == id || a.ValidatorUserId == id)
                .ExecuteDeleteAsync();

            var conversationIds = await _db.Conversations
                .Where(c => c.ParticipantOneId == id || c.ParticipantTwoId == id)
                .Select(c => c.Id)
                .ToListAsync();
            if (conversationIds.Count > 0)
            {
                var conversationMessageIds = await _db.Messages
                    .Where(m => conversationIds.Contains(m.ConversationId))
                    .Select(m => m.Id)
                    .ToListAsync();
                if (conversationMessageIds.Count > 0)
                {
                    await _db.Messages
                        .Where(m => m.ReplyToMessageId.HasValue && conversationMessageIds.Contains(m.ReplyToMessageId.Value))
                        .ExecuteUpdateAsync(s => s.SetProperty(m => m.ReplyToMessageId, (int?)null));
                }

                await _db.Conversations.Where(c => conversationIds.Contains(c.Id)).ExecuteDeleteAsync();
            }

            var sentMessageIds = await _db.Messages.Where(m => m.SenderId == id).Select(m => m.Id).ToListAsync();
            if (sentMessageIds.Count > 0)
            {
                await _db.Messages
                    .Where(m => m.ReplyToMessageId.HasValue && sentMessageIds.Contains(m.ReplyToMessageId.Value))
                    .ExecuteUpdateAsync(s => s.SetProperty(m => m.ReplyToMessageId, (int?)null));
            }

            await _db.MessageLikes.Where(l => l.UserId == id).ExecuteDeleteAsync();
            await _db.Messages.Where(m => m.SenderId == id).ExecuteDeleteAsync();
            await _db.Conversations
                .Where(c => c.RequestedByUserId == id)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.RequestedByUserId, (string?)null));

            var transactionIds = await _db.Transactions
                .Where(t => t.UserId == id)
                .Select(t => t.Id)
                .ToListAsync();
            if (transactionIds.Count > 0)
            {
                var userTicketIds = await _db.UserTickets
                    .Where(ut => transactionIds.Contains(ut.TransactionId))
                    .Select(ut => ut.Id)
                    .ToListAsync();
                if (userTicketIds.Count > 0)
                {
                    await _db.EventSeatInventories
                        .Where(i => i.TicketId.HasValue && userTicketIds.Contains(i.TicketId.Value))
                        .ExecuteUpdateAsync(s => s
                            .SetProperty(i => i.TicketId, (Guid?)null)
                            .SetProperty(i => i.ReservedByUserId, (string?)null)
                            .SetProperty(i => i.Status, EventSeatInventoryStatus.Available));
                    await _db.UserTickets.Where(ut => userTicketIds.Contains(ut.Id)).ExecuteDeleteAsync();
                }

                await _db.Transactions.Where(t => transactionIds.Contains(t.Id)).ExecuteDeleteAsync();
            }

            await _db.EventSeatInventories
                .Where(i => i.ReservedByUserId == id)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(i => i.ReservedByUserId, (string?)null)
                    .SetProperty(i => i.Status, EventSeatInventoryStatus.Available));
            await _db.UserTickets
                .Where(ut => ut.UsedByOrganizerId == id)
                .ExecuteUpdateAsync(s => s.SetProperty(ut => ut.UsedByOrganizerId, (string?)null));

            if (workspaceIds.Count > 0)
            {
                await _db.Transactions
                    .Where(t => t.BusinessWorkspaceId.HasValue && workspaceIds.Contains(t.BusinessWorkspaceId.Value))
                    .ExecuteUpdateAsync(s => s.SetProperty(t => t.BusinessWorkspaceId, (int?)null));
            }

            if (venueLayoutIds.Count > 0)
            {
                await _db.EventSeatInventories
                    .Where(i => venueLayoutIds.Contains(i.Seat.VenueLayoutId))
                    .ExecuteDeleteAsync();
                await _db.Seats.Where(s => venueLayoutIds.Contains(s.VenueLayoutId)).ExecuteDeleteAsync();
                await _db.LayoutSections.Where(s => venueLayoutIds.Contains(s.VenueLayoutId)).ExecuteDeleteAsync();
                await _db.VenueLayouts.Where(l => venueLayoutIds.Contains(l.Id)).ExecuteDeleteAsync();
            }

            await _db.OrganizerProfiles.Where(p => p.OwnerId == id).ExecuteDeleteAsync();
            await _db.BusinessWorkspaces.Where(w => w.OwnerId == id).ExecuteDeleteAsync();
            await _db.Users.Where(u => u.PinnedEventId.HasValue && eventIds.Contains(u.PinnedEventId.Value))
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.PinnedEventId, (int?)null));

            var deleteResult = await _userManager.DeleteAsync(user);
            if (!deleteResult.Succeeded)
            {
                await tx.RollbackAsync();
                foreach (var error in deleteResult.Errors)
                {
                    TempData["StatusMessage"] = $"Не успяхме да изтрием потребителя: {error.Description}";
                    break;
                }

                return RedirectToAction(nameof(Users));
            }

            await tx.CommitAsync();
            TempData["StatusMessage"] = $"Потребителят {user.UserName ?? user.Email} беше изтрит от базата.";
            return RedirectToAction(nameof(Users));
        }

        public async Task<IActionResult> Tickets()
        {
            var ticketTypesCount = await _db.Tickets.CountAsync();
            var soldCount = await _db.UserTickets.CountAsync();
            var usedCount = await _db.UserTickets.CountAsync(ut => ut.IsUsed);
            var unusedCount = soldCount - usedCount;

            var totalRevenue = await _db.Transactions
                .Where(t => t.Status == GlobalConstants.TransactionStatuses.Paid)
                .SumAsync(t => (decimal?)t.TotalAmount) ?? 0m;

            var recent = await _db.UserTickets
                .AsNoTracking()
                .OrderByDescending(ut => ut.CreatedAt)
                .Take(50)
                .Select(ut => new AdminTicketRowViewModel
                {
                    Id = ut.Id,
                    TicketName = ut.Ticket.Name,
                    EventTitle = ut.Ticket.Event.Title,
                    EventId = ut.Ticket.EventId,
                    OwnerUserName = ut.Transaction.User.UserName ?? string.Empty,
                    CreatedAt = ut.CreatedAt,
                    IsUsed = ut.IsUsed,
                    Price = ut.Ticket.Price,
                })
                .ToListAsync();

            return View(new AdminTicketsViewModel
            {
                TicketTypesCount = ticketTypesCount,
                TicketsSoldCount = soldCount,
                UsedTicketsCount = usedCount,
                UnusedTicketsCount = unusedCount,
                TotalRevenue = totalRevenue,
                Recent = recent,
            });
        }

        public async Task<IActionResult> Transactions()
        {
            var transactions = await _db.Transactions
                .AsNoTracking()
                .OrderByDescending(t => t.CreatedAt)
                .Select(t => new AdminTransactionRowViewModel
                {
                    Id = t.Id,
                    UserName = t.User.UserName ?? string.Empty,
                    Email = t.User.Email ?? string.Empty,
                    TotalAmount = t.TotalAmount,
                    Status = t.Status,
                    CreatedAt = t.CreatedAt,
                    TicketCount = t.UserTickets.Count,
                })
                .ToListAsync();

            var totalRevenue = transactions
                .Where(t => t.Status == GlobalConstants.TransactionStatuses.Paid)
                .Sum(t => t.TotalAmount);

            return View(new AdminTransactionsViewModel
            {
                Transactions = transactions,
                TotalRevenue = totalRevenue,
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetRole(string userId, string role)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound();

            var validRoles = new[] { GlobalConstants.Roles.User, GlobalConstants.Roles.Organizer, GlobalConstants.Roles.Admin };
            if (!validRoles.Contains(role)) return BadRequest();

            var currentRoles = await _userManager.GetRolesAsync(user);
            await _userManager.RemoveFromRolesAsync(user, currentRoles);
            await _userManager.AddToRoleAsync(user, role);

            TempData["StatusMessage"] = $"{user.UserName} is now {role}.";
            return RedirectToAction(nameof(Users));
        }
    }
}

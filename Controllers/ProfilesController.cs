using EventsApp.Common;
using EventsApp.Data;
using EventsApp.Models;
using EventsApp.Services;
using EventsApp.ViewModels.Events;
using EventsApp.ViewModels.Posts;
using EventsApp.ViewModels.Social;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EventsApp.Controllers
{
    public class ProfilesController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ISocialFeedService _socialFeed;
        private readonly IPlatformPermissionService _permissions;

        public ProfilesController(
            ApplicationDbContext db,
            UserManager<ApplicationUser> userManager,
            ISocialFeedService socialFeed,
            IPlatformPermissionService permissions)
        {
            _db = db;
            _userManager = userManager;
            _socialFeed = socialFeed;
            _permissions = permissions;
        }

        public async Task<IActionResult> Details(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return NotFound();
            }

            var currentUserId = _userManager.GetUserId(User);
            var isAdmin = User.IsInRole(GlobalConstants.Roles.Admin);

            var user = await _db.Users
                .AsNoTracking()
                .Include(u => u.OrganizerData)
                .FirstOrDefaultAsync(u => u.Id == id);

            if (user == null)
            {
                return NotFound();
            }

            var displayName = GetDisplayName(user);
            var isCurrentUser = currentUserId == id;

            var eventsQuery = _db.Events
                .AsNoTracking()
                .Where(e => e.OrganizerId == id);

            if (!isAdmin && !isCurrentUser)
            {
                eventsQuery = eventsQuery.Where(e => e.IsApproved);
            }

            var posts = await _db.Posts
                .AsNoTracking()
                .Where(p => p.OrganizerId == id)
                .Where(p => user.OrganizerData != null && user.OrganizerData.Approved)
                .OrderByDescending(p => p.CreatedAt)
                .Take(12)
                .Select(p => new PostCardViewModel
                {
                    Id = p.Id,
                    OrganizerId = p.OrganizerId,
                    OrganizerName = displayName,
                    AuthorImageUrl = user.ProfileImageUrl,
                    AuthorIsOrganizer = user.OrganizerData != null && user.OrganizerData.Approved,
                    Content = p.Content,
                    CreatedAt = p.CreatedAt,
                    EventId = p.EventId,
                    EventTitle = p.Event != null ? p.Event.Title : null,
                    FirstMediaUrl = p.Images.Select(i => i.ImageUrl).FirstOrDefault(),
                    FirstMediaType = p.Images.Select(i => i.MediaType).FirstOrDefault(),
                    LikesCount = p.Likes.Count,
                    CommentsCount = p.Comments.Count,
                    SavesCount = p.Saves.Count,
                    CurrentUserLiked = currentUserId != null && p.Likes.Any(l => l.UserId == currentUserId),
                    CurrentUserSaved = currentUserId != null && p.Saves.Any(s => s.UserId == currentUserId),
                })
                .ToListAsync();

            var pinnedEvent = user.PinnedEventId.HasValue
                ? await QueryEventCards(_db.Events.AsNoTracking().Where(e => e.Id == user.PinnedEventId.Value && e.IsApproved), currentUserId)
                    .FirstOrDefaultAsync()
                : null;

            var sharedEvents = await QueryEventCards(_db.UserProfileSharedEvents
                    .AsNoTracking()
                    .Where(s => s.UserId == id && s.Event.IsApproved)
                    .OrderByDescending(s => s.CreatedAt)
                    .Select(s => s.Event)
                    .Take(6),
                currentUserId)
                .ToListAsync();

            var savedEvents = isCurrentUser
                ? await QueryEventCards(_db.EventSaves
                        .AsNoTracking()
                        .Where(s => s.UserId == id && s.Event.IsApproved)
                        .OrderByDescending(s => s.CreatedAt)
                        .Select(s => s.Event)
                        .Take(6),
                    currentUserId)
                    .ToListAsync()
                : new List<EventCardViewModel>();

            var goingEvents = await QueryEventCards(_db.EventAttendances
                    .AsNoTracking()
                    .Where(a => a.UserId == id && a.Event.IsApproved && a.Status == EventAttendanceStatus.Going)
                    .OrderByDescending(a => a.CreatedAt)
                    .Select(a => a.Event)
                    .Take(6),
                currentUserId)
                .ToListAsync();

            var preferences = await _db.UserPreferences.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == id);
            var vibeTags = new List<string>();
            if (preferences?.PreferredGenre != null)
            {
                vibeTags.Add(preferences.PreferredGenre.Value.GetDisplayName());
            }
            if (!string.IsNullOrWhiteSpace(preferences?.PreferredCity))
            {
                vibeTags.Add(preferences.PreferredCity);
            }
            var likedGenres = await _db.EventLikes
                .AsNoTracking()
                .Where(l => l.UserId == id)
                .GroupBy(l => l.Event.Genre)
                .OrderByDescending(g => g.Count())
                .Take(4)
                .Select(g => g.Key)
                .ToListAsync();
            vibeTags.AddRange(likedGenres.Select(g => g.GetDisplayName()));
            vibeTags = vibeTags
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(8)
                .ToList();

            var canViewStatus = await _permissions.CanViewProfileSectionAsync(User, id, user.ProfileStatusVisibility);

            var events = await eventsQuery
                .OrderByDescending(e => e.StartTime)
                .Take(12)
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
                    OrganizerName = displayName,
                    LikesCount = e.Likes.Count,
                    CommentsCount = e.Comments.Count,
                    SavesCount = e.Saves.Count,
                    GoingCount = e.Attendances.Count(a => a.Status == EventAttendanceStatus.Going),
                    InterestedCount = e.Attendances.Count(a => a.Status == EventAttendanceStatus.Interested),
                    CurrentUserLiked = currentUserId != null && e.Likes.Any(l => l.UserId == currentUserId),
                    CurrentUserSaved = currentUserId != null && e.Saves.Any(s => s.UserId == currentUserId),
                    CurrentUserAttendanceStatus = currentUserId == null
                        ? null
                        : e.Attendances
                            .Where(a => a.UserId == currentUserId)
                            .Select(a => (EventAttendanceStatus?)a.Status)
                            .FirstOrDefault(),
                })
                .ToListAsync();

            var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);

            var attendedCount = await _db.EventAttendances
                .CountAsync(a => a.UserId == id && a.Status == EventAttendanceStatus.Going);
            var interestedCount = await _db.EventAttendances
                .CountAsync(a => a.UserId == id && a.Status == EventAttendanceStatus.Interested);
            var likesGiven = await _db.EventLikes.CountAsync(l => l.UserId == id);
            var monthlyEvents = await _db.EventAttendances
                .Where(a => a.UserId == id && a.Status == EventAttendanceStatus.Going)
                .Join(_db.Events, a => a.EventId, e => e.Id, (a, e) => e)
                .CountAsync(e => e.StartTime >= monthStart);
            var monthlyFollowers = await _db.Follows
                .CountAsync(f => f.FollowingId == id && f.CreatedAt >= monthStart);
            var favGenre = await _db.EventAttendances
                .Where(a => a.UserId == id && a.Status == EventAttendanceStatus.Going)
                .Join(_db.Events, a => a.EventId, e => e.Id, (a, e) => e.Genre)
                .GroupBy(g => g)
                .OrderByDescending(g => g.Count())
                .Select(g => (EventGenre?)g.Key)
                .FirstOrDefaultAsync();
            var citiesVisited = await _db.EventAttendances
                .Where(a => a.UserId == id && a.Status == EventAttendanceStatus.Going)
                .Join(_db.Events, a => a.EventId, e => e.Id, (a, e) => e.City)
                .Distinct()
                .CountAsync();

            var memories = new List<MemoryItem>();
            if (isCurrentUser)
            {
                var today = DateTime.UtcNow.Date;
                for (var y = 1; y <= 5; y++)
                {
                    var rangeStart = today.AddYears(-y).AddDays(-3);
                    var rangeEnd = today.AddYears(-y).AddDays(3);
                    var found = await _db.EventAttendances
                        .Where(a => a.UserId == id && a.Status == EventAttendanceStatus.Going)
                        .Join(_db.Events, a => a.EventId, e => e.Id, (a, e) => e)
                        .Where(e => e.StartTime >= rangeStart && e.StartTime <= rangeEnd)
                        .OrderBy(e => e.StartTime)
                        .Select(e => new MemoryItem
                        {
                            EventId = e.Id,
                            Title = e.Title,
                            City = e.City,
                            ImageUrl = e.ImageUrl,
                            EventDate = e.StartTime,
                            YearsAgo = y,
                        })
                        .Take(3)
                        .ToListAsync();
                    memories.AddRange(found);
                }
            }

            var vm = new PublicProfileViewModel
            {
                Id = user.Id,
                DisplayName = displayName,
                UserName = user.UserName,
                Bio = user.Bio ?? user.OrganizerData?.Description,
                ProfileImageUrl = user.ProfileImageUrl,
                IsOrganizer = user.OrganizerData?.Approved ?? false,
                OrganizationName = user.OrganizerData?.OrganizationName,
                Website = user.OrganizerData?.Website,
                ProfileStatusText = canViewStatus ? user.ProfileStatusText : null,
                ProfileStatusEmoji = canViewStatus ? user.ProfileStatusEmoji : null,
                ProfileStatusUpdatedAt = canViewStatus ? user.ProfileStatusUpdatedAt : null,
                ProfileStatusVisibility = user.ProfileStatusVisibility,
                PinnedEventId = user.PinnedEventId,
                FollowersCount = await _db.Follows.CountAsync(f => f.FollowingId == id),
                FollowingCount = await _db.Follows.CountAsync(f => f.FollowerId == id),
                PostsCount = user.OrganizerData?.Approved == true ? await _db.Posts.CountAsync(p => p.OrganizerId == id) : 0,
                EventsCount = await eventsQuery.CountAsync(),
                SavedEventsCount = await _db.EventSaves.CountAsync(s => s.UserId == id),
                GoingEventsCount = await _db.EventAttendances.CountAsync(a => a.UserId == id && a.Status == EventAttendanceStatus.Going),
                TicketsCount = isCurrentUser ? await _db.UserTickets.CountAsync(t => t.Transaction.UserId == id) : 0,
                VibeTags = vibeTags,
                EventsAttendedCount = attendedCount,
                EventsInterestedCount = interestedCount,
                LikesGivenCount = likesGiven,
                MonthlyEventsCount = monthlyEvents,
                MonthlyNewFollowersCount = monthlyFollowers,
                FavouriteGenre = favGenre?.GetDisplayName(),
                CitiesVisitedCount = citiesVisited,
                CurrentUserFollows = currentUserId != null && await _db.Follows.AnyAsync(f => f.FollowerId == currentUserId && f.FollowingId == id),
                IsCurrentUser = isCurrentUser,
                CanStartConversation = currentUserId != null && !isCurrentUser && await _permissions.CanMessageUserAsync(User, id),
                CanEditProfileStatus = await _permissions.CanEditProfileStatusAsync(User, id),
                PinnedEvent = pinnedEvent,
                SharedEvents = sharedEvents,
                SavedEvents = savedEvents,
                GoingEvents = goingEvents,
                Posts = posts,
                Events = events,
                Memories = memories,
            };

            return View(vm);
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(string statusText, string? statusEmoji, ProfileStatusVisibility visibility = ProfileStatusVisibility.Public)
        {
            var currentUserId = _userManager.GetUserId(User)!;
            if (!await _permissions.CanEditProfileStatusAsync(User, currentUserId))
            {
                return Forbid();
            }

            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == currentUserId);
            if (user == null) return Challenge();

            statusText = string.IsNullOrWhiteSpace(statusText) ? string.Empty : statusText.Trim();
            if (statusText.Length > GlobalConstants.User.ProfileStatusMaxLength)
            {
                statusText = statusText[..GlobalConstants.User.ProfileStatusMaxLength];
            }

            user.ProfileStatusText = string.IsNullOrWhiteSpace(statusText) ? null : statusText;
            user.ProfileStatusEmoji = string.IsNullOrWhiteSpace(statusEmoji) ? null : statusEmoji.Trim()[..Math.Min(statusEmoji.Trim().Length, GlobalConstants.User.ProfileStatusEmojiMaxLength)];
            user.ProfileStatusVisibility = visibility;
            user.ProfileStatusUpdatedAt = user.ProfileStatusText == null ? null : DateTime.UtcNow;

            await _db.SaveChangesAsync();
            TempData["StatusMessage"] = user.ProfileStatusText == null ? "Status cleared." : "Status updated.";
            return RedirectToAction(nameof(Details), new { id = currentUserId });
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PinEvent(int eventId, string? returnUrl)
        {
            var currentUserId = _userManager.GetUserId(User)!;
            if (!await _permissions.CanPinEventAsync(User, currentUserId, eventId))
            {
                return Forbid();
            }

            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == currentUserId);
            if (user == null) return Challenge();

            user.PinnedEventId = eventId;
            await _db.SaveChangesAsync();
            TempData["StatusMessage"] = "Event pinned to your profile.";
            return SafeRedirect(returnUrl) ?? RedirectToAction(nameof(Details), new { id = currentUserId });
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UnpinEvent(string? returnUrl)
        {
            var currentUserId = _userManager.GetUserId(User)!;
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == currentUserId);
            if (user == null) return Challenge();

            user.PinnedEventId = null;
            await _db.SaveChangesAsync();
            TempData["StatusMessage"] = "Pinned event removed.";
            return SafeRedirect(returnUrl) ?? RedirectToAction(nameof(Details), new { id = currentUserId });
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ShareEvent(int eventId, string? returnUrl)
        {
            var currentUserId = _userManager.GetUserId(User)!;
            if (!await _permissions.CanShareEventToProfileAsync(User, currentUserId, eventId))
            {
                return Forbid();
            }

            var exists = await _db.UserProfileSharedEvents.AnyAsync(s => s.UserId == currentUserId && s.EventId == eventId);
            if (!exists)
            {
                _db.UserProfileSharedEvents.Add(new UserProfileSharedEvent
                {
                    UserId = currentUserId,
                    EventId = eventId,
                });
                await _db.SaveChangesAsync();
            }

            TempData["StatusMessage"] = "Event shared on your profile.";
            return SafeRedirect(returnUrl) ?? RedirectToAction(nameof(Details), new { id = currentUserId });
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveSharedEvent(int eventId, string? returnUrl)
        {
            var currentUserId = _userManager.GetUserId(User)!;
            var shared = await _db.UserProfileSharedEvents.FirstOrDefaultAsync(s => s.UserId == currentUserId && s.EventId == eventId);
            if (shared != null)
            {
                _db.UserProfileSharedEvents.Remove(shared);
                await _db.SaveChangesAsync();
            }

            TempData["StatusMessage"] = "Shared event removed.";
            return SafeRedirect(returnUrl) ?? RedirectToAction(nameof(Details), new { id = currentUserId });
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Follow(string id, string? returnUrl)
        {
            var currentUserId = _userManager.GetUserId(User)!;
            if (string.IsNullOrWhiteSpace(id) || id == currentUserId)
            {
                return SafeRedirect(returnUrl) ?? RedirectToAction(nameof(Details), new { id });
            }

            if (!await _db.Users.AnyAsync(u => u.Id == id))
            {
                return NotFound();
            }

            var exists = await _db.Follows.AnyAsync(f => f.FollowerId == currentUserId && f.FollowingId == id);
            if (!exists)
            {
                _db.Follows.Add(new Follow { FollowerId = currentUserId, FollowingId = id });
                await _db.SaveChangesAsync();
                await _socialFeed.TrackActivityAsync(currentUserId, UserActivityType.UserFollowed, targetUserId: id);
            }

            return SafeRedirect(returnUrl) ?? RedirectToAction(nameof(Details), new { id });
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Unfollow(string id, string? returnUrl)
        {
            var currentUserId = _userManager.GetUserId(User)!;
            var follow = await _db.Follows.FirstOrDefaultAsync(f => f.FollowerId == currentUserId && f.FollowingId == id);
            if (follow != null)
            {
                _db.Follows.Remove(follow);
                await _db.SaveChangesAsync();
            }

            return SafeRedirect(returnUrl) ?? RedirectToAction(nameof(Details), new { id });
        }

        public async Task<IActionResult> Followers(string id)
        {
            return await FollowList(id, followers: true);
        }

        public async Task<IActionResult> Following(string id)
        {
            return await FollowList(id, followers: false);
        }

        private async Task<IActionResult> FollowList(string id, bool followers)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return NotFound();
            }

            var profile = await _db.Users
                .AsNoTracking()
                .Include(u => u.OrganizerData)
                .FirstOrDefaultAsync(u => u.Id == id);

            if (profile == null)
            {
                return NotFound();
            }

            var currentUserId = _userManager.GetUserId(User);
            var query = followers
                ? _db.Follows.AsNoTracking().Where(f => f.FollowingId == id).Select(f => f.Follower)
                : _db.Follows.AsNoTracking().Where(f => f.FollowerId == id).Select(f => f.Following);

            var profiles = await query
                .OrderBy(u => u.UserName)
                .Select(u => new ProfileSummaryViewModel
                {
                    Id = u.Id,
                    DisplayName = u.OrganizerData != null && u.OrganizerData.Approved
                        ? u.OrganizerData.OrganizationName
                        : u.UserName ?? string.Empty,
                    Bio = u.Bio ?? (u.OrganizerData != null ? u.OrganizerData.Description : null),
                    ProfileImageUrl = u.ProfileImageUrl,
                    IsOrganizer = u.OrganizerData != null && u.OrganizerData.Approved,
                    FollowersCount = u.Followers.Count,
                    FollowingCount = u.Following.Count,
                    PostsCount = u.Posts.Count,
                    EventsCount = u.Events.Count(e => e.IsApproved),
                    CurrentUserFollows = currentUserId != null && u.Followers.Any(f => f.FollowerId == currentUserId),
                })
                .ToListAsync();

            return View("FollowList", new FollowListViewModel
            {
                ProfileId = id,
                ProfileName = GetDisplayName(profile),
                ListTitle = followers ? "Followers" : "Following",
                Profiles = profiles,
            });
        }

        private static IQueryable<EventCardViewModel> QueryEventCards(IQueryable<Event> query, string? currentUserId)
        {
            return query.Select(e => new EventCardViewModel
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
                OrganizerName = e.Organizer.OrganizerData != null && e.Organizer.OrganizerData.Approved
                    ? e.Organizer.OrganizerData.OrganizationName
                    : e.Organizer.UserName ?? string.Empty,
                LikesCount = e.Likes.Count,
                CommentsCount = e.Comments.Count,
                SavesCount = e.Saves.Count,
                GoingCount = e.Attendances.Count(a => a.Status == EventAttendanceStatus.Going),
                InterestedCount = e.Attendances.Count(a => a.Status == EventAttendanceStatus.Interested),
                CurrentUserLiked = currentUserId != null && e.Likes.Any(l => l.UserId == currentUserId),
                CurrentUserSaved = currentUserId != null && e.Saves.Any(s => s.UserId == currentUserId),
                CurrentUserAttendanceStatus = currentUserId == null
                    ? null
                    : e.Attendances
                        .Where(a => a.UserId == currentUserId)
                        .Select(a => (EventAttendanceStatus?)a.Status)
                        .FirstOrDefault(),
                Latitude = e.Latitude,
                Longitude = e.Longitude,
            });
        }

        private static string GetDisplayName(ApplicationUser user)
        {
            if (user.OrganizerData?.Approved == true && !string.IsNullOrWhiteSpace(user.OrganizerData.OrganizationName))
            {
                return user.OrganizerData.OrganizationName;
            }

            var name = string.Join(" ", new[] { user.FirstName, user.LastName }.Where(v => !string.IsNullOrWhiteSpace(v)));
            return string.IsNullOrWhiteSpace(name) ? user.UserName ?? string.Empty : name;
        }

        private IActionResult? SafeRedirect(string? returnUrl)
        {
            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            return null;
        }
    }
}

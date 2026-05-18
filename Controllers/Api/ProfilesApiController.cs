using EventsApp.Data;
using EventsApp.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EventsApp.Controllers.Api
{
    [ApiController]
    [Route("api/profiles")]
    [IgnoreAntiforgeryToken]
    public class ProfilesApiController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public ProfilesApiController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        // GET /api/profiles/suggested?take=6
        // If the user has preferences, suggest organizer pages whose events match
        // those genres/city. Otherwise fall back to most-followed users + most-
        // active organizer pages (those with most events / posts).
        [HttpGet("suggested")]
        public async Task<IActionResult> Suggested([FromQuery] int take = 6)
        {
            take = Math.Clamp(take, 1, 20);
            var userId = _userManager.GetUserId(User);
            var halfTake = Math.Max(2, take / 2);

            // Don't suggest the user themselves, admins, or anyone they already follow.
            var adminRoleId = await _db.Roles.Where(r => r.Name == "Admin").Select(r => r.Id).FirstOrDefaultAsync();
            var adminIds = adminRoleId != null
                ? await _db.UserRoles.Where(ur => ur.RoleId == adminRoleId).Select(ur => ur.UserId).ToListAsync()
                : new List<string>();
            var followingIds = userId != null
                ? await _db.Follows.Where(f => f.FollowerId == userId).Select(f => f.FollowingId).ToListAsync()
                : new List<string>();
            var excludeIds = new HashSet<string>(adminIds);
            foreach (var f in followingIds) excludeIds.Add(f);
            if (userId != null) excludeIds.Add(userId);

            // Try preferences-based pages first
            UserPreferences? prefs = null;
            if (userId != null)
            {
                prefs = await _db.UserPreferences.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == userId);
            }

            var pagesQuery = _db.OrganizerProfiles.AsNoTracking();
            if (prefs != null && !excludeIds.Contains(prefs.UserId))
            {
                var preferredGenres = prefs.PreferredGenres.Select(g => g.ToString()).ToList();
                if (preferredGenres.Count > 0)
                {
                    pagesQuery = pagesQuery.Where(p => _db.Events.Any(e =>
                        e.OrganizerId == p.OwnerId &&
                        preferredGenres.Contains(e.Genre.ToString())));
                }
                if (!string.IsNullOrWhiteSpace(prefs.PreferredCity))
                {
                    pagesQuery = pagesQuery.Where(p =>
                        p.City == prefs.PreferredCity ||
                        _db.Events.Any(e => e.OrganizerId == p.OwnerId && e.City == prefs.PreferredCity));
                }
            }

            var pages = await pagesQuery
                .Where(p => !excludeIds.Contains(p.OwnerId))
                .OrderByDescending(p => _db.Events.Count(e => e.OrganizerId == p.OwnerId))
                .ThenByDescending(p => _db.Posts.Count(po => po.OrganizerProfileId == p.Id))
                .Take(halfTake)
                .Select(p => new
                {
                    id = p.Id.ToString(),
                    userId = (string?)null,
                    organizerProfileId = (int?)p.Id,
                    displayName = p.DisplayName,
                    userName = (string?)null,
                    profileImageUrl = p.AvatarImageUrl,
                    bio = p.Tagline ?? p.Description,
                    isOrganizer = true,
                    typeKey = "profile.type.organizer",
                    typeText = "Организатор",
                    followerCount = 0,
                    followersCount = 0,
                    eventsCount = _db.Events.Count(e => e.OrganizerId == p.OwnerId),
                    postsCount = _db.Posts.Count(po => po.OrganizerProfileId == p.Id),
                })
                .ToListAsync();

            // Fill remaining with most-followed users
            var remaining = take - pages.Count;
            var users = remaining > 0
                ? await _db.Users.AsNoTracking()
                    .Where(u => !excludeIds.Contains(u.Id))
                    .OrderByDescending(u => _db.Follows.Count(f => f.FollowingId == u.Id))
                    .ThenByDescending(u => _db.Posts.Count(po => po.OrganizerId == u.Id))
                    .Take(remaining)
                    .Select(u => new
                    {
                        id = u.Id,
                        userId = (string?)u.Id,
                        organizerProfileId = (int?)null,
                        displayName = (u.FirstName + " " + u.LastName).Trim(),
                        userName = u.UserName,
                        profileImageUrl = u.ProfileImageUrl,
                        bio = u.Bio,
                        isOrganizer = false,
                        typeKey = "profile.type.profile",
                        typeText = "Профил",
                        followerCount = _db.Follows.Count(f => f.FollowingId == u.Id),
                        followersCount = _db.Follows.Count(f => f.FollowingId == u.Id),
                        eventsCount = _db.Events.Count(e => e.OrganizerId == u.Id),
                        postsCount = _db.Posts.Count(po => po.OrganizerId == u.Id),
                    })
                    .ToListAsync()
                : new();

            var items = pages.Cast<object>().Concat(users.Cast<object>()).Take(take).ToList();
            return Ok(new { items });
        }

        // GET /api/profiles/search?q=...&take=8
        // Returns mixed list of users + organizer pages matching the query.
        [HttpGet("search")]
        public async Task<IActionResult> Search([FromQuery] string? q, [FromQuery] int take = 8)
        {
            var query = (q ?? string.Empty).Trim();
            if (query.Length < 1)
            {
                return Ok(new { items = Array.Empty<object>() });
            }

            take = Math.Clamp(take, 1, 20);
            var like = $"%{query}%";

            // Hide admin accounts from search results.
            var adminRoleId = await _db.Roles
                .Where(r => r.Name == "Admin")
                .Select(r => r.Id)
                .FirstOrDefaultAsync();
            var adminUserIds = adminRoleId != null
                ? await _db.UserRoles.Where(ur => ur.RoleId == adminRoleId).Select(ur => ur.UserId).ToListAsync()
                : new List<string>();

            var users = await _db.Users
                .AsNoTracking()
                .Where(u => !adminUserIds.Contains(u.Id))
                .Where(u =>
                    (u.UserName != null && EF.Functions.ILike(u.UserName, like)) ||
                    (u.FirstName != null && EF.Functions.ILike(u.FirstName, like)) ||
                    (u.LastName != null && EF.Functions.ILike(u.LastName, like)) ||
                    (u.Bio != null && EF.Functions.ILike(u.Bio, like)))
                .OrderBy(u => u.UserName)
                .Take(take)
                .Select(u => new
                {
                    id = u.Id,
                    userId = u.Id,
                    organizerProfileId = (int?)null,
                    displayName = (u.FirstName + " " + u.LastName).Trim(),
                    userName = u.UserName,
                    profileImageUrl = u.ProfileImageUrl,
                    bio = u.Bio,
                    isOrganizer = false,
                    typeKey = "profile.type.profile",
                    typeText = "Профил",
                    followerCount = _db.Follows.Count(f => f.FollowingId == u.Id),
                    followersCount = _db.Follows.Count(f => f.FollowingId == u.Id),
                    eventsCount = _db.Events.Count(e => e.OrganizerId == u.Id),
                    postsCount = _db.Posts.Count(p => p.OrganizerId == u.Id),
                })
                .ToListAsync();

            var pages = await _db.OrganizerProfiles
                .AsNoTracking()
                .Where(p =>
                    EF.Functions.ILike(p.DisplayName, like) ||
                    (p.Tagline != null && EF.Functions.ILike(p.Tagline, like)) ||
                    (p.Description != null && EF.Functions.ILike(p.Description, like)) ||
                    (p.City != null && EF.Functions.ILike(p.City, like)))
                .OrderBy(p => p.DisplayName)
                .Take(take)
                .Select(p => new
                {
                    id = p.Id.ToString(),
                    userId = (string?)null,
                    organizerProfileId = (int?)p.Id,
                    displayName = p.DisplayName,
                    userName = (string?)null,
                    profileImageUrl = p.AvatarImageUrl,
                    bio = p.Tagline ?? p.Description,
                    isOrganizer = true,
                    typeKey = "profile.type.organizer",
                    typeText = "Организатор",
                    followerCount = 0,
                    followersCount = 0,
                    eventsCount = _db.Events.Count(e => e.OrganizerId == p.OwnerId),
                    postsCount = 0,
                })
                .ToListAsync();

            var items = users.Cast<object>().Concat(pages.Cast<object>()).Take(take).ToList();
            return Ok(new { items });
        }

        // GET /api/profiles/{id}
        [HttpGet("{id}")]
        public async Task<IActionResult> Details(string id)
        {
            var currentUserId = _userManager.GetUserId(User);

            var user = await _db.Users
                .AsNoTracking()
                .Include(u => u.Followers)
                .Include(u => u.Following)
                .FirstOrDefaultAsync(u => u.Id == id);

            if (user == null) return NotFound();

            var roles = await _userManager.GetRolesAsync(user);
            // "Organizer" view is only for users who actually run events — not admins.
            // Admins keep the rich personal profile UI.
            var isOrganizer = roles.Contains("Organizer");
            var isOwn = user.Id == currentUserId;
            var isFollowing = currentUserId != null && user.Followers.Any(f => f.FollowerId == currentUserId);
            var followersCount = user.Followers.Count;
            var followingCount = user.Following.Count;

            var displayName = string.Join(" ", new[] { user.FirstName, user.LastName }
                .Where(s => !string.IsNullOrWhiteSpace(s)));
            if (string.IsNullOrWhiteSpace(displayName)) displayName = user.UserName ?? "Профил";

            var preferences = await _db.UserPreferences
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.UserId == user.Id);
            var vibeTags = preferences?.PreferredGenres.Select(g => g.ToString()).ToArray() ?? Array.Empty<string>();

            // Attendance-based counts (cheap aggregates only — no event payloads here).
            var attendedCount = await _db.EventAttendances
                .CountAsync(a => a.UserId == user.Id && a.Status == EventAttendanceStatus.Going);
            var interestedCount = await _db.EventAttendances
                .CountAsync(a => a.UserId == user.Id && a.Status == EventAttendanceStatus.Interested);

            var now = DateTime.UtcNow;

            var citiesVisitedCount = await _db.EventAttendances
                .Where(a => a.UserId == user.Id && a.Status == EventAttendanceStatus.Going && a.Event!.EndTime < now)
                .Select(a => a.Event!.City)
                .Where(c => c != null && c != "")
                .Distinct()
                .CountAsync();

            // Past attendance — events the user was Going to whose end time has passed.
            var attendedEvents = await _db.EventAttendances
                .AsNoTracking()
                .Where(a => a.UserId == user.Id
                    && a.Status == EventAttendanceStatus.Going
                    && a.Event!.EndTime < now)
                .OrderByDescending(a => a.Event!.StartTime)
                .Take(24)
                .Select(a => new
                {
                    id = a.Event!.Id,
                    title = a.Event.Title,
                    description = a.Event.Description,
                    startTime = a.Event.StartTime,
                    endTime = a.Event.EndTime,
                    genre = a.Event.Genre.ToString(),
                    imageUrl = a.Event.ImageUrl,
                    address = a.Event.Address,
                    city = a.Event.City,
                    organizerProfileId = a.Event.OrganizerProfileId,
                    organizerName = a.Event.OrganizerProfile != null
                        ? a.Event.OrganizerProfile.DisplayName
                        : (a.Event.Organizer.FirstName + " " + a.Event.Organizer.LastName).Trim(),
                })
                .ToListAsync();

            // Upcoming "plans" — events the user is Going to that are still in the future.
            var goingEvents = await _db.EventAttendances
                .AsNoTracking()
                .Where(a => a.UserId == user.Id
                    && a.Status == EventAttendanceStatus.Going
                    && a.Event!.EndTime >= now)
                .OrderBy(a => a.Event!.StartTime)
                .Take(12)
                .Select(a => new
                {
                    id = a.Event!.Id,
                    title = a.Event.Title,
                    description = a.Event.Description,
                    startTime = a.Event.StartTime,
                    endTime = a.Event.EndTime,
                    genre = a.Event.Genre.ToString(),
                    imageUrl = a.Event.ImageUrl,
                    address = a.Event.Address,
                    city = a.Event.City,
                    organizerProfileId = a.Event.OrganizerProfileId,
                    organizerName = a.Event.OrganizerProfile != null
                        ? a.Event.OrganizerProfile.DisplayName
                        : (a.Event.Organizer.FirstName + " " + a.Event.Organizer.LastName).Trim(),
                })
                .ToListAsync();

            // Saved/bookmarked events — only visible on own profile.
            var savedEvents = isOwn
                ? await _db.EventSaves
                    .AsNoTracking()
                    .Where(s => s.UserId == user.Id && s.Event!.EndTime >= now)
                    .OrderBy(s => s.Event!.StartTime)
                    .Take(12)
                    .Select(s => new
                    {
                        id = s.Event!.Id,
                        title = s.Event.Title,
                        description = s.Event.Description,
                        startTime = s.Event.StartTime,
                        endTime = s.Event.EndTime,
                        genre = s.Event.Genre.ToString(),
                        imageUrl = s.Event.ImageUrl,
                        address = s.Event.Address,
                        city = s.Event.City,
                        organizerProfileId = s.Event.OrganizerProfileId,
                        organizerName = s.Event.OrganizerProfile != null
                            ? s.Event.OrganizerProfile.DisplayName
                            : (s.Event.Organizer.FirstName + " " + s.Event.Organizer.LastName).Trim(),
                    })
                    .ToListAsync()
                : new();

            // Pinned event — single highlighted card.
            var pinnedEvent = user.PinnedEventId.HasValue
                ? await _db.Events
                    .AsNoTracking()
                    .Where(e => e.Id == user.PinnedEventId.Value)
                    .Select(e => new
                    {
                        id = e.Id,
                        title = e.Title,
                        description = e.Description,
                        startTime = e.StartTime,
                        endTime = e.EndTime,
                        genre = e.Genre.ToString(),
                        imageUrl = e.ImageUrl,
                        address = e.Address,
                        city = e.City,
                        organizerProfileId = e.OrganizerProfileId,
                        organizerName = e.OrganizerProfile != null
                            ? e.OrganizerProfile.DisplayName
                            : (e.Organizer.FirstName + " " + e.Organizer.LastName).Trim(),
                    })
                    .FirstOrDefaultAsync()
                : null;

            // Compute "mutual nights" — events that BOTH the viewer (currentUserId)
            // AND the followed profile have Going attendance for.
            // Only meaningful when the viewer is a different user from the profile owner.
            var viewerForMutuals = currentUserId != null && currentUserId != user.Id ? currentUserId : null;

            var followingProfiles = await _db.Follows
                .AsNoTracking()
                .Where(f => f.FollowerId == user.Id)
                .OrderByDescending(f => f.CreatedAt)
                .Select(f => f.Following!)
                .Take(12)
                .Select(u => new
                {
                    id = u.Id,
                    userName = u.UserName,
                    displayName = ((u.FirstName ?? "") + " " + (u.LastName ?? "")).Trim() == ""
                        ? u.UserName
                        : ((u.FirstName ?? "") + " " + (u.LastName ?? "")).Trim(),
                    profileImageUrl = u.ProfileImageUrl,
                    followersCount = _db.Follows.Count(f => f.FollowingId == u.Id),
                    postsCount = _db.Posts.Count(p => p.OrganizerId == u.Id),
                    currentUserFollows = currentUserId != null
                        && _db.Follows.Any(f => f.FollowerId == currentUserId && f.FollowingId == u.Id),
                    mutualEventsCount = viewerForMutuals == null
                        ? 0
                        : _db.EventAttendances.Count(a =>
                            a.UserId == u.Id
                            && a.Status == EventAttendanceStatus.Going
                            && _db.EventAttendances.Any(a2 =>
                                a2.EventId == a.EventId
                                && a2.UserId == viewerForMutuals
                                && a2.Status == EventAttendanceStatus.Going)),
                })
                .ToListAsync();

            return Ok(new
            {
                id = user.Id,
                userName = user.UserName,
                firstName = user.FirstName,
                lastName = user.LastName,
                displayName,
                profileImageUrl = user.ProfileImageUrl,
                bio = user.Bio,
                joinedAt = user.CreatedAt,
                profileStatusText = user.ProfileStatusText,
                profileStatusEmoji = user.ProfileStatusEmoji,
                profileStatusUpdatedAt = user.ProfileStatusUpdatedAt,

                // Counts (return both camelCase variants the frontend handles)
                followerCount = followersCount,
                followersCount,
                followingCount,
                eventsAttendedCount = attendedCount,
                eventsInterestedCount = interestedCount,
                citiesVisitedCount,
                pinnedEventId = user.PinnedEventId,

                // Identity flags (return aliases so the frontend's `??` chains always hit)
                isFollowing,
                currentUserFollows = isFollowing,
                isOwnProfile = isOwn,
                isCurrentUser = isOwn,
                canStartConversation = !isOwn,
                isOrganizer,
                vibeTags,
                favouriteGenre = vibeTags.FirstOrDefault(),

                // Event payloads consumed by the rich personal profile UI.
                attendedEvents,
                goingEvents,
                plannedEvents = goingEvents,
                savedEvents,
                pinnedEvent,
                followingProfiles,

                roles,
            });
        }

        // GET /api/profiles/{id}/followers
        [HttpGet("{id}/followers")]
        public async Task<IActionResult> Followers(string id)
        {
            return await ResolveFollowList(id, kind: "followers");
        }

        // GET /api/profiles/{id}/following
        [HttpGet("{id}/following")]
        public async Task<IActionResult> Following(string id)
        {
            return await ResolveFollowList(id, kind: "following");
        }

        private async Task<IActionResult> ResolveFollowList(string id, string kind)
        {
            var currentUserId = _userManager.GetUserId(User);
            var owner = await _db.Users
                .AsNoTracking()
                .Where(u => u.Id == id)
                .Select(u => new { u.Id, u.FirstName, u.LastName, u.UserName })
                .FirstOrDefaultAsync();
            if (owner == null) return NotFound();

            IQueryable<ApplicationUser> users;
            if (kind == "followers")
            {
                // People who follow `id`.
                users = _db.Follows
                    .Where(f => f.FollowingId == id)
                    .OrderByDescending(f => f.CreatedAt)
                    .Select(f => f.Follower!);
            }
            else
            {
                // People `id` follows.
                users = _db.Follows
                    .Where(f => f.FollowerId == id)
                    .OrderByDescending(f => f.CreatedAt)
                    .Select(f => f.Following!);
            }

            var profiles = await users
                .AsNoTracking()
                .Select(u => new
                {
                    id = u.Id,
                    userName = u.UserName,
                    displayName = ((u.FirstName ?? "") + " " + (u.LastName ?? "")).Trim() == ""
                        ? u.UserName
                        : ((u.FirstName ?? "") + " " + (u.LastName ?? "")).Trim(),
                    profileImageUrl = u.ProfileImageUrl,
                    followersCount = _db.Follows.Count(f => f.FollowingId == u.Id),
                    postsCount = _db.Posts.Count(p => p.OrganizerId == u.Id),
                    currentUserFollows = currentUserId != null
                        && _db.Follows.Any(f => f.FollowerId == currentUserId && f.FollowingId == u.Id),
                })
                .Take(200)
                .ToListAsync();

            var ownerDisplay = string.Join(" ", new[] { owner.FirstName, owner.LastName }
                .Where(s => !string.IsNullOrWhiteSpace(s)));
            if (string.IsNullOrWhiteSpace(ownerDisplay)) ownerDisplay = owner.UserName ?? "Профил";

            return Ok(new
            {
                profileId = owner.Id,
                profileName = ownerDisplay,
                listTitle = kind == "followers" ? "Последователи" : "Следвани",
                profiles,
            });
        }

        // POST /api/profiles/me/pin/{eventId} — pin an event on your own profile.
        [HttpPost("me/pin/{eventId:int}")]
        [Authorize(Policy = "ApiAuth")]
        public async Task<IActionResult> PinEvent(int eventId)
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null) return Unauthorized();

            var exists = await _db.Events.AnyAsync(e => e.Id == eventId);
            if (!exists) return NotFound(new { error = "Събитието не съществува." });

            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null) return NotFound();

            user.PinnedEventId = eventId;
            await _db.SaveChangesAsync();
            return Ok(new { pinnedEventId = eventId });
        }

        // DELETE /api/profiles/me/pin — clear the pinned event.
        [HttpDelete("me/pin")]
        [Authorize(Policy = "ApiAuth")]
        public async Task<IActionResult> UnpinEvent()
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null) return Unauthorized();

            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null) return NotFound();

            user.PinnedEventId = null;
            await _db.SaveChangesAsync();
            return Ok(new { pinnedEventId = (int?)null });
        }

        // GET /api/profiles/me
        [HttpGet("me")]
        [Authorize(Policy = "ApiAuth")]
        public async Task<IActionResult> Me()
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null) return Unauthorized();
            return await Details(userId);
        }

        [HttpPost("{id}/follow")]
        [Authorize(Policy = "ApiAuth")]
        public async Task<IActionResult> Follow(string id)
        {
            var userId = _userManager.GetUserId(User)!;
            if (id == userId) return BadRequest(new { error = "Не можеш да следваш себе си." });
            if (!await _db.Users.AnyAsync(u => u.Id == id)) return NotFound();

            var exists = await _db.Follows.AnyAsync(f => f.FollowerId == userId && f.FollowingId == id);
            if (!exists)
            {
                _db.Follows.Add(new Follow { FollowerId = userId, FollowingId = id, CreatedAt = DateTime.UtcNow });
                await _db.SaveChangesAsync();
            }

            return Ok(new { isFollowing = true, followerCount = await _db.Follows.CountAsync(f => f.FollowingId == id) });
        }

        [HttpDelete("{id}/follow")]
        [Authorize(Policy = "ApiAuth")]
        public async Task<IActionResult> Unfollow(string id)
        {
            var userId = _userManager.GetUserId(User)!;
            var follow = await _db.Follows.FirstOrDefaultAsync(f => f.FollowerId == userId && f.FollowingId == id);
            if (follow != null)
            {
                _db.Follows.Remove(follow);
                await _db.SaveChangesAsync();
            }

            return Ok(new { isFollowing = false, followerCount = await _db.Follows.CountAsync(f => f.FollowingId == id) });
        }
    }
}

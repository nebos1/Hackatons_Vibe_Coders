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

            return Ok(new
            {
                id = user.Id,
                userName = user.UserName,
                firstName = user.FirstName,
                lastName = user.LastName,
                profileImageUrl = user.ProfileImageUrl,
                bio = user.Bio,
                followerCount = user.Followers.Count,
                followingCount = user.Following.Count,
                isFollowing = currentUserId != null && user.Followers.Any(f => f.FollowerId == currentUserId),
                isOwnProfile = user.Id == currentUserId,
                roles = roles,
            });
        }

        // GET /api/profiles/me
        [HttpGet("me")]
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

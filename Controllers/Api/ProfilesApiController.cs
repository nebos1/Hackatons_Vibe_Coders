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

            var users = await _db.Users
                .AsNoTracking()
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

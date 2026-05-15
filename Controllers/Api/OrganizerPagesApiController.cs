using EventsApp.Data;
using EventsApp.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EventsApp.Controllers.Api
{
    [ApiController]
    [Route("api/organizer-pages")]
    [IgnoreAntiforgeryToken]
    public class OrganizerPagesApiController : ControllerBase
    {
        private readonly ApplicationDbContext _db;

        public OrganizerPagesApiController(ApplicationDbContext db)
        {
            _db = db;
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> Details(int id)
        {
            var profile = await _db.OrganizerProfiles
                .AsNoTracking()
                .Where(p => p.Id == id && p.IsActive && p.IsApproved)
                .Include(p => p.Owner)
                .Include(p => p.BusinessWorkspace)
                .FirstOrDefaultAsync();
            if (profile == null) return NotFound();

            var events = await _db.Events
                .AsNoTracking()
                .Where(e => e.OrganizerProfileId == id && e.IsApproved && e.StartTime >= DateTime.UtcNow)
                .OrderBy(e => e.StartTime)
                .Take(12)
                .Select(e => new
                {
                    id = e.Id,
                    title = e.Title,
                    city = e.City,
                    address = e.Address,
                    startTime = e.StartTime,
                    endTime = e.EndTime,
                    genre = e.Genre.ToString(),
                    imageUrl = e.ImageUrl,
                })
                .ToListAsync();

            var posts = await _db.Posts
                .AsNoTracking()
                .Where(p => p.OrganizerProfileId == id)
                .OrderByDescending(p => p.CreatedAt)
                .Take(8)
                .Select(p => new
                {
                    id = p.Id,
                    content = p.Content,
                    createdAt = p.CreatedAt,
                    mediaUrl = p.Images.Select(i => i.ImageUrl).FirstOrDefault(),
                    mediaType = p.Images.Select(i => i.MediaType.ToString()).FirstOrDefault(),
                    likesCount = p.Likes.Count,
                    commentsCount = p.Comments.Count,
                })
                .ToListAsync();

            var followerCount = await _db.Follows.CountAsync(f => f.FollowingId == profile.OwnerId);
            var totalPostsCount = await _db.Posts.CountAsync(p => p.OrganizerProfileId == id);
            var livePastEventsCount = await _db.Events.CountAsync(e => e.OrganizerProfileId == id && e.EndTime < DateTime.UtcNow);

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
                profile.ContactEmail,
                profile.InstagramUrl,
                profile.FacebookUrl,
                profile.TikTokUrl,
                profile.BrandColor,
                createdAt = profile.CreatedAt,
                pastEventsCount = profile.PastEventsCount + livePastEventsCount,
                followerCount,
                totalPostsCount,
                workspaceName = profile.BusinessWorkspace != null && profile.ShowLegalBusinessNamePublicly ? profile.BusinessWorkspace.DisplayName : null,
                ownerId = profile.OwnerId,
                events,
                posts,
            });
        }
    }
}

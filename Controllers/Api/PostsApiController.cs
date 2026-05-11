using EventsApp.Data;
using EventsApp.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EventsApp.Controllers.Api
{
    [ApiController]
    [Route("api/posts")]
    [IgnoreAntiforgeryToken]
    public class PostsApiController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public PostsApiController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        // GET /api/posts
        [HttpGet]
        public async Task<IActionResult> List(
            [FromQuery] string? q,
            [FromQuery] string? filter,
            [FromQuery] string? sort,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 12)
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 48);
            var userId = _userManager.GetUserId(User);

            var query = _db.Posts
                .AsNoTracking()
                .Include(p => p.Organizer)
                .Include(p => p.OrganizerProfile)
                .Include(p => p.Images)
                .Include(p => p.Likes)
                .Include(p => p.Saves)
                .Include(p => p.Comments)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(q))
                query = query.Where(p => p.Content.Contains(q) || p.Organizer.UserName!.Contains(q));

            if (filter == "media")
                query = query.Where(p => p.Images.Any());

            if (filter == "saved" && userId != null)
                query = query.Where(p => p.Saves.Any(s => s.UserId == userId));

            if (filter == "following" && userId != null)
            {
                var following = await _db.Follows.Where(f => f.FollowerId == userId).Select(f => f.FollowingId).ToListAsync();
                query = query.Where(p => following.Contains(p.OrganizerId));
            }

            query = sort switch
            {
                "popular" => query.OrderByDescending(p => p.Likes.Count + p.Comments.Count),
                "discussed" => query.OrderByDescending(p => p.Comments.Count),
                _ => query.OrderByDescending(p => p.CreatedAt),
            };

            var total = await query.CountAsync();
            var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

            return Ok(new
            {
                items = items.Select(p => MapToCard(p, userId)),
                totalCount = total,
                page,
                pageSize,
                hasMore = total > page * pageSize,
            });
        }

        // GET /api/posts/{id}
        [HttpGet("{id:int}")]
        public async Task<IActionResult> Details(int id)
        {
            var userId = _userManager.GetUserId(User);
            var isAdmin = User.IsInRole("Admin");

            var post = await _db.Posts
                .AsNoTracking()
                .Include(p => p.Organizer)
                .Include(p => p.OrganizerProfile)
                .Include(p => p.Images)
                .Include(p => p.Likes)
                .Include(p => p.Saves)
                .Include(p => p.Comments)
                    .ThenInclude(c => c.User)
                .Include(p => p.Comments)
                    .ThenInclude(c => c.Likes)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (post == null) return NotFound();

            var rootComments = post.Comments
                .Where(c => c.ParentCommentId == null)
                .OrderByDescending(c => c.Likes.Count)
                .ThenByDescending(c => c.CreatedAt)
                .Take(20)
                .ToList();

            var rootIds = rootComments.Select(c => c.Id).ToHashSet();
            var repliesByParent = post.Comments
                .Where(c => c.ParentCommentId.HasValue && rootIds.Contains(c.ParentCommentId.Value))
                .GroupBy(c => c.ParentCommentId!.Value)
                .ToDictionary(g => g.Key, g => g.ToList());

            return Ok(new
            {
                id = post.Id,
                authorId = post.OrganizerId,
                authorName = post.OrganizerProfile?.DisplayName ?? post.Organizer?.UserName ?? string.Empty,
                authorImageUrl = post.OrganizerProfile?.AvatarImageUrl ?? post.Organizer?.ProfileImageUrl,
                content = post.Content,
                mediaType = post.Images.FirstOrDefault()?.MediaType.ToString(),
                mediaUrl = post.Images.FirstOrDefault()?.ImageUrl,
                createdAt = post.CreatedAt,
                likesCount = post.Likes.Count,
                savesCount = post.Saves.Count,
                commentsCount = post.Comments.Count,
                isLiked = userId != null && post.Likes.Any(l => l.UserId == userId),
                isSaved = userId != null && post.Saves.Any(s => s.UserId == userId),
                canEdit = isAdmin || post.OrganizerId == userId,
                canDelete = isAdmin || post.OrganizerId == userId,
                comments = rootComments.Select(c => new
                {
                    id = c.Id,
                    userId = c.UserId,
                    userName = c.User?.UserName ?? string.Empty,
                    content = c.Content,
                    createdAt = c.CreatedAt,
                    likesCount = c.Likes.Count,
                    currentUserLiked = userId != null && c.Likes.Any(l => l.UserId == userId),
                    canDelete = isAdmin || c.UserId == userId,
                    replies = (IEnumerable<object>)(repliesByParent.TryGetValue(c.Id, out var reps)
                        ? reps.Select(r => (object)new
                        {
                            id = r.Id,
                            userId = r.UserId,
                            userName = r.User?.UserName ?? string.Empty,
                            content = r.Content,
                            createdAt = r.CreatedAt,
                            likesCount = r.Likes.Count,
                            currentUserLiked = userId != null && r.Likes.Any(l => l.UserId == userId),
                            canDelete = isAdmin || r.UserId == userId,
                            replies = Array.Empty<object>(),
                        })
                        : Array.Empty<object>()),
                }),
            });
        }

        // POST /api/posts
        [HttpPost]
        [Authorize(Policy = "ApiAuth")]
        public async Task<IActionResult> Create([FromBody] CreatePostDto dto)
        {
            var userId = _userManager.GetUserId(User)!;
            var post = new Post { OrganizerId = userId, Content = dto.Content };
            _db.Posts.Add(post);
            await _db.SaveChangesAsync();
            return Ok(MapToCard(post, userId));
        }

        [HttpPut("{id:int}")]
        [Authorize(Policy = "ApiAuth")]
        public async Task<IActionResult> Update(int id, [FromBody] CreatePostDto dto)
        {
            var userId = _userManager.GetUserId(User)!;
            var post = await _db.Posts.FirstOrDefaultAsync(p => p.Id == id);
            if (post == null) return NotFound();
            if (post.OrganizerId != userId && !User.IsInRole("Admin")) return Forbid();
            if (string.IsNullOrWhiteSpace(dto.Content)) return BadRequest(new { error = "Публикацията не може да е празна." });

            post.Content = dto.Content.Trim();
            await _db.SaveChangesAsync();
            return Ok(new { id = post.Id, content = post.Content });
        }

        [HttpDelete("{id:int}")]
        [Authorize(Policy = "ApiAuth")]
        public async Task<IActionResult> Delete(int id)
        {
            var userId = _userManager.GetUserId(User)!;
            var post = await _db.Posts
                .Include(p => p.Images)
                .Include(p => p.Comments)
                .Include(p => p.Likes)
                .Include(p => p.Saves)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (post == null) return NotFound();
            if (post.OrganizerId != userId && !User.IsInRole("Admin")) return Forbid();

            _db.Posts.Remove(post);
            await _db.SaveChangesAsync();
            return Ok(new { deleted = true });
        }

        // POST /api/posts/{id}/like
        [HttpPost("{id:int}/like")]
        [Authorize(Policy = "ApiAuth")]
        public async Task<IActionResult> Like(int id)
        {
            var userId = _userManager.GetUserId(User)!;
            var post = await _db.Posts.Include(p => p.Likes).FirstOrDefaultAsync(p => p.Id == id);
            if (post == null) return NotFound();
            if (!post.Likes.Any(l => l.UserId == userId))
            {
                post.Likes.Add(new PostLike { UserId = userId, PostId = id });
                await _db.SaveChangesAsync();
            }
            return Ok(new { likesCount = post.Likes.Count });
        }

        // POST /api/posts/{id}/unlike
        [HttpPost("{id:int}/unlike")]
        [Authorize(Policy = "ApiAuth")]
        public async Task<IActionResult> Unlike(int id)
        {
            var userId = _userManager.GetUserId(User)!;
            var like = await _db.PostLikes.FirstOrDefaultAsync(l => l.PostId == id && l.UserId == userId);
            if (like != null)
            {
                _db.PostLikes.Remove(like);
                await _db.SaveChangesAsync();
            }
            var count = await _db.PostLikes.CountAsync(l => l.PostId == id);
            return Ok(new { likesCount = count });
        }

        // POST /api/posts/{id}/save
        [HttpPost("{id:int}/save")]
        [Authorize(Policy = "ApiAuth")]
        public async Task<IActionResult> Save(int id)
        {
            var userId = _userManager.GetUserId(User)!;
            var post = await _db.Posts.Include(p => p.Saves).FirstOrDefaultAsync(p => p.Id == id);
            if (post == null) return NotFound();
            if (!post.Saves.Any(s => s.UserId == userId))
            {
                post.Saves.Add(new PostSave { UserId = userId, PostId = id });
                await _db.SaveChangesAsync();
            }
            return Ok(new { savesCount = post.Saves.Count });
        }

        // POST /api/posts/{id}/unsave
        [HttpPost("{id:int}/unsave")]
        [Authorize(Policy = "ApiAuth")]
        public async Task<IActionResult> Unsave(int id)
        {
            var userId = _userManager.GetUserId(User)!;
            var save = await _db.PostSaves.FirstOrDefaultAsync(s => s.PostId == id && s.UserId == userId);
            if (save != null)
            {
                _db.PostSaves.Remove(save);
                await _db.SaveChangesAsync();
            }
            var count = await _db.PostSaves.CountAsync(s => s.PostId == id);
            return Ok(new { savesCount = count });
        }

        private static object MapToCard(Post p, string? userId) => new
        {
            id = p.Id,
            authorId = p.OrganizerId,
            authorName = p.OrganizerProfile?.DisplayName ?? p.Organizer?.UserName ?? string.Empty,
            authorImageUrl = p.OrganizerProfile?.AvatarImageUrl ?? p.Organizer?.ProfileImageUrl,
            content = p.Content,
            mediaType = p.Images.FirstOrDefault()?.MediaType.ToString(),
            mediaUrl = p.Images.FirstOrDefault()?.ImageUrl,
            createdAt = p.CreatedAt,
            likesCount = p.Likes.Count,
            savesCount = p.Saves.Count,
            commentsCount = p.Comments.Count,
            isLiked = userId != null && p.Likes.Any(l => l.UserId == userId),
            isSaved = userId != null && p.Saves.Any(s => s.UserId == userId),
            canEdit = p.OrganizerId == userId,
            canDelete = p.OrganizerId == userId,
        };
    }
}

public record CreatePostDto(string Content);

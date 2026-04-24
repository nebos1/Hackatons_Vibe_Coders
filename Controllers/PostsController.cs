using EventsApp.Common;
using EventsApp.Data;
using EventsApp.Models;
using EventsApp.ViewModels.Posts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace EventsApp.Controllers
{
    public class PostsController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public PostsController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var posts = await _db.Posts
                .AsNoTracking()
                .OrderByDescending(p => p.CreatedAt)
                .Select(p => new PostCardViewModel
                {
                    Id = p.Id,
                    OrganizerId = p.OrganizerId,
                    OrganizerName = p.Organizer.UserName ?? string.Empty,
                    Content = p.Content,
                    CreatedAt = p.CreatedAt,
                    EventId = p.EventId,
                    EventTitle = p.Event != null ? p.Event.Title : null,
                    FirstImageUrl = p.Images.Select(i => i.ImageUrl).FirstOrDefault(),
                    LikesCount = p.Likes.Count,
                    CommentsCount = p.Comments.Count,
                })
                .ToListAsync();

            return View(posts);
        }

        public async Task<IActionResult> Details(int id)
        {
            var userId = _userManager.GetUserId(User);
            var isAdmin = User.IsInRole(GlobalConstants.Roles.Admin);

            var post = await _db.Posts
                .AsNoTracking()
                .Include(p => p.Organizer)
                .Include(p => p.Event)
                .Include(p => p.Images)
                .Include(p => p.Likes)
                .Include(p => p.Comments)
                    .ThenInclude(c => c.User)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (post == null) return NotFound();

            return View(new PostDetailsViewModel
            {
                Id = post.Id,
                OrganizerId = post.OrganizerId,
                OrganizerName = post.Organizer?.UserName ?? string.Empty,
                Content = post.Content,
                CreatedAt = post.CreatedAt,
                UpdatedAt = post.UpdatedAt,
                EventId = post.EventId,
                EventTitle = post.Event?.Title,
                ImageUrls = post.Images.Select(i => i.ImageUrl).ToList(),
                LikesCount = post.Likes.Count,
                CurrentUserLiked = userId != null && post.Likes.Any(l => l.UserId == userId),
                Comments = post.Comments
                    .OrderByDescending(c => c.CreatedAt)
                    .Select(c => new PostCommentViewModel
                    {
                        Id = c.Id,
                        UserId = c.UserId,
                        UserName = c.User?.UserName ?? string.Empty,
                        Content = c.Content,
                        CreatedAt = c.CreatedAt,
                        CanDelete = isAdmin || c.UserId == userId,
                    })
                    .ToList(),
                CanEdit = isAdmin || post.OrganizerId == userId,
                CanDelete = isAdmin || post.OrganizerId == userId,
            });
        }

        [Authorize(Roles = GlobalConstants.Roles.Admin + "," + GlobalConstants.Roles.Organizer)]
        public async Task<IActionResult> Create()
        {
            return View(new PostCreateEditViewModel
            {
                Events = await GetEventOptionsAsync(),
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = GlobalConstants.Roles.Admin + "," + GlobalConstants.Roles.Organizer)]
        public async Task<IActionResult> Create(PostCreateEditViewModel input)
        {
            if (!ModelState.IsValid)
            {
                input.Events = await GetEventOptionsAsync();
                return View(input);
            }

            var userId = _userManager.GetUserId(User)!;

            var post = new Post
            {
                OrganizerId = userId,
                Content = input.Content,
                EventId = input.EventId,
            };

            _db.Posts.Add(post);
            await _db.SaveChangesAsync();

            if (!string.IsNullOrWhiteSpace(input.ImageUrl))
            {
                _db.PostImages.Add(new PostImage { PostId = post.Id, ImageUrl = input.ImageUrl });
                await _db.SaveChangesAsync();
            }

            TempData["StatusMessage"] = "Post created.";
            return RedirectToAction(nameof(Details), new { id = post.Id });
        }

        [Authorize(Roles = GlobalConstants.Roles.Admin + "," + GlobalConstants.Roles.Organizer)]
        public async Task<IActionResult> Edit(int id)
        {
            var userId = _userManager.GetUserId(User)!;
            var isAdmin = User.IsInRole(GlobalConstants.Roles.Admin);

            var post = await _db.Posts
                .Include(p => p.Images)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (post == null) return NotFound();
            if (!isAdmin && post.OrganizerId != userId) return Forbid();

            return View(new PostCreateEditViewModel
            {
                Id = post.Id,
                Content = post.Content,
                EventId = post.EventId,
                ImageUrl = post.Images.Select(i => i.ImageUrl).FirstOrDefault(),
                Events = await GetEventOptionsAsync(),
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = GlobalConstants.Roles.Admin + "," + GlobalConstants.Roles.Organizer)]
        public async Task<IActionResult> Edit(int id, PostCreateEditViewModel input)
        {
            var userId = _userManager.GetUserId(User)!;
            var isAdmin = User.IsInRole(GlobalConstants.Roles.Admin);

            var post = await _db.Posts
                .Include(p => p.Images)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (post == null) return NotFound();
            if (!isAdmin && post.OrganizerId != userId) return Forbid();

            if (!ModelState.IsValid)
            {
                input.Events = await GetEventOptionsAsync();
                return View(input);
            }

            post.Content = input.Content;
            post.EventId = input.EventId;
            post.UpdatedAt = DateTime.UtcNow;

            if (!string.IsNullOrWhiteSpace(input.ImageUrl))
            {
                var existing = post.Images.FirstOrDefault();
                if (existing != null)
                    existing.ImageUrl = input.ImageUrl;
                else
                    _db.PostImages.Add(new PostImage { PostId = post.Id, ImageUrl = input.ImageUrl });
            }

            await _db.SaveChangesAsync();
            TempData["StatusMessage"] = "Post updated.";
            return RedirectToAction(nameof(Details), new { id = post.Id });
        }

        [Authorize(Roles = GlobalConstants.Roles.Admin + "," + GlobalConstants.Roles.Organizer)]
        public async Task<IActionResult> Delete(int id)
        {
            var userId = _userManager.GetUserId(User)!;
            var isAdmin = User.IsInRole(GlobalConstants.Roles.Admin);

            var post = await _db.Posts.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
            if (post == null) return NotFound();
            if (!isAdmin && post.OrganizerId != userId) return Forbid();

            return View(post);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = GlobalConstants.Roles.Admin + "," + GlobalConstants.Roles.Organizer)]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var userId = _userManager.GetUserId(User)!;
            var isAdmin = User.IsInRole(GlobalConstants.Roles.Admin);

            var post = await _db.Posts.FirstOrDefaultAsync(p => p.Id == id);
            if (post == null) return NotFound();
            if (!isAdmin && post.OrganizerId != userId) return Forbid();

            _db.Posts.Remove(post);
            await _db.SaveChangesAsync();
            TempData["StatusMessage"] = "Post deleted.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> Like(int id)
        {
            var userId = _userManager.GetUserId(User)!;
            var exists = await _db.PostLikes.AnyAsync(l => l.PostId == id && l.UserId == userId);
            if (!exists && await _db.Posts.AnyAsync(p => p.Id == id))
            {
                _db.PostLikes.Add(new PostLike { PostId = id, UserId = userId });
                await _db.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> Unlike(int id)
        {
            var userId = _userManager.GetUserId(User)!;
            var like = await _db.PostLikes.FirstOrDefaultAsync(l => l.PostId == id && l.UserId == userId);
            if (like != null)
            {
                _db.PostLikes.Remove(like);
                await _db.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> AddComment(int id, string content)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                TempData["StatusMessage"] = "Comment cannot be empty.";
                return RedirectToAction(nameof(Details), new { id });
            }

            content = content.Trim();
            if (content.Length > GlobalConstants.Comment.ContentMaxLength)
                content = content[..GlobalConstants.Comment.ContentMaxLength];

            if (!await _db.Posts.AnyAsync(p => p.Id == id))
                return NotFound();

            var userId = _userManager.GetUserId(User)!;
            _db.PostComments.Add(new PostComment
            {
                PostId = id,
                UserId = userId,
                Content = content,
            });
            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> DeleteComment(int commentId)
        {
            var userId = _userManager.GetUserId(User)!;
            var isAdmin = User.IsInRole(GlobalConstants.Roles.Admin);

            var comment = await _db.PostComments.FirstOrDefaultAsync(c => c.Id == commentId);
            if (comment == null) return NotFound();
            if (!isAdmin && comment.UserId != userId) return Forbid();

            var postId = comment.PostId;
            _db.PostComments.Remove(comment);
            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Details), new { id = postId });
        }

        private async Task<IEnumerable<SelectListItem>> GetEventOptionsAsync()
        {
            var userId = _userManager.GetUserId(User);
            var isAdmin = User.IsInRole(GlobalConstants.Roles.Admin);

            var query = _db.Events.AsNoTracking();
            if (!isAdmin && userId != null)
                query = query.Where(e => e.OrganizerId == userId);

            return await query
                .OrderByDescending(e => e.StartTime)
                .Select(e => new SelectListItem
                {
                    Value = e.Id.ToString(),
                    Text = e.Title,
                })
                .ToListAsync();
        }
    }
}

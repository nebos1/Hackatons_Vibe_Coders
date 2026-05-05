using EventsApp.Common;
using EventsApp.Data;
using EventsApp.Models;
using EventsApp.Services;
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
        private const long UploadSizeLimit = 100L * 1024 * 1024;

        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IMediaUploadService _mediaUpload;
        private readonly ISocialFeedService _socialFeed;
        private readonly IPlatformPermissionService _permissions;
        private readonly IBusinessContextService _businessContext;
        private readonly IActingIdentityService _actingIdentity;

        public PostsController(
            ApplicationDbContext db,
            UserManager<ApplicationUser> userManager,
            IMediaUploadService mediaUpload,
            ISocialFeedService socialFeed,
            IPlatformPermissionService permissions,
            IBusinessContextService businessContext,
            IActingIdentityService actingIdentity)
        {
            _db = db;
            _userManager = userManager;
            _mediaUpload = mediaUpload;
            _socialFeed = socialFeed;
            _permissions = permissions;
            _businessContext = businessContext;
            _actingIdentity = actingIdentity;
        }

        public async Task<IActionResult> Index(string? q)
        {
            var userId = _userManager.GetUserId(User);
            var feed = await _socialFeed.BuildFeedAsync(userId, q);
            return View(feed);
        }

        public async Task<IActionResult> Details(int id)
        {
            var userId = _userManager.GetUserId(User);
            var isAdmin = User.IsInRole(GlobalConstants.Roles.Admin);

            var post = await _db.Posts
                .AsNoTracking()
                .Include(p => p.Organizer)
                    .ThenInclude(o => o.OrganizerData)
                .Include(p => p.OrganizerProfile)
                .Include(p => p.Event)
                .Include(p => p.Images)
                .Include(p => p.Likes)
                .Include(p => p.Saves)
                .Include(p => p.Comments)
                    .ThenInclude(c => c.User)
                .Include(p => p.Comments)
                    .ThenInclude(c => c.AuthorOrganizerProfile)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (post == null) return NotFound();

            var comments = post.Comments.ToList();
            var repliesByParentId = comments
                .Where(c => c.ParentCommentId.HasValue)
                .GroupBy(c => c.ParentCommentId!.Value)
                .ToDictionary(g => g.Key, g => g.OrderBy(r => r.CreatedAt).ToList());

            return View(new PostDetailsViewModel
            {
                Id = post.Id,
                OrganizerId = post.OrganizerId,
                OrganizerProfileId = post.OrganizerProfileId,
                OrganizerName = !string.IsNullOrWhiteSpace(post.OrganizerProfile?.DisplayName)
                    ? post.OrganizerProfile.DisplayName
                    : post.Organizer?.OrganizerData?.Approved == true
                    ? post.Organizer.OrganizerData.OrganizationName
                    : post.Organizer?.UserName ?? string.Empty,
                Content = post.Content,
                CreatedAt = post.CreatedAt,
                UpdatedAt = post.UpdatedAt,
                EventId = post.EventId,
                EventTitle = post.Event?.Title,
                OrganizerImageUrl = post.OrganizerProfile?.AvatarImageUrl ?? post.Organizer?.ProfileImageUrl,
                OrganizerIsOrganizer = post.Organizer?.OrganizerData?.Approved ?? false,
                Media = post.Images
                    .Select(i => new PostMediaItemViewModel { Url = i.ImageUrl, MediaType = i.MediaType })
                    .ToList(),
                LikesCount = post.Likes.Count,
                SavesCount = post.Saves.Count,
                CommentsCount = comments.Count,
                CurrentUserLiked = userId != null && post.Likes.Any(l => l.UserId == userId),
                CurrentUserSaved = userId != null && post.Saves.Any(s => s.UserId == userId),
                Comments = comments
                    .Where(c => c.ParentCommentId == null)
                    .OrderByDescending(c => c.CreatedAt)
                    .Select(c => ToCommentViewModel(c, userId, isAdmin, repliesByParentId))
                    .ToList(),
                CanEdit = isAdmin || post.OrganizerId == userId,
                CanDelete = isAdmin || post.OrganizerId == userId,
                ActingIdentities = User.Identity?.IsAuthenticated == true
                    ? await _actingIdentity.GetOptionsAsync(HttpContext, post.OrganizerProfileId)
                    : Array.Empty<ViewModels.Social.ActingIdentityOptionViewModel>(),
            });
        }

        [Authorize]
        public async Task<IActionResult> Create()
        {
            if (!await _permissions.CanCreatePostAsync(User))
            {
                TempData["StatusMessage"] = "Only approved organizers can create public posts.";
                return Forbid();
            }

            var context = await _businessContext.GetContextAsync(HttpContext);
            if (context.Page == null || context.Workspace == null)
            {
                TempData["StatusMessage"] = "Избери активна organizer page преди да публикуваш.";
                return RedirectToAction("Dashboard", "Organizer");
            }

            return View(new PostCreateEditViewModel
            {
                Events = await GetEventOptionsAsync(),
                ActiveWorkspaceName = context.Workspace.DisplayName,
                ActivePageName = context.Page.DisplayName,
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(UploadSizeLimit)]
        [Authorize]
        public async Task<IActionResult> Create(PostCreateEditViewModel input)
        {
            if (!await _permissions.CanCreatePostAsync(User))
            {
                TempData["StatusMessage"] = "Only approved organizers can create public posts.";
                return Forbid();
            }

            var context = await _businessContext.GetContextAsync(HttpContext);
            if (context.Page == null || context.Workspace == null)
            {
                ModelState.AddModelError(string.Empty, "Избери активна organizer page преди да публикуваш.");
            }
            else if (!await _permissions.CanPublishAsIdentityAsync(User, AuthorIdentityType.OrganizerPage, context.Page.Id))
            {
                return Forbid();
            }

            if (!ModelState.IsValid)
            {
                input.Events = await GetEventOptionsAsync();
                input.ActiveWorkspaceName = context.Workspace?.DisplayName;
                input.ActivePageName = context.Page?.DisplayName;
                return View(input);
            }

            var userId = _userManager.GetUserId(User)!;

            var post = new Post
            {
                OrganizerId = userId,
                OrganizerProfileId = context.Page!.Id,
                BusinessWorkspaceId = context.Workspace!.Id,
                Content = input.Content,
                EventId = input.EventId,
            };

            _db.Posts.Add(post);
            await _db.SaveChangesAsync();

            var media = await ResolveMediaAsync(input);
            if (media != null)
            {
                _db.PostImages.Add(new PostImage
                {
                    PostId = post.Id,
                    ImageUrl = media.Url,
                    MediaType = media.MediaType,
                });
                await _db.SaveChangesAsync();
            }

            TempData["StatusMessage"] = "Post created.";
            return RedirectToAction(nameof(Details), new { id = post.Id });
        }

        [Authorize]
        public async Task<IActionResult> Edit(int id)
        {
            var userId = _userManager.GetUserId(User)!;
            var isAdmin = User.IsInRole(GlobalConstants.Roles.Admin);

            var post = await _db.Posts
                .Include(p => p.Images)
                .Include(p => p.OrganizerProfile)
                .Include(p => p.BusinessWorkspace)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (post == null) return NotFound();
            if (!isAdmin && post.OrganizerId != userId) return Forbid();

            return View(new PostCreateEditViewModel
            {
                Id = post.Id,
                Content = post.Content,
                EventId = post.EventId,
                CurrentMediaUrl = post.Images.Select(i => i.ImageUrl).FirstOrDefault(),
                Events = await GetEventOptionsAsync(),
                ActiveWorkspaceName = post.BusinessWorkspace?.DisplayName,
                ActivePageName = post.OrganizerProfile?.DisplayName,
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(UploadSizeLimit)]
        [Authorize]
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
                input.CurrentMediaUrl = post.Images.Select(i => i.ImageUrl).FirstOrDefault();
                return View(input);
            }

            post.Content = input.Content;
            post.EventId = input.EventId;
            post.UpdatedAt = DateTime.UtcNow;

            var media = await ResolveMediaAsync(input);
            if (media != null)
            {
                var existing = post.Images.FirstOrDefault();
                if (existing != null)
                {
                    existing.ImageUrl = media.Url;
                    existing.MediaType = media.MediaType;
                }
                else
                {
                    _db.PostImages.Add(new PostImage
                    {
                        PostId = post.Id,
                        ImageUrl = media.Url,
                        MediaType = media.MediaType,
                    });
                }
            }

            await _db.SaveChangesAsync();
            TempData["StatusMessage"] = "Post updated.";
            return RedirectToAction(nameof(Details), new { id = post.Id });
        }

        [Authorize]
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
        [Authorize]
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
        public async Task<IActionResult> Like(int id, string? returnUrl)
        {
            var userId = _userManager.GetUserId(User)!;
            var exists = await _db.PostLikes.AnyAsync(l => l.PostId == id && l.UserId == userId);
            if (!exists && await _db.Posts.AnyAsync(p => p.Id == id))
            {
                _db.PostLikes.Add(new PostLike { PostId = id, UserId = userId });
                await _db.SaveChangesAsync();
                await _socialFeed.TrackActivityAsync(userId, UserActivityType.PostLiked, postId: id);
            }
            return SafeRedirect(returnUrl) ?? RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> Unlike(int id, string? returnUrl)
        {
            var userId = _userManager.GetUserId(User)!;
            var like = await _db.PostLikes.FirstOrDefaultAsync(l => l.PostId == id && l.UserId == userId);
            if (like != null)
            {
                _db.PostLikes.Remove(like);
                await _db.SaveChangesAsync();
            }
            return SafeRedirect(returnUrl) ?? RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> Save(int id, string? returnUrl)
        {
            var userId = _userManager.GetUserId(User)!;
            var exists = await _db.PostSaves.AnyAsync(s => s.PostId == id && s.UserId == userId);
            if (!exists && await _db.Posts.AnyAsync(p => p.Id == id))
            {
                _db.PostSaves.Add(new PostSave { PostId = id, UserId = userId });
                await _db.SaveChangesAsync();
                await _socialFeed.TrackActivityAsync(userId, UserActivityType.PostSaved, postId: id);
            }

            return SafeRedirect(returnUrl) ?? RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> Unsave(int id, string? returnUrl)
        {
            var userId = _userManager.GetUserId(User)!;
            var save = await _db.PostSaves.FirstOrDefaultAsync(s => s.PostId == id && s.UserId == userId);
            if (save != null)
            {
                _db.PostSaves.Remove(save);
                await _db.SaveChangesAsync();
            }

            return SafeRedirect(returnUrl) ?? RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> AddComment(int id, string content, string? actingIdentityKey, int? parentCommentId)
        {
            if (!await _permissions.CanCommentAsync(User))
            {
                return Forbid();
            }

            if (string.IsNullOrWhiteSpace(content))
            {
                TempData["StatusMessage"] = "Comment cannot be empty.";
                return RedirectToAction(nameof(Details), new { id });
            }

            content = content.Trim();
            if (content.Length > GlobalConstants.Comment.ContentMaxLength)
                content = content[..GlobalConstants.Comment.ContentMaxLength];

            var post = await _db.Posts.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
            if (post == null)
                return NotFound();

            if (parentCommentId.HasValue)
            {
                var parentIsValid = await _db.PostComments
                    .AsNoTracking()
                    .AnyAsync(c => c.Id == parentCommentId.Value && c.PostId == id && c.ParentCommentId == null);
                if (!parentIsValid)
                {
                    TempData["StatusMessage"] = "Reply target was not found.";
                    return RedirectToAction(nameof(Details), new { id });
                }
            }

            var userId = _userManager.GetUserId(User)!;
            var identity = await _actingIdentity.ResolveAsync(HttpContext, actingIdentityKey, post.OrganizerProfileId);
            if (identity == null || !await _permissions.CanCommentAsIdentityAsync(User, identity.Type, identity.OrganizerProfileId))
            {
                return Forbid();
            }

            _db.PostComments.Add(new PostComment
            {
                PostId = id,
                UserId = userId,
                AuthorType = identity.Type,
                AuthorOrganizerProfileId = identity.OrganizerProfileId,
                BusinessWorkspaceId = identity.BusinessWorkspaceId,
                ParentCommentId = parentCommentId,
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
            var replies = await _db.PostComments
                .Where(c => c.ParentCommentId == comment.Id)
                .ToListAsync();
            if (replies.Count > 0)
            {
                _db.PostComments.RemoveRange(replies);
            }
            _db.PostComments.Remove(comment);
            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Details), new { id = postId });
        }

        private async Task<MediaUploadResult?> ResolveMediaAsync(PostCreateEditViewModel input)
        {
            if (input.MediaFile != null && input.MediaFile.Length > 0)
            {
                try
                {
                    return await _mediaUpload.SaveAsync(input.MediaFile, "posts");
                }
                catch (InvalidOperationException ex)
                {
                    ModelState.AddModelError(nameof(input.MediaFile), ex.Message);
                    return null;
                }
            }

            return null;
        }

        private static bool LooksLikeVideoUrl(string url)
        {
            var path = url.Split('?', '#')[0];
            var ext = Path.GetExtension(path);
            return ext.Equals(".mp4", StringComparison.OrdinalIgnoreCase)
                || ext.Equals(".webm", StringComparison.OrdinalIgnoreCase)
                || ext.Equals(".mov", StringComparison.OrdinalIgnoreCase)
                || ext.Equals(".m4v", StringComparison.OrdinalIgnoreCase);
        }

        private IActionResult? SafeRedirect(string? returnUrl)
        {
            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            return null;
        }

        private async Task<IEnumerable<SelectListItem>> GetEventOptionsAsync()
        {
            var userId = _userManager.GetUserId(User);
            var isAdmin = User.IsInRole(GlobalConstants.Roles.Admin);

            var query = _db.Events.AsNoTracking();
            if (!isAdmin && User.IsInRole(GlobalConstants.Roles.Organizer) && userId != null)
                query = query.Where(e => e.OrganizerId == userId);
            else if (!isAdmin)
                query = query.Where(e => e.IsApproved);

            return await query
                .OrderByDescending(e => e.StartTime)
                .Select(e => new SelectListItem
                {
                    Value = e.Id.ToString(),
                    Text = e.Title,
                })
                .ToListAsync();
        }

        private static string GetCommentDisplayName(PostComment comment)
        {
            return comment.AuthorType == AuthorIdentityType.OrganizerPage && comment.AuthorOrganizerProfile != null
                ? comment.AuthorOrganizerProfile.DisplayName
                : comment.User?.UserName ?? string.Empty;
        }

        private static PostCommentViewModel ToCommentViewModel(
            PostComment comment,
            string? currentUserId,
            bool isAdmin,
            IReadOnlyDictionary<int, List<PostComment>> repliesByParentId)
        {
            return new PostCommentViewModel
            {
                Id = comment.Id,
                UserId = comment.UserId,
                UserName = GetCommentDisplayName(comment),
                AuthorImageUrl = comment.AuthorType == AuthorIdentityType.OrganizerPage
                    ? comment.AuthorOrganizerProfile?.AvatarImageUrl
                    : comment.User?.ProfileImageUrl,
                AuthorBadgeKey = GetAuthorBadgeKey(comment.AuthorType),
                AuthorBadgeText = GetAuthorBadgeText(comment.AuthorType, comment.UserId == currentUserId),
                AuthorProfileUserId = comment.AuthorType == AuthorIdentityType.OrganizerPage ? null : comment.UserId,
                IsOrganizerPageAuthor = comment.AuthorType == AuthorIdentityType.OrganizerPage,
                Content = comment.Content,
                CreatedAt = comment.CreatedAt,
                CanDelete = isAdmin || comment.UserId == currentUserId,
                Replies = repliesByParentId.TryGetValue(comment.Id, out var replies)
                    ? replies.Select(r => ToCommentViewModel(r, currentUserId, isAdmin, new Dictionary<int, List<PostComment>>())).ToList()
                    : Array.Empty<PostCommentViewModel>(),
            };
        }

        private static string GetAuthorBadgeKey(AuthorIdentityType type)
        {
            return type switch
            {
                AuthorIdentityType.OrganizerPage => "identity.page",
                AuthorIdentityType.Admin => "identity.admin",
                AuthorIdentityType.System => "identity.system",
                _ => "identity.user",
            };
        }

        private static string GetAuthorBadgeText(AuthorIdentityType type, bool isYou)
        {
            if (isYou)
            {
                return "You";
            }

            return type switch
            {
                AuthorIdentityType.OrganizerPage => "Organizer Page",
                AuthorIdentityType.Admin => "Admin",
                AuthorIdentityType.System => "System",
                _ => "User",
            };
        }
    }
}

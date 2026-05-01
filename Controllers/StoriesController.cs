using EventsApp.Data;
using EventsApp.Models;
using EventsApp.Services;
using EventsApp.ViewModels.Social;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EventsApp.Controllers
{
    [Authorize]
    public class StoriesController : Controller
    {
        private const long UploadSizeLimit = 100L * 1024 * 1024;

        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IMediaUploadService _mediaUpload;

        public StoriesController(
            ApplicationDbContext db,
            UserManager<ApplicationUser> userManager,
            IMediaUploadService mediaUpload)
        {
            _db = db;
            _userManager = userManager;
            _mediaUpload = mediaUpload;
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View(new StoryCreateViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(UploadSizeLimit)]
        public async Task<IActionResult> Create(StoryCreateViewModel input, string? returnUrl)
        {
            MediaUploadResult? media = null;

            if (input.MediaFile != null && input.MediaFile.Length > 0)
            {
                try
                {
                    media = await _mediaUpload.SaveAsync(input.MediaFile, "stories");
                }
                catch (InvalidOperationException ex)
                {
                    ModelState.AddModelError(nameof(input.MediaFile), ex.Message);
                }
            }
            else
            {
                ModelState.AddModelError(nameof(input.MediaFile), "Upload an image or video file.");
            }

            if (!ModelState.IsValid || media == null)
            {
                return View(input);
            }

            var now = DateTime.UtcNow;
            _db.Stories.Add(new Story
            {
                AuthorId = _userManager.GetUserId(User)!,
                MediaUrl = media.Url,
                MediaType = media.MediaType,
                Caption = string.IsNullOrWhiteSpace(input.Caption) ? null : input.Caption.Trim(),
                CreatedAt = now,
                ExpiresAt = now.AddHours(24),
            });

            await _db.SaveChangesAsync();
            TempData["StatusMessage"] = "Story published for 24 hours.";

            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction("Index", "Posts");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id, string? returnUrl)
        {
            var userId = _userManager.GetUserId(User)!;
            var story = await _db.Stories.FirstOrDefaultAsync(s => s.Id == id);
            if (story == null)
            {
                return NotFound();
            }

            if (story.AuthorId != userId && !User.IsInRole(Common.GlobalConstants.Roles.Admin))
            {
                return Forbid();
            }

            _db.Stories.Remove(story);
            await _db.SaveChangesAsync();

            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction("Index", "Posts");
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
    }
}

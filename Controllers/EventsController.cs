using EventsApp.Common;
using EventsApp.Data;
using EventsApp.Models;
using EventsApp.ViewModels.Events;
using EventsApp.Services;
using EventsApp.Services.AI;
using EventsApp.Services.Geocoding;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EventsApp.Controllers
{
    public class EventsController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IMediaUploadService _mediaUploadService;
        private readonly IAiSearchService _ai;
        private readonly IGeocodingService _geocoder;

        public EventsController(
            ApplicationDbContext db,
            UserManager<ApplicationUser> userManager,
            IMediaUploadService mediaUploadService,
            IAiSearchService ai,
            IGeocodingService geocoder)
        {
            _db = db;
            _userManager = userManager;
            _mediaUploadService = mediaUploadService;
            _ai = ai;
            _geocoder = geocoder;
        }

        public class GenerateDescriptionRequest
        {
            public string Title { get; set; } = string.Empty;
            public string? City { get; set; }
            public string? Genre { get; set; }
            public string? Hints { get; set; }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = GlobalConstants.Roles.Admin + "," + GlobalConstants.Roles.Organizer)]
        public async Task<IActionResult> GenerateDescription([FromBody] GenerateDescriptionRequest req, CancellationToken cancellationToken)
        {
            if (req == null || string.IsNullOrWhiteSpace(req.Title))
            {
                return Json(new { ok = false, error = "Please enter a title first - the AI uses it as the seed." });
            }

            if (!_ai.IsEnabled)
            {
                return Json(new { ok = false, error = "AI is not configured. Set AI:ApiKey." });
            }

            var description = await _ai.GenerateEventDescriptionAsync(req.Title, req.City, req.Genre, req.Hints, cancellationToken);

            if (string.IsNullOrWhiteSpace(description))
            {
                var detail = _ai.LastStatusDetail ?? "AI returned no text.";
                return Json(new { ok = false, error = detail });
            }

            return Json(new { ok = true, description });
        }

        public IActionResult Index(string? search, string? city, EventGenre? genre, DateTime? dateFrom)
        {
            return RedirectToAction("Index", "Home", new { search, city, genre, dateFrom });
        }

        public async Task<IActionResult> Details(int id)
        {
            var userId = _userManager.GetUserId(User);
            var isAdmin = User.IsInRole(GlobalConstants.Roles.Admin);

            var ev = await _db.Events
                .AsNoTracking()
                .Include(e => e.Organizer)
                .Include(e => e.Images)
                .Include(e => e.Likes)
                .Include(e => e.Tickets)
                .Include(e => e.Comments)
                    .ThenInclude(c => c.User)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (ev == null)
            {
                return NotFound();
            }

            if (!ev.IsApproved && !isAdmin && ev.OrganizerId != userId)
            {
                return NotFound();
            }

            var vm = new EventDetailsViewModel
            {
                Id = ev.Id,
                Title = ev.Title,
                Description = ev.Description,
                StartTime = ev.StartTime,
                EndTime = ev.EndTime,
                Genre = ev.Genre,
                ImageUrl = ev.ImageUrl,
                IsApproved = ev.IsApproved,
                Address = ev.Address,
                City = ev.City,
                Latitude = ev.Latitude,
                Longitude = ev.Longitude,
                OrganizerId = ev.OrganizerId,
                OrganizerName = ev.Organizer.UserName ?? string.Empty,
                ImageUrls = ev.Images.Select(i => i.ImageUrl).ToList(),
                LikesCount = ev.Likes.Count,
                CurrentUserLiked = userId != null && ev.Likes.Any(l => l.UserId == userId),
                Comments = ev.Comments
                    .OrderByDescending(c => c.CreatedAt)
                    .Select(c => new EventCommentViewModel
                    {
                        Id = c.Id,
                        UserId = c.UserId,
                        UserName = c.User.UserName ?? string.Empty,
                        Content = c.Content,
                        CreatedAt = c.CreatedAt,
                        CanDelete = isAdmin || c.UserId == userId,
                    })
                    .ToList(),
                CanEdit = isAdmin || ev.OrganizerId == userId,
                CanDelete = isAdmin || ev.OrganizerId == userId,
                CanManageTickets = isAdmin || ev.OrganizerId == userId,
                Tickets = ev.Tickets
                    .Where(t => t.IsActive)
                    .OrderBy(t => t.Price)
                    .Select(t => new EventsApp.ViewModels.Tickets.EventTicketOptionViewModel
                    {
                        Id = t.Id,
                        Name = t.Name,
                        Description = t.Description,
                        Price = t.Price,
                        QuantityRemaining = t.QuantityRemaining,
                        IsActive = t.IsActive,
                    })
                    .ToList(),
            };

            return View(vm);
        }

        [Authorize(Roles = GlobalConstants.Roles.Admin + "," + GlobalConstants.Roles.Organizer)]
        public IActionResult Create()
        {
            var vm = new EventCreateEditViewModel
            {
                CanEditApproval = User.IsInRole(GlobalConstants.Roles.Admin),
            };
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = GlobalConstants.Roles.Admin + "," + GlobalConstants.Roles.Organizer)]
        public async Task<IActionResult> Create(EventCreateEditViewModel input)
        {
            var userId = _userManager.GetUserId(User)!;
            var isAdmin = User.IsInRole(GlobalConstants.Roles.Admin);

            if (input.EndTime <= input.StartTime)
            {
                ModelState.AddModelError(nameof(input.EndTime), "End time must be after start time.");
            }

            if (!ModelState.IsValid)
            {
                input.CanEditApproval = isAdmin;
                return View(input);
            }

            var ev = new Event
            {
                Title = input.Title,
                Description = input.Description,
                City = input.City,
                Address = input.Address,
                OrganizerId = userId,
                StartTime = input.StartTime,
                EndTime = input.EndTime,
                Genre = input.Genre,
                ImageUrl = input.ImageUrl, // fallback to URL if no upload
                Latitude = input.Latitude,
                Longitude = input.Longitude,
                IsApproved = isAdmin && input.IsApproved,
            };

            if (!ev.Latitude.HasValue || !ev.Longitude.HasValue)
            {
                var geo = await _geocoder.GeocodeAsync(ev.Address, ev.City);
                if (geo != null)
                {
                    ev.Latitude = geo.Latitude;
                    ev.Longitude = geo.Longitude;
                }
                else if (CityCoordinates.TryGetCoordinates(ev.City, out var seedLat, out var seedLng))
                {
                    ev.Latitude = seedLat;
                    ev.Longitude = seedLng;
                }
            }

            _db.Events.Add(ev);
            await _db.SaveChangesAsync();

            // Handle photo upload
            if (input.Photo != null && input.Photo.Length > 0)
            {
                var uploadResult = await _mediaUploadService.SaveAsync(input.Photo, "events");
                if (uploadResult != null)
                {
                    // Save as main image if not set
                    ev.ImageUrl = uploadResult.Url;
                    // Optionally, add to Images collection
                    _db.EventImages.Add(new EventImage { EventId = ev.Id, ImageUrl = uploadResult.Url });
                    await _db.SaveChangesAsync();
                }
            }

            TempData["StatusMessage"] = "Event created.";
            return RedirectToAction(nameof(Details), new { id = ev.Id });
        }

        [Authorize(Roles = GlobalConstants.Roles.Admin + "," + GlobalConstants.Roles.Organizer)]
        public async Task<IActionResult> Edit(int id)
        {
            var userId = _userManager.GetUserId(User)!;
            var isAdmin = User.IsInRole(GlobalConstants.Roles.Admin);

            var ev = await _db.Events.FirstOrDefaultAsync(e => e.Id == id);
            if (ev == null)
            {
                return NotFound();
            }

            if (!isAdmin && ev.OrganizerId != userId)
            {
                return Forbid();
            }

            var vm = new EventCreateEditViewModel
            {
                Id = ev.Id,
                Title = ev.Title,
                Description = ev.Description,
                City = ev.City,
                Address = ev.Address,
                StartTime = ev.StartTime,
                EndTime = ev.EndTime,
                Genre = ev.Genre,
                ImageUrl = ev.ImageUrl,
                Latitude = ev.Latitude,
                Longitude = ev.Longitude,
                IsApproved = ev.IsApproved,
                CanEditApproval = isAdmin,
            };
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = GlobalConstants.Roles.Admin + "," + GlobalConstants.Roles.Organizer)]
        public async Task<IActionResult> Edit(int id, EventCreateEditViewModel input)
        {
            var userId = _userManager.GetUserId(User)!;
            var isAdmin = User.IsInRole(GlobalConstants.Roles.Admin);

            var ev = await _db.Events.FirstOrDefaultAsync(e => e.Id == id);
            if (ev == null)
            {
                return NotFound();
            }

            if (!isAdmin && ev.OrganizerId != userId)
            {
                return Forbid();
            }

            if (input.EndTime <= input.StartTime)
            {
                ModelState.AddModelError(nameof(input.EndTime), "End time must be after start time.");
            }

            if (!ModelState.IsValid)
            {
                input.CanEditApproval = isAdmin;
                return View(input);
            }

            var addressChanged = !string.Equals(ev.Address, input.Address, StringComparison.Ordinal)
                                 || !string.Equals(ev.City, input.City, StringComparison.Ordinal);

            ev.Title = input.Title;
            ev.Description = input.Description;
            ev.City = input.City;
            ev.Address = input.Address;
            ev.StartTime = input.StartTime;
            ev.EndTime = input.EndTime;
            ev.Genre = input.Genre;
            ev.Latitude = input.Latitude;
            ev.Longitude = input.Longitude;

            if ((!ev.Latitude.HasValue || !ev.Longitude.HasValue) || addressChanged && (input.Latitude == null || input.Longitude == null))
            {
                if (!ev.Latitude.HasValue || !ev.Longitude.HasValue)
                {
                    var geo = await _geocoder.GeocodeAsync(ev.Address, ev.City);
                    if (geo != null)
                    {
                        ev.Latitude = geo.Latitude;
                        ev.Longitude = geo.Longitude;
                    }
                    else if (CityCoordinates.TryGetCoordinates(ev.City, out var lat, out var lng))
                    {
                        ev.Latitude = lat;
                        ev.Longitude = lng;
                    }
                }
            }
            if (input.Photo != null && input.Photo.Length > 0)
            {
                var uploadResult = await _mediaUploadService.SaveAsync(input.Photo, "events");
                if (uploadResult != null)
                {
                    ev.ImageUrl = uploadResult.Url;
                    _db.EventImages.Add(new EventImage { EventId = ev.Id, ImageUrl = uploadResult.Url });
                }
            }
            else if (!string.IsNullOrWhiteSpace(input.ImageUrl))
            {
                ev.ImageUrl = input.ImageUrl;
            }

            if (isAdmin)
            {
                ev.IsApproved = input.IsApproved;
            }

            await _db.SaveChangesAsync();
            TempData["StatusMessage"] = "Event updated.";
            return RedirectToAction(nameof(Details), new { id = ev.Id });
        }

        [Authorize(Roles = GlobalConstants.Roles.Admin + "," + GlobalConstants.Roles.Organizer)]
        public async Task<IActionResult> Delete(int id)
        {
            var userId = _userManager.GetUserId(User)!;
            var isAdmin = User.IsInRole(GlobalConstants.Roles.Admin);

            var ev = await _db.Events
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == id);
            if (ev == null)
            {
                return NotFound();
            }

            if (!isAdmin && ev.OrganizerId != userId)
            {
                return Forbid();
            }

            return View(ev);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = GlobalConstants.Roles.Admin + "," + GlobalConstants.Roles.Organizer)]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var userId = _userManager.GetUserId(User)!;
            var isAdmin = User.IsInRole(GlobalConstants.Roles.Admin);

            var ev = await _db.Events.FirstOrDefaultAsync(e => e.Id == id);
            if (ev == null)
            {
                return NotFound();
            }

            if (!isAdmin && ev.OrganizerId != userId)
            {
                return Forbid();
            }

            _db.Events.Remove(ev);
            await _db.SaveChangesAsync();
            TempData["StatusMessage"] = "Event deleted.";
            return RedirectToAction("Index", "Home");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> Like(int id, string? returnUrl)
        {
            var userId = _userManager.GetUserId(User)!;
            var exists = await _db.EventLikes.AnyAsync(l => l.EventId == id && l.UserId == userId);
            if (!exists && await _db.Events.AnyAsync(e => e.Id == id))
            {
                _db.EventLikes.Add(new EventLike { EventId = id, UserId = userId });
                await _db.SaveChangesAsync();
            }
            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> Unlike(int id, string? returnUrl)
        {
            var userId = _userManager.GetUserId(User)!;
            var like = await _db.EventLikes.FirstOrDefaultAsync(l => l.EventId == id && l.UserId == userId);
            if (like != null)
            {
                _db.EventLikes.Remove(like);
                await _db.SaveChangesAsync();
            }
            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
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
            {
                content = content[..GlobalConstants.Comment.ContentMaxLength];
            }

            if (!await _db.Events.AnyAsync(e => e.Id == id))
            {
                return NotFound();
            }

            var userId = _userManager.GetUserId(User)!;
            _db.EventComments.Add(new EventComment
            {
                EventId = id,
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

            var comment = await _db.EventComments.FirstOrDefaultAsync(c => c.Id == commentId);
            if (comment == null)
            {
                return NotFound();
            }

            if (!isAdmin && comment.UserId != userId)
            {
                return Forbid();
            }

            var eventId = comment.EventId;
            _db.EventComments.Remove(comment);
            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Details), new { id = eventId });
        }

        [Authorize]
        public async Task<IActionResult> Recommended()
        {
            var userId = _userManager.GetUserId(User)!;
            var prefs = await _db.UserPreferences.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == userId);

            var now = DateTime.UtcNow;
            var query = _db.Events
                .AsNoTracking()
                .Where(e => e.IsApproved && e.StartTime >= now);

            if (prefs != null)
            {
                if (prefs.PreferredGenre.HasValue)
                {
                    query = query.Where(e => e.Genre == prefs.PreferredGenre.Value);
                }

                if (!string.IsNullOrWhiteSpace(prefs.PreferredCity))
                {
                    query = query.Where(e => e.City == prefs.PreferredCity);
                }
            }

            var events = await query
                .OrderBy(e => e.StartTime)
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
                    OrganizerName = e.Organizer.UserName ?? string.Empty,
                    LikesCount = e.Likes.Count,
                    CommentsCount = e.Comments.Count,
                    CurrentUserLiked = e.Likes.Any(l => l.UserId == userId),
                })
                .ToListAsync();

            ViewBag.HasPreferences = prefs != null;
            return View(events);
        }
    }
}

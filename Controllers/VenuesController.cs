using EventsApp.Common;
using EventsApp.Data;
using EventsApp.Models;
using EventsApp.ViewModels.Events;
using EventsApp.ViewModels.Venues;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace EventsApp.Controllers
{
    public class VenuesController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public VenuesController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index(string? search, string? city)
        {
            var query = _db.Venues.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(v => v.Name.Contains(search));

            if (!string.IsNullOrWhiteSpace(city))
                query = query.Where(v => v.City == city);

            var venues = await query
                .OrderBy(v => v.Name)
                .Select(v => new VenueCardViewModel
                {
                    Id = v.Id,
                    Name = v.Name,
                    Address = v.Address,
                    City = v.City,
                    ImageUrl = v.ImageUrl,
                    EventsCount = v.Events.Count,
                })
                .ToListAsync();

            var cities = await _db.Venues
                .AsNoTracking()
                .Select(v => v.City)
                .Distinct()
                .OrderBy(c => c)
                .ToListAsync();

            return View(new VenuesIndexViewModel
            {
                Search = search,
                City = city,
                Venues = venues,
                Cities = cities,
            });
        }

        public async Task<IActionResult> Details(int id)
        {
            var userId = _userManager.GetUserId(User);
            var isAdmin = User.IsInRole(GlobalConstants.Roles.Admin);

            var venue = await _db.Venues
                .AsNoTracking()
                .Include(v => v.Organizer)
                .Include(v => v.Events)
                    .ThenInclude(e => e.Likes)
                .Include(v => v.Events)
                    .ThenInclude(e => e.Comments)
                .FirstOrDefaultAsync(v => v.Id == id);

            if (venue == null) return NotFound();

            var events = venue.Events
                .Where(e => e.IsApproved || isAdmin || e.OrganizerId == userId)
                .OrderBy(e => e.StartTime)
                .Select(e => new EventCardViewModel
                {
                    Id = e.Id,
                    Title = e.Title,
                    ImageUrl = e.ImageUrl,
                    VenueName = venue.Name,
                    VenueCity = venue.City,
                    StartTime = e.StartTime,
                    Genre = e.Genre,
                    IsApproved = e.IsApproved,
                    OrganizerId = e.OrganizerId,
                    OrganizerName = e.Organizer?.UserName ?? string.Empty,
                    LikesCount = e.Likes.Count,
                    CommentsCount = e.Comments.Count,
                })
                .ToList();

            return View(new VenueDetailsViewModel
            {
                Id = venue.Id,
                Name = venue.Name,
                Description = venue.Description,
                Address = venue.Address,
                City = venue.City,
                ImageUrl = venue.ImageUrl,
                OrganizerId = venue.OrganizerId,
                OrganizerName = venue.Organizer?.UserName ?? string.Empty,
                Events = events,
                CanEdit = isAdmin || venue.OrganizerId == userId,
                CanDelete = isAdmin || venue.OrganizerId == userId,
            });
        }

        [Authorize(Roles = GlobalConstants.Roles.Admin + "," + GlobalConstants.Roles.Organizer)]
        public async Task<IActionResult> Create()
        {
            var vm = new VenueCreateEditViewModel
            {
                CanPickOrganizer = User.IsInRole(GlobalConstants.Roles.Admin),
                Organizers = User.IsInRole(GlobalConstants.Roles.Admin)
                    ? await GetOrganizerOptionsAsync()
                    : Array.Empty<SelectListItem>(),
            };
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = GlobalConstants.Roles.Admin + "," + GlobalConstants.Roles.Organizer)]
        public async Task<IActionResult> Create(VenueCreateEditViewModel input)
        {
            var userId = _userManager.GetUserId(User)!;
            var isAdmin = User.IsInRole(GlobalConstants.Roles.Admin);

            if (!ModelState.IsValid)
            {
                input.CanPickOrganizer = isAdmin;
                input.Organizers = isAdmin ? await GetOrganizerOptionsAsync() : Array.Empty<SelectListItem>();
                return View(input);
            }

            var venue = new Venue
            {
                Name = input.Name,
                Description = input.Description,
                Address = input.Address,
                City = input.City,
                ImageUrl = input.ImageUrl,
                OrganizerId = isAdmin && !string.IsNullOrEmpty(input.OrganizerId) ? input.OrganizerId : userId,
            };

            _db.Venues.Add(venue);
            await _db.SaveChangesAsync();
            TempData["StatusMessage"] = "Venue created.";
            return RedirectToAction(nameof(Details), new { id = venue.Id });
        }

        [Authorize(Roles = GlobalConstants.Roles.Admin + "," + GlobalConstants.Roles.Organizer)]
        public async Task<IActionResult> Edit(int id)
        {
            var userId = _userManager.GetUserId(User)!;
            var isAdmin = User.IsInRole(GlobalConstants.Roles.Admin);

            var venue = await _db.Venues.FirstOrDefaultAsync(v => v.Id == id);
            if (venue == null) return NotFound();
            if (!isAdmin && venue.OrganizerId != userId) return Forbid();

            return View(new VenueCreateEditViewModel
            {
                Id = venue.Id,
                Name = venue.Name,
                Description = venue.Description,
                Address = venue.Address,
                City = venue.City,
                ImageUrl = venue.ImageUrl,
                OrganizerId = venue.OrganizerId,
                CanPickOrganizer = isAdmin,
                Organizers = isAdmin ? await GetOrganizerOptionsAsync() : Array.Empty<SelectListItem>(),
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = GlobalConstants.Roles.Admin + "," + GlobalConstants.Roles.Organizer)]
        public async Task<IActionResult> Edit(int id, VenueCreateEditViewModel input)
        {
            var userId = _userManager.GetUserId(User)!;
            var isAdmin = User.IsInRole(GlobalConstants.Roles.Admin);

            var venue = await _db.Venues.FirstOrDefaultAsync(v => v.Id == id);
            if (venue == null) return NotFound();
            if (!isAdmin && venue.OrganizerId != userId) return Forbid();

            if (!ModelState.IsValid)
            {
                input.CanPickOrganizer = isAdmin;
                input.Organizers = isAdmin ? await GetOrganizerOptionsAsync() : Array.Empty<SelectListItem>();
                return View(input);
            }

            venue.Name = input.Name;
            venue.Description = input.Description;
            venue.Address = input.Address;
            venue.City = input.City;
            venue.ImageUrl = input.ImageUrl;
            if (isAdmin && !string.IsNullOrEmpty(input.OrganizerId))
                venue.OrganizerId = input.OrganizerId;

            await _db.SaveChangesAsync();
            TempData["StatusMessage"] = "Venue updated.";
            return RedirectToAction(nameof(Details), new { id = venue.Id });
        }

        [Authorize(Roles = GlobalConstants.Roles.Admin + "," + GlobalConstants.Roles.Organizer)]
        public async Task<IActionResult> Delete(int id)
        {
            var userId = _userManager.GetUserId(User)!;
            var isAdmin = User.IsInRole(GlobalConstants.Roles.Admin);

            var venue = await _db.Venues.AsNoTracking().FirstOrDefaultAsync(v => v.Id == id);
            if (venue == null) return NotFound();
            if (!isAdmin && venue.OrganizerId != userId) return Forbid();

            return View(venue);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = GlobalConstants.Roles.Admin + "," + GlobalConstants.Roles.Organizer)]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var userId = _userManager.GetUserId(User)!;
            var isAdmin = User.IsInRole(GlobalConstants.Roles.Admin);

            var venue = await _db.Venues.FirstOrDefaultAsync(v => v.Id == id);
            if (venue == null) return NotFound();
            if (!isAdmin && venue.OrganizerId != userId) return Forbid();

            _db.Venues.Remove(venue);
            await _db.SaveChangesAsync();
            TempData["StatusMessage"] = "Venue deleted.";
            return RedirectToAction(nameof(Index));
        }

        private async Task<IEnumerable<SelectListItem>> GetOrganizerOptionsAsync()
        {
            return await _db.OrganizerData
                .AsNoTracking()
                .Select(o => new SelectListItem
                {
                    Value = o.OrganizerId,
                    Text = o.OrganizationName + " (" + o.Organizer.UserName + ")",
                })
                .ToListAsync();
        }
    }
}

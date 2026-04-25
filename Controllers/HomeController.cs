using System.Diagnostics;
using EventsApp.Common;
using EventsApp.Data;
using EventsApp.Models;
using EventsApp.ViewModels.Events;
using EventsApp.ViewModels.Home;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EventsApp.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<HomeController> _logger;

        public HomeController(
            ApplicationDbContext db,
            UserManager<ApplicationUser> userManager,
            ILogger<HomeController> logger)
        {
            _db = db;
            _userManager = userManager;
            _logger = logger;
        }

        public async Task<IActionResult> Index(string? search, string? city, EventGenre? genre, DateTime? dateFrom)
        {
            var isAdmin = User.IsInRole(GlobalConstants.Roles.Admin);
            var userId = _userManager.GetUserId(User);

            var query = _db.Events.AsNoTracking().AsQueryable();
            if (!isAdmin)
            {
                query = query.Where(e => e.IsApproved);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(e => e.Title.Contains(search));
            }

            if (!string.IsNullOrWhiteSpace(city))
            {
                query = query.Where(e => e.City == city);
            }

            if (genre.HasValue)
            {
                query = query.Where(e => e.Genre == genre.Value);
            }

            if (dateFrom.HasValue)
            {
                var from = dateFrom.Value.Date;
                query = query.Where(e => e.StartTime >= from);
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
                    CurrentUserLiked = userId != null && e.Likes.Any(l => l.UserId == userId),
                })
                .ToListAsync();

            var cityEventCounts = await query
                .GroupBy(e => e.City)
                .Select(g => new
                {
                    City = g.Key,
                    EventCount = g.Count(),
                })
                .ToListAsync();

            var markers = cityEventCounts
                .Where(e => CityCoordinates.TryGetCoordinates(e.City, out _, out _))
                .Select(e =>
                {
                    CityCoordinates.TryGetCoordinates(e.City, out var lat, out var lng);
                    return new EventMapMarkerViewModel
                    {
                        City = e.City,
                        EventCount = e.EventCount,
                        Lat = lat,
                        Lng = lng,
                    };
                })
                .OrderBy(m => m.City)
                .ToList();

            var cities = await _db.Events
                .AsNoTracking()
                .Select(e => e.City)
                .Distinct()
                .OrderBy(c => c)
                .ToListAsync();

            return View(new EventsIndexViewModel
            {
                Search = search,
                City = city,
                Genre = genre,
                DateFrom = dateFrom,
                Events = events,
                MapMarkers = markers,
                Cities = cities,
            });
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel
            {
                RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier,
            });
        }
    }
}

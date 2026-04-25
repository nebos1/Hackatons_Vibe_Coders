using System.Diagnostics;
using EventsApp.Common;
using EventsApp.Data;
using EventsApp.Models;
using EventsApp.ViewModels.Events;
using EventsApp.ViewModels.Home;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace EventsApp.Controllers
{
    public class HomeController : Controller
    {
        private const int LatestCount = 12;
        private const int MapMarkerCount = 30;

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
            var now = DateTime.UtcNow;
            var userId = _userManager.GetUserId(User);

            var query = _db.Events
                .AsNoTracking()
                .Where(e => e.IsApproved && e.StartTime >= now);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(e => e.Title.Contains(term));
            }

            if (!string.IsNullOrWhiteSpace(city))
            {
                query = query.Where(e => e.Venue.City == city);
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
                .Take(LatestCount)
                .Select(e => new EventCardViewModel
                {
                    Id = e.Id,
                    Title = e.Title,
                    ImageUrl = e.ImageUrl,
                    VenueName = e.Venue.Name,
                    VenueCity = e.Venue.City,
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

            var upcomingForMap = await query
                .OrderBy(e => e.StartTime)
                .Take(MapMarkerCount)
                .Select(e => new
                {
                    e.Id,
                    e.Title,
                    e.ImageUrl,
                    VenueName = e.Venue.Name,
                    VenueCity = e.Venue.City,
                    e.StartTime,
                })
                .ToListAsync();

            var markers = upcomingForMap
                .Where(e => CityCoordinates.TryGetCoordinates(e.VenueCity, out _, out _))
                .Select(e =>
                {
                    CityCoordinates.TryGetCoordinates(e.VenueCity, out var lat, out var lng);
                    return new EventMapMarkerViewModel
                    {
                        EventId = e.Id,
                        Title = e.Title,
                        VenueName = e.VenueName,
                        City = e.VenueCity,
                        StartTime = e.StartTime,
                        Lat = lat,
                        Lng = lng,
                        ImageUrl = e.ImageUrl,
                    };
                })
                .ToList();

            var cities = await _db.Venues
                .AsNoTracking()
                .Select(v => v.City)
                .Distinct()
                .OrderBy(c => c)
                .Select(c => new SelectListItem { Value = c, Text = c })
                .ToListAsync();

            return View(new HomeIndexViewModel
            {
                Search = search,
                City = city,
                Genre = genre,
                DateFrom = dateFrom,
                LatestEvents = events,
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

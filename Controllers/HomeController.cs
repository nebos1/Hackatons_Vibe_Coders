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
                query = query.Where(e =>
                    e.Title.Contains(search) ||
                    (e.Description != null && e.Description.Contains(search)) ||
                    e.City.Contains(search) ||
                    e.Address.Contains(search));
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
                    Latitude = e.Latitude,
                    Longitude = e.Longitude,
                })
                .ToListAsync();

            var markers = BuildMarkers(events);

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

        private static IReadOnlyList<EventMapMarkerViewModel> BuildMarkers(IReadOnlyList<EventCardViewModel> events)
        {
            var byCity = events
                .Where(e => !e.Latitude.HasValue || !e.Longitude.HasValue)
                .GroupBy(e => e.City, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Select(e => e.Id).ToList(), StringComparer.OrdinalIgnoreCase);

            var result = new List<EventMapMarkerViewModel>(events.Count);

            foreach (var ev in events)
            {
                double lat, lng;
                bool approx = false;

                if (ev.Latitude.HasValue && ev.Longitude.HasValue)
                {
                    lat = ev.Latitude.Value;
                    lng = ev.Longitude.Value;
                }
                else if (CityCoordinates.TryGetCoordinates(ev.City, out var clat, out var clng))
                {
                    var idx = byCity.TryGetValue(ev.City, out var ids) ? ids.IndexOf(ev.Id) : 0;
                    var (offLat, offLng) = JitterOffset(idx);
                    lat = clat + offLat;
                    lng = clng + offLng;
                    approx = true;
                }
                else
                {
                    continue;
                }

                result.Add(new EventMapMarkerViewModel
                {
                    EventId = ev.Id,
                    Title = ev.Title,
                    City = ev.City,
                    Address = ev.Address,
                    StartTime = ev.StartTime,
                    Genre = ev.Genre,
                    ImageUrl = ev.ImageUrl,
                    OrganizerName = ev.OrganizerName,
                    Lat = lat,
                    Lng = lng,
                    IsApproximate = approx,
                });
            }

            return result;
        }

        private static (double dLat, double dLng) JitterOffset(int index)
        {
            if (index <= 0) return (0, 0);
            const double step = 0.0035;
            var ring = (int)Math.Ceiling(Math.Sqrt(index));
            var angle = (index * 137.508) * Math.PI / 180.0;
            return (Math.Sin(angle) * step * ring, Math.Cos(angle) * step * ring);
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

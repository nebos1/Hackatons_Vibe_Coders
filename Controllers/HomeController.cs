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

        public async Task<IActionResult> Index(string? search, string? city, EventGenre? genre, DateTime? dateFrom, DateTime? dateTo)
        {
            var isAdmin = User.IsInRole(GlobalConstants.Roles.Admin);
            var userId = _userManager.GetUserId(User);

            var query = _db.Events.AsNoTracking().AsQueryable();
            if (!isAdmin)
            {
                query = query.Where(e => e.IsApproved);
            }

            var normalizedSearch = search?.Trim();
            var freeOnly = string.Equals(normalizedSearch, "free", StringComparison.OrdinalIgnoreCase);

            if (!string.IsNullOrWhiteSpace(normalizedSearch) && !freeOnly)
            {
                query = query.Where(e =>
                    e.Title.Contains(normalizedSearch) ||
                    (e.Description != null && e.Description.Contains(normalizedSearch)) ||
                    e.City.Contains(normalizedSearch) ||
                    e.Address.Contains(normalizedSearch));
            }

            if (freeOnly)
            {
                query = query.Where(e => !e.Tickets.Any(t => t.IsActive && t.Price > 0m));
            }

            if (!string.IsNullOrWhiteSpace(city))
            {
                var cityVariants = CityCoordinates.GetEquivalentNames(city);
                query = query.Where(e => cityVariants.Contains(e.City));
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

            if (dateTo.HasValue)
            {
                var to = dateTo.Value.Date.AddDays(1);
                query = query.Where(e => e.StartTime < to);
            }

            var events = await QueryEventCards(query.OrderBy(e => e.StartTime), userId)
                .ToListAsync();

            var markers = BuildMarkers(events);
            var now = DateTime.UtcNow;
            var tonightEnd = now.Date.AddDays(1);
            var weekendStart = GetWeekendStart(now);
            var weekendEnd = weekendStart.AddDays(2);

            var discoveryBase = _db.Events.AsNoTracking().Where(e => isAdmin || e.IsApproved);
            var upcomingBase = discoveryBase.Where(e => e.StartTime >= now);

            var tonightEvents = await QueryEventCards(upcomingBase
                    .Where(e => e.StartTime >= now && e.StartTime < tonightEnd)
                    .OrderBy(e => e.StartTime)
                    .Take(6),
                userId)
                .ToListAsync();

            var weekendEvents = await QueryEventCards(upcomingBase
                    .Where(e => e.StartTime >= weekendStart && e.StartTime < weekendEnd)
                    .OrderBy(e => e.StartTime)
                    .Take(6),
                userId)
                .ToListAsync();

            var trending = await QueryEventCards(upcomingBase
                    .OrderByDescending(e => e.Attendances.Count * 2 + e.Likes.Count + e.Saves.Count + e.Comments.Count)
                    .ThenBy(e => e.StartTime)
                    .Take(6),
                userId)
                .ToListAsync();

            var popularOrganizers = await _db.OrganizerProfiles
                .AsNoTracking()
                .Where(p => p.IsActive && p.IsApproved)
                .OrderByDescending(p => p.Events.Count(e => e.IsApproved && e.StartTime >= now))
                .ThenByDescending(p => p.Owner.Followers.Count)
                .Take(6)
                .Select(p => new PopularOrganizerViewModel
                {
                    Id = p.Id,
                    OwnerId = p.OwnerId,
                    DisplayName = p.DisplayName,
                    City = p.City,
                    Tagline = p.Tagline,
                    AvatarImageUrl = p.AvatarImageUrl,
                    UpcomingEventsCount = p.Events.Count(e => e.IsApproved && e.StartTime >= now),
                })
                .ToListAsync();

            var popularCities = await discoveryBase
                .Where(e => e.StartTime >= now)
                .GroupBy(e => e.City)
                .OrderByDescending(g => g.Count())
                .ThenBy(g => g.Key)
                .Take(8)
                .Select(g => new PopularCityViewModel
                {
                    Name = g.Key,
                    EventsCount = g.Count(),
                })
                .ToListAsync();

            var recentlyViewed = userId == null
                ? new List<EventCardViewModel>()
                : await QueryEventCards(_db.UserActivities
                        .AsNoTracking()
                        .Where(a => a.UserId == userId && a.ActivityType == UserActivityType.EventViewed && a.EventId != null && a.Event!.IsApproved)
                        .OrderByDescending(a => a.CreatedAt)
                        .Select(a => a.Event!)
                        .Distinct()
                        .Take(6),
                    userId)
                    .ToListAsync();

            var preferredCity = userId == null
                ? null
                : await _db.UserPreferences
                    .AsNoTracking()
                    .Where(p => p.UserId == userId)
                    .Select(p => p.PreferredCity)
                    .FirstOrDefaultAsync();

            var cities = await _db.Events
                .AsNoTracking()
                .Select(e => e.City)
                .Distinct()
                .OrderBy(c => c)
                .ToListAsync();

            return View(new EventsIndexViewModel
            {
                Search = freeOnly ? "free" : normalizedSearch,
                City = city,
                Genre = genre,
                DateFrom = dateFrom,
                DateTo = dateTo,
                Events = events,
                MapMarkers = markers,
                Cities = cities,
                TonightEvents = tonightEvents,
                WeekendEvents = weekendEvents,
                TrendingEvents = trending,
                PopularOrganizers = popularOrganizers,
                PopularCities = popularCities,
                RecentlyViewedEvents = recentlyViewed,
                PreferredCity = preferredCity,
                IsAuthenticated = User.Identity?.IsAuthenticated == true,
            });
        }

        private static IQueryable<EventCardViewModel> QueryEventCards(IQueryable<Event> query, string? userId)
        {
            return query.Select(e => new EventCardViewModel
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
                OrganizerProfileId = e.OrganizerProfileId,
                OrganizerName = e.OrganizerProfile != null ? e.OrganizerProfile.DisplayName : e.Organizer.UserName ?? string.Empty,
                LikesCount = e.Likes.Count,
                CommentsCount = e.Comments.Count,
                SavesCount = e.Saves.Count,
                GoingCount = e.Attendances.Count(a => a.Status == EventAttendanceStatus.Going),
                InterestedCount = e.Attendances.Count(a => a.Status == EventAttendanceStatus.Interested),
                CurrentUserLiked = userId != null && e.Likes.Any(l => l.UserId == userId),
                CurrentUserSaved = userId != null && e.Saves.Any(s => s.UserId == userId),
                CurrentUserAttendanceStatus = userId == null
                    ? null
                    : e.Attendances
                        .Where(a => a.UserId == userId)
                        .Select(a => (EventAttendanceStatus?)a.Status)
                        .FirstOrDefault(),
                Latitude = e.Latitude,
                Longitude = e.Longitude,
                HasActiveTickets = e.Tickets.Any(t => t.IsActive),
                HasPaidTickets = e.Tickets.Any(t => t.IsActive && t.Price > 0m),
                LowestPaidTicketPrice = e.Tickets
                    .Where(t => t.IsActive && t.Price > 0m)
                    .Min(t => (decimal?)t.Price),
            });
        }

        private static DateTime GetWeekendStart(DateTime now)
        {
            var today = now.Date;
            var daysUntilSaturday = ((int)DayOfWeek.Saturday - (int)today.DayOfWeek + 7) % 7;
            return today.AddDays(daysUntilSaturday);
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

        public async Task<IActionResult> Calendar(int? year, int? month)
        {
            var isAdmin = User.IsInRole(GlobalConstants.Roles.Admin);
            var now = DateTime.UtcNow;
            var y = year ?? now.Year;
            var m = month ?? now.Month;
            if (m < 1) { m = 12; y--; }
            if (m > 12) { m = 1; y++; }

            var monthStart = new DateTime(y, m, 1);
            var monthEnd = monthStart.AddMonths(1);

            var query = _db.Events.AsNoTracking().AsQueryable();
            if (!isAdmin) query = query.Where(e => e.IsApproved);

            var events = await query
                .Where(e => e.StartTime >= monthStart && e.StartTime < monthEnd)
                .OrderBy(e => e.StartTime)
                .Select(e => new ViewModels.Home.CalendarEventViewModel
                {
                    Id = e.Id,
                    Title = e.Title,
                    City = e.City,
                    StartTime = e.StartTime,
                    Genre = e.Genre,
                })
                .ToListAsync();

            return View(new ViewModels.Home.CalendarViewModel
            {
                Year = y,
                Month = m,
                Events = events,
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

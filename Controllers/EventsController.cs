using EventsApp.Common;
using EventsApp.Data;
using EventsApp.Models;
using EventsApp.ViewModels.Events;
using EventsApp.ViewModels.Layouts;
using EventsApp.Services;
using EventsApp.Services.AI;
using EventsApp.Services.Geocoding;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Hosting;
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
        private readonly ISocialFeedService _socialFeed;
        private readonly IRecurringEventService _recurringEvents;
        private readonly ILayoutService _layouts;
        private readonly IPlatformPermissionService _permissions;
        private readonly IBusinessContextService _businessContext;
        private readonly IActingIdentityService _actingIdentity;

        public EventsController(
            ApplicationDbContext db,
            UserManager<ApplicationUser> userManager,
            IMediaUploadService mediaUploadService,
            IAiSearchService ai,
            IGeocodingService geocoder,
            ISocialFeedService socialFeed,
            IRecurringEventService recurringEvents,
            ILayoutService layouts,
            IPlatformPermissionService permissions,
            IBusinessContextService businessContext,
            IActingIdentityService actingIdentity)
        {
            _db = db;
            _userManager = userManager;
            _mediaUploadService = mediaUploadService;
            _ai = ai;
            _geocoder = geocoder;
            _socialFeed = socialFeed;
            _recurringEvents = recurringEvents;
            _layouts = layouts;
            _permissions = permissions;
            _businessContext = businessContext;
            _actingIdentity = actingIdentity;
        }

        public class GenerateDescriptionRequest
        {
            public string Title { get; set; } = string.Empty;
            public string? City { get; set; }
            public string? Genre { get; set; }
            public string? Hints { get; set; }
            public string? Lang { get; set; }
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

            var description = await _ai.GenerateEventDescriptionAsync(req.Title, req.City, req.Genre, req.Hints, req.Lang, cancellationToken);

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

        public async Task<IActionResult> Details(int id, int? occurrenceId)
        {
            var userId = _userManager.GetUserId(User);
            var isAdmin = User.IsInRole(GlobalConstants.Roles.Admin);

            var ev = await _db.Events
                .AsNoTracking()
                .Include(e => e.Organizer)
                .Include(e => e.OrganizerProfile)
                .Include(e => e.Images)
                .Include(e => e.EventSeries)
                    .ThenInclude(s => s!.Occurrences)
                        .ThenInclude(o => o.UserTickets)
                            .ThenInclude(ut => ut.Transaction)
                .Include(e => e.VenueLayout)
                    .ThenInclude(l => l!.Sections)
                .Include(e => e.VenueLayout)
                    .ThenInclude(l => l!.Seats)
                        .ThenInclude(s => s.Section)
                .Include(e => e.Likes)
                .Include(e => e.Saves)
                .Include(e => e.Attendances)
                .Include(e => e.Tickets)
                .Include(e => e.Comments)
                    .ThenInclude(c => c.User)
                .Include(e => e.Comments)
                    .ThenInclude(c => c.AuthorOrganizerProfile)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (ev == null)
            {
                return NotFound();
            }

            if (!ev.IsApproved && !isAdmin && ev.OrganizerId != userId)
            {
                return NotFound();
            }

            var occurrences = ev.EventSeries?.Occurrences
                .OrderBy(o => o.StartDateTime)
                .ToList() ?? new List<EventOccurrence>();
            var selectedOccurrence = occurrences.FirstOrDefault(o => o.Id == occurrenceId)
                ?? occurrences.FirstOrDefault(o => o.Status == EventOccurrenceStatus.Scheduled && o.StartDateTime > DateTime.UtcNow)
                ?? occurrences.FirstOrDefault();

            var comments = ev.Comments.ToList();
            var repliesByParentId = comments
                .Where(c => c.ParentCommentId.HasValue)
                .GroupBy(c => c.ParentCommentId!.Value)
                .ToDictionary(g => g.Key, g => g.OrderBy(r => r.CreatedAt).ToList());

            if (ev.VenueLayoutId.HasValue && ev.TicketingMode != EventTicketingMode.GeneralAdmission)
            {
                await _layouts.EnsureInventoryAsync(ev.Id, selectedOccurrence?.Id, ev.VenueLayoutId.Value);
            }

            var vm = new EventDetailsViewModel
            {
                Id = ev.Id,
                Title = ev.Title,
                Description = ev.Description,
                StartTime = selectedOccurrence?.StartDateTime ?? ev.StartTime,
                EndTime = selectedOccurrence?.EndDateTime ?? ev.EndTime,
                Genre = ev.Genre,
                ImageUrl = ev.ImageUrl,
                IsApproved = ev.IsApproved,
                Address = ev.Address,
                City = ev.City,
                Latitude = ev.Latitude,
                Longitude = ev.Longitude,
                OrganizerId = ev.OrganizerId,
                OrganizerProfileId = ev.OrganizerProfileId,
                OrganizerName = ev.OrganizerProfile != null
                    ? ev.OrganizerProfile.DisplayName
                    : ev.Organizer.UserName ?? string.Empty,
                ImageUrls = ev.Images.Select(i => i.ImageUrl).ToList(),
                LikesCount = ev.Likes.Count,
                SavesCount = ev.Saves.Count,
                GoingCount = ev.Attendances.Count(a => a.Status == EventAttendanceStatus.Going),
                InterestedCount = ev.Attendances.Count(a => a.Status == EventAttendanceStatus.Interested),
                CommentsCount = comments.Count,
                CurrentUserLiked = userId != null && ev.Likes.Any(l => l.UserId == userId),
                CurrentUserSaved = userId != null && ev.Saves.Any(s => s.UserId == userId),
                CurrentUserPinned = userId != null && await _db.Users.AnyAsync(u => u.Id == userId && u.PinnedEventId == ev.Id),
                CurrentUserSharedToProfile = userId != null && await _db.UserProfileSharedEvents.AnyAsync(s => s.UserId == userId && s.EventId == ev.Id),
                CurrentUserAttendanceStatus = userId == null
                    ? null
                    : ev.Attendances
                        .Where(a => a.UserId == userId)
                        .Select(a => (EventAttendanceStatus?)a.Status)
                        .FirstOrDefault(),
                Comments = comments
                    .Where(c => c.ParentCommentId == null)
                    .OrderByDescending(c => c.CreatedAt)
                    .Select(c => ToCommentViewModel(c, userId, isAdmin, repliesByParentId))
                    .ToList(),
                CanEdit = isAdmin || ev.OrganizerId == userId,
                CanDelete = isAdmin || ev.OrganizerId == userId,
                CanManageTickets = isAdmin || ev.OrganizerId == userId,
                IsRecurring = ev.EventSeries != null && ev.EventSeries.RecurrenceType != EventRecurrenceType.None,
                EventSeriesId = ev.EventSeries?.Id,
                SelectedOccurrenceId = selectedOccurrence?.Id,
                SelectedOccurrenceStatus = selectedOccurrence?.Status,
                TicketingMode = ev.TicketingMode,
                HasSeatLayout = ev.VenueLayoutId.HasValue && ev.TicketingMode != EventTicketingMode.GeneralAdmission,
                Occurrences = occurrences
                    .Select(o => new EventOccurrenceOptionViewModel
                    {
                        Id = o.Id,
                        StartDateTime = o.StartDateTime,
                        EndDateTime = o.EndDateTime,
                        Status = o.Status,
                        CapacityOverride = o.CapacityOverride,
                        SoldCount = o.UserTickets.Count(ut => ut.Transaction.Status == GlobalConstants.TransactionStatuses.Paid),
                    })
                    .ToList(),
                Tickets = ev.Tickets
                    .Where(t => t.IsActive)
                    .OrderBy(t => t.Price)
                    .Select(t =>
                    {
                        var remaining = t.QuantityRemaining;
                        if (selectedOccurrence != null)
                        {
                            var soldForOccurrence = selectedOccurrence.UserTickets
                                .Count(ut => ut.TicketId == t.Id && ut.Transaction.Status == GlobalConstants.TransactionStatuses.Paid);
                            var occurrenceCapacity = selectedOccurrence.CapacityOverride ?? t.QuantityTotal;
                            remaining = Math.Max(0, occurrenceCapacity - soldForOccurrence);
                        }

                        var maxPurchaseQuantity = ev.VenueLayoutId.HasValue && ev.TicketingMode != EventTicketingMode.GeneralAdmission
                            ? Math.Min(remaining, 50)
                            : Math.Min(remaining, 10);

                        return new EventsApp.ViewModels.Tickets.EventTicketOptionViewModel
                        {
                            Id = t.Id,
                            Name = t.Name,
                            Description = t.Description,
                            Price = t.Price,
                            QuantityRemaining = remaining,
                            MaxPurchaseQuantity = maxPurchaseQuantity,
                            IsActive = t.IsActive,
                            RequiresAttendeeNames = t.RequiresAttendeeNames,
                        };
                    })
                    .ToList(),
            };

            if (vm.HasSeatLayout && ev.VenueLayoutId.HasValue)
            {
                vm.SeatMap = await BuildSeatMapAsync(ev.VenueLayoutId.Value, ev.Id, selectedOccurrence?.Id);
            }

            if (User.Identity?.IsAuthenticated == true)
            {
                vm.ActingIdentities = await _actingIdentity.GetOptionsAsync(HttpContext, ev.OrganizerProfileId);
            }

            if (userId != null)
            {
                await _socialFeed.TrackActivityAsync(userId, UserActivityType.EventViewed, eventId: id);
            }

            vm.SimilarEvents = await _db.Events
                .AsNoTracking()
                .Where(e => e.Id != ev.Id
                    && e.IsApproved
                    && (e.Genre == ev.Genre || e.City == ev.City)
                    && e.StartTime >= DateTime.UtcNow)
                .OrderByDescending(e => e.Genre == ev.Genre && e.City == ev.City ? 2 : (e.Genre == ev.Genre ? 1 : 0))
                .ThenBy(e => e.StartTime)
                .Take(4)
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
                    OrganizerProfileId = e.OrganizerProfileId,
                    OrganizerName = e.OrganizerProfile != null ? e.OrganizerProfile.DisplayName : e.Organizer.UserName ?? string.Empty,
                    LikesCount = e.Likes.Count,
                    SavesCount = e.Saves.Count,
                    GoingCount = e.Attendances.Count(a => a.Status == EventAttendanceStatus.Going),
                    InterestedCount = e.Attendances.Count(a => a.Status == EventAttendanceStatus.Interested),
                    CurrentUserLiked = userId != null && e.Likes.Any(l => l.UserId == userId),
                    CurrentUserSaved = userId != null && e.Saves.Any(s => s.UserId == userId),
                    HasActiveTickets = e.Tickets.Any(t => t.IsActive),
                    HasPaidTickets = e.Tickets.Any(t => t.IsActive && t.Price > 0m),
                    LowestPaidTicketPrice = e.Tickets
                        .Where(t => t.IsActive && t.Price > 0m)
                        .Min(t => (decimal?)t.Price),
                })
                .ToListAsync();

            return View(vm);
        }


        [Authorize(Roles = GlobalConstants.Roles.Admin + "," + GlobalConstants.Roles.Organizer)]
        public async Task<IActionResult> Create()
        {
            var profileGate = await RequireOrganizerPublicPageAsync();
            if (profileGate != null)
            {
                return profileGate;
            }

            var vm = new EventCreateEditViewModel
            {
                CanEditApproval = User.IsInRole(GlobalConstants.Roles.Admin),
                Cities = GetAllBgCities(),
                CityCoordinatesMap = GetAllBgCitiesMap(),
                OrganizerProfiles = await GetOrganizerProfileOptionsAsync(),
                OrganizerProfileId = await GetDefaultOrganizerProfileIdAsync(),
                RecurrenceStartDate = DateTime.UtcNow.AddDays(1).Date,
                RecurrenceEndDate = DateTime.UtcNow.AddDays(31).Date,
                RecurrenceStartTime = TimeSpan.FromHours(20),
                RecurrenceEndTime = TimeSpan.FromHours(22),
                VenueLayouts = await GetVenueLayoutOptionsAsync(),
            };
            await PopulateBusinessContextAsync(vm);
            return View(vm);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = GlobalConstants.Roles.Admin + "," + GlobalConstants.Roles.Organizer)]
        public async Task<IActionResult> Create(EventCreateEditViewModel input)
        {
            var userId = _userManager.GetUserId(User)!;
            var isAdmin = User.IsInRole(GlobalConstants.Roles.Admin);

            var profileGate = await RequireOrganizerPublicPageAsync();
            if (profileGate != null)
            {
                return profileGate;
            }

            var profile = isAdmin && input.OrganizerProfileId.HasValue
                ? await _db.OrganizerProfiles.FirstOrDefaultAsync(p => p.Id == input.OrganizerProfileId.Value)
                : await ResolveOrganizerProfileAsync(input.OrganizerProfileId);
            if (!isAdmin && profile == null)
            {
                ModelState.AddModelError(nameof(input.OrganizerProfileId), "Избери активна публична организаторска страница.");
            }

            if (input.EndTime <= input.StartTime)
            {
                ModelState.AddModelError(nameof(input.EndTime), "Крайният час трябва да е след началния.");
            }

            ValidateRecurringInput(input);
            await ValidateLayoutInputAsync(input, userId, isAdmin);


            if (!ModelState.IsValid)
            {
                input.CanEditApproval = isAdmin;
                input.Cities = GetAllBgCities();
                input.CityCoordinatesMap = GetAllBgCitiesMap();
                input.OrganizerProfiles = await GetOrganizerProfileOptionsAsync();
                input.VenueLayouts = await GetVenueLayoutOptionsAsync();
                await PopulateBusinessContextAsync(input);
                return View(input);
            }

            var eventStart = input.StartTime;
            var eventEnd = input.EndTime;
            if (input.RecurrenceType != EventRecurrenceType.None
                && input.RecurrenceStartDate.HasValue
                && input.RecurrenceStartTime.HasValue
                && input.RecurrenceEndTime.HasValue)
            {
                eventStart = input.RecurrenceStartDate.Value.Date.Add(input.RecurrenceStartTime.Value);
                eventEnd = input.RecurrenceStartDate.Value.Date.Add(input.RecurrenceEndTime.Value);
                if (eventEnd <= eventStart)
                {
                    eventEnd = eventEnd.AddDays(1);
                }
            }

            var ev = new Event
            {
                Title = input.Title,
                Description = input.Description,
                City = input.City,
                Address = input.Address,
                OrganizerId = userId,
                OrganizerProfileId = isAdmin ? input.OrganizerProfileId : profile!.Id,
                BusinessWorkspaceId = isAdmin ? profile?.BusinessWorkspaceId : profile!.BusinessWorkspaceId,
                StartTime = eventStart,
                EndTime = eventEnd,
                Genre = input.Genre,
                Latitude = input.Latitude,
                Longitude = input.Longitude,
                IsApproved = isAdmin && input.IsApproved,
                TicketingMode = input.TicketingMode,
                VenueLayoutId = input.TicketingMode == EventTicketingMode.GeneralAdmission ? null : input.VenueLayoutId,
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

            if (input.RecurrenceType != EventRecurrenceType.None)
            {
                var series = ToEventSeries(ev, input);
                _db.EventSeries.Add(series);
                await _db.SaveChangesAsync();
                await _recurringEvents.RegenerateOccurrencesAsync(series, RecurringEditScope.EntireSeries);

                var firstOccurrence = await _db.EventOccurrences
                    .Where(o => o.EventSeriesId == series.Id)
                    .OrderBy(o => o.StartDateTime)
                    .FirstOrDefaultAsync();

                if (firstOccurrence != null)
                {
                    ev.StartTime = firstOccurrence.StartDateTime;
                    ev.EndTime = firstOccurrence.EndDateTime;
                    await _db.SaveChangesAsync();
                }
            }

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

                    var series = await _db.EventSeries.FirstOrDefaultAsync(s => s.EventId == ev.Id);
                    if (series != null)
                    {
                        series.ImageUrl = uploadResult.Url;
                        await _db.SaveChangesAsync();
                    }
                }
            }

            await EnsureSeatInventoriesForEventAsync(ev.Id);

            TempData["StatusMessage"] = "Събитието е създадено.";
            return RedirectToAction(nameof(Details), new { id = ev.Id });
        }

        // Връща всички градове от CityCoordinates
        private List<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem> GetAllBgCities()
        {
            var cities = typeof(EventsApp.Common.CityCoordinates)
                .GetField("Coords", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
                ?.GetValue(null) as Dictionary<string, (double Lat, double Lng)>;

            if (cities == null) return new List<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem>();

            return cities
                .Where(kv => !string.IsNullOrWhiteSpace(kv.Key) && kv.Key.All(ch => ch < 128))
                .Select(kv => kv.Key)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(c => c)
                .Select(c => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem { Value = c, Text = c })
                .ToList();
        }

        private Dictionary<string, string> GetAllBgCitiesMap()
        {
            var cities = typeof(EventsApp.Common.CityCoordinates)
                .GetField("Coords", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
                ?.GetValue(null) as Dictionary<string, (double Lat, double Lng)>;

            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (cities == null) return dict;

            foreach (var kv in cities)
            {
                var name = kv.Key;
                // only keep latin keys (the map contains both latin and cyrillic entries)
                if (string.IsNullOrWhiteSpace(name) || !name.All(ch => ch < 128)) continue;
                dict[name] = kv.Value.Lat.ToString(System.Globalization.CultureInfo.InvariantCulture) + "," + kv.Value.Lng.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }

            return dict;
        }
        [Authorize(Roles = GlobalConstants.Roles.Admin + "," + GlobalConstants.Roles.Organizer)]
        public async Task<IActionResult> Edit(int id)
        {
            var userId = _userManager.GetUserId(User)!;
            var isAdmin = User.IsInRole(GlobalConstants.Roles.Admin);

            var ev = await _db.Events
                .Include(e => e.EventSeries)
                .FirstOrDefaultAsync(e => e.Id == id);
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
                OrganizerProfileId = ev.OrganizerProfileId,
                Latitude = ev.Latitude,
                Longitude = ev.Longitude,
                RecurrenceType = ev.EventSeries?.RecurrenceType ?? EventRecurrenceType.None,
                RecurrenceInterval = ev.EventSeries?.Interval ?? 1,
                SelectedDaysOfWeek = ParseDaysForInput(ev.EventSeries?.DaysOfWeek),
                RecurrenceStartDate = ev.EventSeries?.StartDate.Date,
                RecurrenceEndDate = ev.EventSeries?.EndDate.Date,
                RecurrenceStartTime = ev.EventSeries?.StartTime,
                RecurrenceEndTime = ev.EventSeries?.EndTime,
                TimeZone = ev.EventSeries?.TimeZone ?? "Europe/Sofia",
                TicketingMode = ev.TicketingMode,
                VenueLayoutId = ev.VenueLayoutId,
                IsApproved = ev.IsApproved,
                CanEditApproval = isAdmin,
                OrganizerProfiles = await GetOrganizerProfileOptionsAsync(),
                VenueLayouts = await GetVenueLayoutOptionsAsync(),
            };
            await PopulateBusinessContextAsync(vm);
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = GlobalConstants.Roles.Admin + "," + GlobalConstants.Roles.Organizer)]
        public async Task<IActionResult> Edit(int id, EventCreateEditViewModel input)
        {
            var userId = _userManager.GetUserId(User)!;
            var isAdmin = User.IsInRole(GlobalConstants.Roles.Admin);

            var ev = await _db.Events
                .Include(e => e.EventSeries)
                .FirstOrDefaultAsync(e => e.Id == id);
            if (ev == null)
            {
                return NotFound();
            }

            if (!isAdmin && ev.OrganizerId != userId)
            {
                return Forbid();
            }

            var profile = isAdmin && input.OrganizerProfileId.HasValue
                ? await _db.OrganizerProfiles.FirstOrDefaultAsync(p => p.Id == input.OrganizerProfileId.Value)
                : await ResolveOrganizerProfileAsync(input.OrganizerProfileId);
            if (!isAdmin && profile == null)
            {
                ModelState.AddModelError(nameof(input.OrganizerProfileId), "Избери активна публична организаторска страница.");
            }

            if (input.EndTime <= input.StartTime)
            {
                ModelState.AddModelError(nameof(input.EndTime), "Крайният час трябва да е след началния.");
            }

            ValidateRecurringInput(input);
            await ValidateLayoutInputAsync(input, userId, isAdmin);

            if (!ModelState.IsValid)
            {
                input.CanEditApproval = isAdmin;
                input.OrganizerProfiles = await GetOrganizerProfileOptionsAsync();
                input.VenueLayouts = await GetVenueLayoutOptionsAsync();
                await PopulateBusinessContextAsync(input);
                return View(input);
            }

            var eventStart = input.StartTime;
            var eventEnd = input.EndTime;
            if (input.RecurrenceType != EventRecurrenceType.None
                && input.RecurrenceStartDate.HasValue
                && input.RecurrenceStartTime.HasValue
                && input.RecurrenceEndTime.HasValue)
            {
                eventStart = input.RecurrenceStartDate.Value.Date.Add(input.RecurrenceStartTime.Value);
                eventEnd = input.RecurrenceStartDate.Value.Date.Add(input.RecurrenceEndTime.Value);
                if (eventEnd <= eventStart)
                {
                    eventEnd = eventEnd.AddDays(1);
                }
            }

            var addressChanged = !string.Equals(ev.Address, input.Address, StringComparison.Ordinal)
                                 || !string.Equals(ev.City, input.City, StringComparison.Ordinal);

            ev.Title = input.Title;
            ev.Description = input.Description;
            ev.OrganizerProfileId = isAdmin ? input.OrganizerProfileId : profile!.Id;
            ev.BusinessWorkspaceId = isAdmin ? profile?.BusinessWorkspaceId : profile!.BusinessWorkspaceId;
            ev.City = input.City;
            ev.Address = input.Address;
            ev.StartTime = eventStart;
            ev.EndTime = eventEnd;
            ev.Genre = input.Genre;
            ev.Latitude = input.Latitude;
            ev.Longitude = input.Longitude;
            ev.TicketingMode = input.TicketingMode;
            ev.VenueLayoutId = input.TicketingMode == EventTicketingMode.GeneralAdmission ? null : input.VenueLayoutId;

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
            if (isAdmin)
            {
                ev.IsApproved = input.IsApproved;
            }

            await _db.SaveChangesAsync();

            if (input.RecurrenceType != EventRecurrenceType.None)
            {
                if (ev.EventSeries == null)
                {
                    ev.EventSeries = ToEventSeries(ev, input);
                    _db.EventSeries.Add(ev.EventSeries);
                    await _db.SaveChangesAsync();
                }
                else
                {
                    ApplySeriesInput(ev.EventSeries, ev, input);
                }

                await _db.SaveChangesAsync();
                await _recurringEvents.RegenerateOccurrencesAsync(ev.EventSeries, input.RecurringEditScope);
            }

            await EnsureSeatInventoriesForEventAsync(ev.Id);

            TempData["StatusMessage"] = "Събитието е обновено.";
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

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = GlobalConstants.Roles.Admin + "," + GlobalConstants.Roles.Organizer)]
        public async Task<IActionResult> CancelOccurrence(int id, int occurrenceId)
        {
            var userId = _userManager.GetUserId(User)!;
            var cancelled = await _recurringEvents.CancelOccurrenceAsync(
                occurrenceId,
                userId,
                User.IsInRole(GlobalConstants.Roles.Admin));

            TempData["StatusMessage"] = cancelled
                ? "Тази дата беше отменена."
                : "Датата не можа да бъде отменена.";

            return RedirectToAction(nameof(Details), new { id, occurrenceId });
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = GlobalConstants.Roles.Admin + "," + GlobalConstants.Roles.Organizer)]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var userId = _userManager.GetUserId(User)!;
            var isAdmin = User.IsInRole(GlobalConstants.Roles.Admin);

            var ev = await _db.Events
                .Include(e => e.EventSeries)
                    .ThenInclude(s => s!.Occurrences)
                .FirstOrDefaultAsync(e => e.Id == id);
            if (ev == null)
            {
                return NotFound();
            }

            if (!isAdmin && ev.OrganizerId != userId)
            {
                return Forbid();
            }

            var occurrenceIds = ev.EventSeries?.Occurrences.Select(o => o.Id).ToList() ?? new List<int>();
            var inventories = await _db.EventSeatInventories
                .Where(i => i.EventId == ev.Id || (i.EventOccurrenceId.HasValue && occurrenceIds.Contains(i.EventOccurrenceId.Value)))
                .ToListAsync();
            _db.EventSeatInventories.RemoveRange(inventories);
            _db.Events.Remove(ev);
            await _db.SaveChangesAsync();
            TempData["StatusMessage"] = "Събитието е изтрито.";
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
                await _socialFeed.TrackActivityAsync(userId, UserActivityType.EventLiked, eventId: id);
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
        public async Task<IActionResult> Save(int id, string? returnUrl)
        {
            var userId = _userManager.GetUserId(User)!;
            var exists = await _db.EventSaves.AnyAsync(s => s.EventId == id && s.UserId == userId);
            if (!exists && await _db.Events.AnyAsync(e => e.Id == id))
            {
                _db.EventSaves.Add(new EventSave { EventId = id, UserId = userId });
                await _db.SaveChangesAsync();
                await _socialFeed.TrackActivityAsync(userId, UserActivityType.EventSaved, eventId: id);
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
        public async Task<IActionResult> Unsave(int id, string? returnUrl)
        {
            var userId = _userManager.GetUserId(User)!;
            var save = await _db.EventSaves.FirstOrDefaultAsync(s => s.EventId == id && s.UserId == userId);
            if (save != null)
            {
                _db.EventSaves.Remove(save);
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
        public async Task<IActionResult> Attendance(int id, EventAttendanceStatus status, string? returnUrl)
        {
            if (!await _db.Events.AnyAsync(e => e.Id == id))
            {
                return NotFound();
            }

            var userId = _userManager.GetUserId(User)!;
            var attendance = await _db.EventAttendances.FirstOrDefaultAsync(a => a.EventId == id && a.UserId == userId);
            if (attendance == null)
            {
                _db.EventAttendances.Add(new EventAttendance
                {
                    EventId = id,
                    UserId = userId,
                    Status = status,
                });
            }
            else if (attendance.Status == status)
            {
                _db.EventAttendances.Remove(attendance);
            }
            else
            {
                attendance.Status = status;
                attendance.UpdatedAt = DateTime.UtcNow;
            }

            await _db.SaveChangesAsync();
            await _socialFeed.TrackActivityAsync(
                userId,
                status == EventAttendanceStatus.Going ? UserActivityType.EventGoing : UserActivityType.EventInterested,
                eventId: id);

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
        public async Task<IActionResult> AddComment(int id, string content, string? actingIdentityKey, int? parentCommentId)
        {
            if (!await _permissions.CanCommentAsync(User))
            {
                return Forbid();
            }

            if (string.IsNullOrWhiteSpace(content))
            {
                TempData["StatusMessage"] = "Коментарът не може да е празен.";
                return RedirectToAction(nameof(Details), new { id });
            }

            content = content.Trim();
            if (content.Length > GlobalConstants.Comment.ContentMaxLength)
            {
                content = content[..GlobalConstants.Comment.ContentMaxLength];
            }

            var ev = await _db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id);
            if (ev == null)
            {
                return NotFound();
            }

            if (parentCommentId.HasValue)
            {
                var parentIsValid = await _db.EventComments
                    .AsNoTracking()
                    .AnyAsync(c => c.Id == parentCommentId.Value && c.EventId == id && c.ParentCommentId == null);
                if (!parentIsValid)
                {
                    TempData["StatusMessage"] = "Reply target was not found.";
                    return RedirectToAction(nameof(Details), new { id });
                }
            }

            var userId = _userManager.GetUserId(User)!;
            var identity = await _actingIdentity.ResolveAsync(HttpContext, actingIdentityKey, ev.OrganizerProfileId);
            if (identity == null || !await _permissions.CanCommentAsIdentityAsync(User, identity.Type, identity.OrganizerProfileId))
            {
                return Forbid();
            }

            _db.EventComments.Add(new EventComment
            {
                EventId = id,
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
            var replies = await _db.EventComments
                .Where(c => c.ParentCommentId == comment.Id)
                .ToListAsync();
            if (replies.Count > 0)
            {
                _db.EventComments.RemoveRange(replies);
            }
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
                    OrganizerProfileId = e.OrganizerProfileId,
                    OrganizerName = e.OrganizerProfile != null ? e.OrganizerProfile.DisplayName : e.Organizer.UserName ?? string.Empty,
                    LikesCount = e.Likes.Count,
                    CommentsCount = e.Comments.Count,
                    SavesCount = e.Saves.Count,
                    GoingCount = e.Attendances.Count(a => a.Status == EventAttendanceStatus.Going),
                    InterestedCount = e.Attendances.Count(a => a.Status == EventAttendanceStatus.Interested),
                    CurrentUserLiked = e.Likes.Any(l => l.UserId == userId),
                    CurrentUserSaved = e.Saves.Any(s => s.UserId == userId),
                    CurrentUserAttendanceStatus = e.Attendances
                        .Where(a => a.UserId == userId)
                        .Select(a => (EventAttendanceStatus?)a.Status)
                        .FirstOrDefault(),
                    HasActiveTickets = e.Tickets.Any(t => t.IsActive),
                    HasPaidTickets = e.Tickets.Any(t => t.IsActive && t.Price > 0m),
                    LowestPaidTicketPrice = e.Tickets
                        .Where(t => t.IsActive && t.Price > 0m)
                        .Min(t => (decimal?)t.Price),
                })
                .ToListAsync();

            ViewBag.HasPreferences = prefs != null;
            return View(events);
        }

        private async Task<EventSeatMapViewModel?> BuildSeatMapAsync(int layoutId, int eventId, int? occurrenceId)
        {
            var layout = await _db.VenueLayouts
                .AsNoTracking()
                .Include(l => l.Sections)
                .Include(l => l.Seats)
                    .ThenInclude(s => s.Section)
                .FirstOrDefaultAsync(l => l.Id == layoutId);

            if (layout == null)
            {
                return null;
            }

            var inventoryRows = occurrenceId.HasValue
                ? await _db.EventSeatInventories
                    .AsNoTracking()
                    .Where(i => i.EventOccurrenceId == occurrenceId.Value)
                    .ToListAsync()
                : await _db.EventSeatInventories
                    .AsNoTracking()
                    .Where(i => i.EventId == eventId && i.EventOccurrenceId == null)
                    .ToListAsync();

            var inventory = inventoryRows.ToDictionary(i => i.SeatId);

            return new EventSeatMapViewModel
            {
                LayoutId = layout.Id,
                LayoutName = layout.Name,
                Floors = layout.Sections
                    .Select(s => string.IsNullOrWhiteSpace(s.FloorName) ? "Floor 1" : s.FloorName)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                Sections = layout.Sections
                    .OrderBy(s => s.FloorName)
                    .ThenBy(s => s.Name)
                    .Select(section => new EventSeatSectionViewModel
                    {
                        Id = section.Id,
                        Name = section.Name,
                        FloorName = string.IsNullOrWhiteSpace(section.FloorName) ? "Floor 1" : section.FloorName,
                        Type = section.Type,
                        Shape = string.IsNullOrWhiteSpace(section.Shape) ? "Rectangle" : section.Shape,
                        PriceModifier = section.PriceModifier,
                        X = section.X,
                        Y = section.Y,
                        Width = section.Width,
                        Height = section.Height,
                        Rotation = section.Rotation,
                        Seats = layout.Seats
                            .Where(seat => seat.SectionId == section.Id)
                            .OrderBy(seat => seat.Row)
                            .ThenBy(seat => seat.Number)
                            .Select(seat =>
                            {
                                inventory.TryGetValue(seat.Id, out var inv);
                                return new EventSeatViewModel
                                {
                                    Id = seat.Id,
                                    InventoryId = inv?.Id,
                                    Label = string.IsNullOrWhiteSpace(seat.Label) ? $"{seat.Row}{seat.Number}" : seat.Label,
                                    X = seat.X,
                                    Y = seat.Y,
                                    Radius = seat.Radius <= 0 ? 16 : seat.Radius,
                                    Rotation = seat.Rotation,
                                    Capacity = Math.Max(1, seat.Capacity),
                                    IsCapacityUnlimited = seat.IsCapacityUnlimited,
                                    SeatType = seat.SeatType,
                                    Status = seat.Status == LayoutSeatStatus.Blocked
                                        ? EventSeatInventoryStatus.Blocked
                                        : inv?.Status ?? EventSeatInventoryStatus.Available,
                                };
                            })
                            .ToList(),
                    })
                    .ToList(),
            };
        }

        private void ValidateRecurringInput(EventCreateEditViewModel input)
        {
            if (input.RecurrenceType == EventRecurrenceType.None)
            {
                return;
            }

            if (!input.RecurrenceStartDate.HasValue)
            {
                ModelState.AddModelError(nameof(input.RecurrenceStartDate), "Избери начална дата на серията.");
            }

            if (!input.RecurrenceEndDate.HasValue)
            {
                ModelState.AddModelError(nameof(input.RecurrenceEndDate), "Избери крайна дата на серията.");
            }

            if (!input.RecurrenceStartTime.HasValue)
            {
                ModelState.AddModelError(nameof(input.RecurrenceStartTime), "Избери начален час.");
            }

            if (!input.RecurrenceEndTime.HasValue)
            {
                ModelState.AddModelError(nameof(input.RecurrenceEndTime), "Избери краен час.");
            }

            if (input.RecurrenceInterval < 1)
            {
                ModelState.AddModelError(nameof(input.RecurrenceInterval), "Интервалът трябва да е поне 1.");
            }

            if (input.RecurrenceType == EventRecurrenceType.Weekly && input.SelectedDaysOfWeek.Count == 0)
            {
                ModelState.AddModelError(nameof(input.SelectedDaysOfWeek), "Избери поне един ден от седмицата.");
            }

            if (input.RecurrenceStartDate.HasValue && input.RecurrenceEndDate.HasValue)
            {
                var rangeDays = (input.RecurrenceEndDate.Value.Date - input.RecurrenceStartDate.Value.Date).Days;
                if (rangeDays < 0)
                {
                    ModelState.AddModelError(nameof(input.RecurrenceEndDate), "Крайната дата трябва да е след началната.");
                }
                else if (rangeDays > 370)
                {
                    ModelState.AddModelError(nameof(input.RecurrenceEndDate), "Засега използвай период до 370 дни.");
                }
            }
        }

        private async Task ValidateLayoutInputAsync(EventCreateEditViewModel input, string userId, bool isAdmin)
        {
            if (input.TicketingMode == EventTicketingMode.GeneralAdmission)
            {
                return;
            }

            if (!input.VenueLayoutId.HasValue)
            {
                ModelState.AddModelError(nameof(input.VenueLayoutId), "Избери преизползваем layout или остави без места.");
                return;
            }

            var layout = await _db.VenueLayouts
                .AsNoTracking()
                .FirstOrDefaultAsync(l => l.Id == input.VenueLayoutId.Value);

            if (layout == null || (!isAdmin && layout.OrganizerId != userId))
            {
                ModelState.AddModelError(nameof(input.VenueLayoutId), "Този layout не е наличен.");
            }
        }

        private EventSeries ToEventSeries(Event ev, EventCreateEditViewModel input)
        {
            var series = new EventSeries
            {
                EventId = ev.Id,
                OrganizerId = ev.OrganizerId,
            };

            ApplySeriesInput(series, ev, input);
            return series;
        }

        private static void ApplySeriesInput(EventSeries series, Event ev, EventCreateEditViewModel input)
        {
            series.Title = ev.Title;
            series.Description = ev.Description;
            series.Category = ev.Genre;
            series.Location = ev.Address;
            series.City = ev.City;
            series.ImageUrl = ev.ImageUrl;
            series.RecurrenceType = input.RecurrenceType;
            series.Interval = Math.Max(1, input.RecurrenceInterval);
            series.DaysOfWeek = SerializeDays(input.SelectedDaysOfWeek);
            series.StartDate = input.RecurrenceStartDate!.Value.Date;
            series.EndDate = input.RecurrenceEndDate!.Value.Date;
            series.StartTime = input.RecurrenceStartTime!.Value;
            series.EndTime = input.RecurrenceEndTime!.Value;
            series.TimeZone = string.IsNullOrWhiteSpace(input.TimeZone) ? "Europe/Sofia" : input.TimeZone.Trim();
            series.Status = ev.IsApproved ? EventSeriesStatus.Published : EventSeriesStatus.Draft;
            series.UpdatedAt = DateTime.UtcNow;
        }

        private async Task EnsureSeatInventoriesForEventAsync(int eventId)
        {
            var ev = await _db.Events
                .AsNoTracking()
                .Include(e => e.EventSeries)
                    .ThenInclude(s => s!.Occurrences)
                .FirstOrDefaultAsync(e => e.Id == eventId);

            if (ev?.VenueLayoutId == null || ev.TicketingMode == EventTicketingMode.GeneralAdmission)
            {
                return;
            }

            if (ev.EventSeries?.Occurrences.Any() == true)
            {
                foreach (var occurrence in ev.EventSeries.Occurrences)
                {
                    await _layouts.EnsureInventoryAsync(eventId, occurrence.Id, ev.VenueLayoutId.Value);
                }
            }
            else
            {
                await _layouts.EnsureInventoryAsync(eventId, null, ev.VenueLayoutId.Value);
            }
        }

        private async Task<IEnumerable<SelectListItem>> GetVenueLayoutOptionsAsync()
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Array.Empty<SelectListItem>();
            }

            return await _db.VenueLayouts
                .AsNoTracking()
                .Where(l => l.OrganizerId == userId && l.Status != VenueLayoutStatus.Archived)
                .OrderBy(l => l.VenueName)
                .ThenBy(l => l.Name)
                .Select(l => new SelectListItem
                {
                    Value = l.Id.ToString(),
                    Text = $"{l.VenueName} - {l.Name} v{l.Version}",
                })
                .ToListAsync();
        }

        private static string? SerializeDays(IEnumerable<DayOfWeek> days)
        {
            var value = string.Join(",", days.Distinct().OrderBy(d => d).Select(d => d.ToString()));
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }

        private static List<DayOfWeek> ParseDaysForInput(string? days)
        {
            if (string.IsNullOrWhiteSpace(days))
            {
                return new List<DayOfWeek>();
            }

            return days
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(value => Enum.TryParse<DayOfWeek>(value, ignoreCase: true, out var day) ? (DayOfWeek?)day : null)
                .Where(day => day.HasValue)
                .Select(day => day!.Value)
                .ToList();
        }

        private async Task<IActionResult?> RequireOrganizerPublicPageAsync()
        {
            if (User.IsInRole(GlobalConstants.Roles.Admin))
            {
                return null;
            }

            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Challenge();
            }

            var hasPublicPage = await _db.OrganizerProfiles
                .AsNoTracking()
                .AnyAsync(p => p.OwnerId == userId && p.IsActive);

            if (hasPublicPage)
            {
                return null;
            }

            TempData["StatusMessage"] = "Създай публична организаторска страница преди да публикуваш събития.";
            return RedirectToAction("Profile", "Organizer");
        }

        private async Task<IEnumerable<SelectListItem>> GetOrganizerProfileOptionsAsync()
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Array.Empty<SelectListItem>();
            }

            return await _db.OrganizerProfiles
                .AsNoTracking()
                .Where(p => p.OwnerId == userId && p.IsActive)
                .OrderByDescending(p => p.IsDefault)
                .ThenBy(p => p.DisplayName)
                .Select(p => new SelectListItem
                {
                    Value = p.Id.ToString(),
                    Text = p.IsDefault ? p.DisplayName + " (default)" : p.DisplayName,
                })
                .ToListAsync();
        }

        private async Task<int?> GetDefaultOrganizerProfileIdAsync()
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrWhiteSpace(userId)) return null;

            var context = await _businessContext.GetContextAsync(HttpContext);
            if (context.Page != null)
            {
                return context.Page.Id;
            }

            return await _db.OrganizerProfiles
                .AsNoTracking()
                .Where(p => p.OwnerId == userId && p.IsActive)
                .OrderByDescending(p => p.IsDefaultForWorkspace)
                .ThenByDescending(p => p.IsDefault)
                .ThenBy(p => p.DisplayName)
                .Select(p => (int?)p.Id)
                .FirstOrDefaultAsync();
        }

        private async Task<OrganizerProfile?> ResolveOrganizerProfileAsync(int? profileId)
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrWhiteSpace(userId)) return null;

            var query = _db.OrganizerProfiles.Where(p => p.OwnerId == userId && p.IsActive);
            if (profileId.HasValue)
            {
                return await query.FirstOrDefaultAsync(p => p.Id == profileId.Value);
            }

            var context = await _businessContext.GetContextAsync(HttpContext);
            if (context.Page != null)
            {
                return context.Page;
            }

            return await query
                .OrderByDescending(p => p.IsDefaultForWorkspace)
                .ThenByDescending(p => p.IsDefault)
                .ThenBy(p => p.DisplayName)
                .FirstOrDefaultAsync();
        }

        private async Task PopulateBusinessContextAsync(EventCreateEditViewModel vm)
        {
            var context = await _businessContext.GetContextAsync(HttpContext);
            vm.ActiveWorkspaceName = context.Workspace?.DisplayName;
            vm.ActivePageName = context.Page?.DisplayName;
        }

        private static string GetCommentDisplayName(EventComment comment)
        {
            return comment.AuthorType == AuthorIdentityType.OrganizerPage && comment.AuthorOrganizerProfile != null
                ? comment.AuthorOrganizerProfile.DisplayName
                : comment.User.UserName ?? string.Empty;
        }

        private static EventCommentViewModel ToCommentViewModel(
            EventComment comment,
            string? currentUserId,
            bool isAdmin,
            IReadOnlyDictionary<int, List<EventComment>> repliesByParentId)
        {
            return new EventCommentViewModel
            {
                Id = comment.Id,
                UserId = comment.UserId,
                UserName = GetCommentDisplayName(comment),
                AuthorImageUrl = comment.AuthorType == AuthorIdentityType.OrganizerPage
                    ? comment.AuthorOrganizerProfile?.AvatarImageUrl
                    : comment.User.ProfileImageUrl,
                AuthorBadgeKey = GetAuthorBadgeKey(comment.AuthorType),
                AuthorBadgeText = GetAuthorBadgeText(comment.AuthorType, comment.UserId == currentUserId),
                AuthorProfileUserId = comment.AuthorType == AuthorIdentityType.OrganizerPage ? null : comment.UserId,
                IsOrganizerPageAuthor = comment.AuthorType == AuthorIdentityType.OrganizerPage,
                Content = comment.Content,
                CreatedAt = comment.CreatedAt,
                CanDelete = isAdmin || comment.UserId == currentUserId,
                Replies = repliesByParentId.TryGetValue(comment.Id, out var replies)
                    ? replies.Select(r => ToCommentViewModel(r, currentUserId, isAdmin, new Dictionary<int, List<EventComment>>())).ToList()
                    : Array.Empty<EventCommentViewModel>(),
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

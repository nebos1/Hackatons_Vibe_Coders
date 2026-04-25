using EventsApp.Common;
using EventsApp.Data;
using EventsApp.Models;
using EventsApp.Services.AI;
using EventsApp.ViewModels.Events;
using EventsApp.ViewModels.Home;
using EventsApp.ViewModels.Posts;
using EventsApp.ViewModels.Search;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EventsApp.Controllers
{
    public class SearchController : Controller
    {
        private const int ResultsPerSection = 12;

        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IAiSearchService _ai;

        public SearchController(ApplicationDbContext db, UserManager<ApplicationUser> userManager, IAiSearchService ai)
        {
            _db = db;
            _userManager = userManager;
            _ai = ai;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string? q, CancellationToken cancellationToken)
        {
            var vm = new SearchResultsViewModel { Query = q };

            if (string.IsNullOrWhiteSpace(q))
            {
                ApplyHint(vm, intentExtracted: false);
                return View(vm);
            }

            var term = q.Trim();
            var userId = _userManager.GetUserId(User);
            var isAdmin = User.IsInRole(GlobalConstants.Roles.Admin);

            AiSearchIntent? intent = null;
            if (_ai.IsEnabled)
            {
                intent = await _ai.InterpretAsync(term, cancellationToken);
            }

            var keyword = intent?.Keyword ?? term;

            var eventsQuery = _db.Events
                .AsNoTracking()
                .Where(e => isAdmin || e.IsApproved);

            if (intent?.City != null)
            {
                var c = intent.City;
                eventsQuery = eventsQuery.Where(e => e.City == c || e.City.Contains(c));
            }

            if (intent?.Genre.HasValue == true)
            {
                var g = intent.Genre.Value;
                eventsQuery = eventsQuery.Where(e => e.Genre == g);
            }

            if (intent?.DateFrom.HasValue == true)
            {
                var df = intent.DateFrom.Value;
                eventsQuery = eventsQuery.Where(e => e.StartTime >= df);
            }

            if (intent?.DateTo.HasValue == true)
            {
                var dt = intent.DateTo.Value.AddDays(1);
                eventsQuery = eventsQuery.Where(e => e.StartTime < dt);
            }

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                eventsQuery = eventsQuery.Where(e =>
                    e.Title.Contains(keyword)
                    || (e.Description != null && e.Description.Contains(keyword))
                    || e.Address.Contains(keyword)
                    || e.City.Contains(keyword));
            }

            vm.Events = await eventsQuery
                .OrderBy(e => e.StartTime)
                .Take(ResultsPerSection)
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
                .ToListAsync(cancellationToken);

            vm.Posts = await _db.Posts
                .AsNoTracking()
                .Where(p => p.Content.Contains(keyword) || (p.Event != null && p.Event.Title.Contains(keyword)))
                .OrderByDescending(p => p.CreatedAt)
                .Take(ResultsPerSection)
                .Select(p => new PostCardViewModel
                {
                    Id = p.Id,
                    OrganizerId = p.OrganizerId,
                    OrganizerName = p.Organizer.UserName ?? string.Empty,
                    Content = p.Content,
                    CreatedAt = p.CreatedAt,
                    EventId = p.EventId,
                    EventTitle = p.Event != null ? p.Event.Title : null,
                    FirstMediaUrl = p.Images.Select(i => i.ImageUrl).FirstOrDefault(),
                    FirstMediaType = p.Images.Select(i => i.MediaType).FirstOrDefault(),
                    LikesCount = p.Likes.Count,
                    CommentsCount = p.Comments.Count,
                    CurrentUserLiked = userId != null && p.Likes.Any(l => l.UserId == userId),
                })
                .ToListAsync(cancellationToken);

            vm.MapMarkers = BuildMarkers(vm.Events);

            if (intent != null)
            {
                vm.AiUsed = true;
                vm.InterpretedCity = intent.City;
                vm.InterpretedKeyword = intent.Keyword;
                vm.InterpretedGenre = intent.Genre;
                vm.InterpretedDateFrom = intent.DateFrom;
                vm.InterpretedDateTo = intent.DateTo;
                vm.InterpretedNearMe = intent.NearMe;
            }

            ApplyHint(vm, intent != null);
            return View(vm);
        }

        private void ApplyHint(SearchResultsViewModel vm, bool intentExtracted)
        {
            if (!_ai.IsEnabled)
            {
                vm.AiHintLevel = "warning";
                vm.AiHint = "Smart AI is OFF. Add Sirma_key to .env to enable.";
                return;
            }

            switch (_ai.LastStatus)
            {
                case AiStatus.MissingProjectId:
                    vm.AiHintLevel = "warning";
                    vm.AiHint = "AI is configured but no agent is linked. Add Sirma_project_id to .env (or Sirma_agent_id with an existing agent UUID from your Sirma dashboard) and restart.";
                    break;
                case AiStatus.ProvisionFailed:
                    vm.AiHintLevel = "danger";
                    vm.AiHint = "Sirma agent provisioning failed: " + (_ai.LastStatusDetail ?? "unknown error");
                    break;
                case AiStatus.CallFailed:
                    vm.AiHintLevel = "danger";
                    vm.AiHint = "Sirma AI call failed: " + (_ai.LastStatusDetail ?? "unknown error");
                    break;
                case AiStatus.ParseFailed:
                    vm.AiHintLevel = "warning";
                    vm.AiHint = "AI responded but the answer didn't match the expected JSON shape - falling back to keyword search.";
                    break;
                case AiStatus.Ok when intentExtracted:
                    vm.AiHintLevel = "success";
                    vm.AiHint = "AI parsed your query into structured filters.";
                    break;
                case AiStatus.Ok:
                    vm.AiHintLevel = "info";
                    vm.AiHint = string.IsNullOrWhiteSpace(vm.Query)
                        ? "Smart AI search is on. Try: \"techno tonight in Sofia\" or \"jazz this weekend\"."
                        : "Showing keyword matches (AI couldn't extract filters from this query).";
                    break;
                default:
                    vm.AiHintLevel = "info";
                    vm.AiHint = "Smart AI search is on.";
                    break;
            }
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
    }
}

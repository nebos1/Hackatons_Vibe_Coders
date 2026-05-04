using System.Globalization;
using EventsApp.Common;
using EventsApp.Data;
using EventsApp.Models;
using EventsApp.Services.AI;
using EventsApp.ViewModels.Wrapped;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EventsApp.Controllers
{
    [Authorize]
    public class WrappedController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IAiSearchService _ai;

        public WrappedController(ApplicationDbContext db, UserManager<ApplicationUser> userManager, IAiSearchService ai)
        {
            _db = db;
            _userManager = userManager;
            _ai = ai;
        }

        public async Task<IActionResult> Index(int? year)
        {
            var userId = _userManager.GetUserId(User)!;
            var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
            var displayName = string.Join(" ", new[] { user?.FirstName, user?.LastName }
                .Where(s => !string.IsNullOrWhiteSpace(s)));
            if (string.IsNullOrWhiteSpace(displayName)) displayName = user?.UserName ?? "ти";

            var y = year ?? DateTime.UtcNow.Year;
            var yearStart = new DateTime(y, 1, 1);
            var yearEnd = yearStart.AddYears(1);

            var attended = await _db.EventAttendances
                .AsNoTracking()
                .Where(a => a.UserId == userId && a.Status == EventAttendanceStatus.Going)
                .Join(_db.Events, a => a.EventId, e => e.Id, (a, e) => e)
                .Where(e => e.StartTime >= yearStart && e.StartTime < yearEnd)
                .Select(e => new
                {
                    e.Id,
                    e.Title,
                    e.City,
                    e.StartTime,
                    e.EndTime,
                    e.Genre,
                    e.ImageUrl,
                    e.OrganizerId,
                    OrganizerName = e.Organizer.UserName,
                    LikesCount = e.Likes.Count,
                })
                .ToListAsync();

            var totalHours = (int)attended.Sum(e => Math.Max(1, (e.EndTime - e.StartTime).TotalHours));
            var topGenreGroup = attended.GroupBy(e => e.Genre).OrderByDescending(g => g.Count()).FirstOrDefault();
            var topCityGroup = attended.GroupBy(e => e.City).OrderByDescending(g => g.Count()).FirstOrDefault();
            var topOrgGroup = attended.GroupBy(e => new { e.OrganizerId, e.OrganizerName })
                .OrderByDescending(g => g.Count()).FirstOrDefault();
            var favEvent = attended.OrderByDescending(e => e.LikesCount).FirstOrDefault();
            var busiestMonth = attended.GroupBy(e => e.StartTime.Month).OrderByDescending(g => g.Count()).FirstOrDefault();
            var topEvents = attended
                .OrderByDescending(e => e.LikesCount)
                .Take(4)
                .Select(e => new EventCardSnapshot
                {
                    Id = e.Id,
                    Title = e.Title,
                    City = e.City,
                    StartTime = e.StartTime,
                    ImageUrl = e.ImageUrl,
                    Genre = e.Genre,
                })
                .ToList();

            var likesGiven = await _db.EventLikes
                .AsNoTracking()
                .Where(l => l.UserId == userId && l.CreatedAt >= yearStart && l.CreatedAt < yearEnd)
                .CountAsync();
            var commentsPosted = await _db.EventComments
                .AsNoTracking()
                .Where(c => c.UserId == userId && c.CreatedAt >= yearStart && c.CreatedAt < yearEnd)
                .CountAsync();
            var organizersFollowed = await _db.Follows
                .AsNoTracking()
                .Where(f => f.FollowerId == userId && f.CreatedAt >= yearStart && f.CreatedAt < yearEnd)
                .CountAsync();

            var vm = new WrappedViewModel
            {
                Year = y,
                DisplayName = displayName,
                TotalEventsAttended = attended.Count,
                TotalHoursOnScene = totalHours,
                CitiesVisited = attended.Select(e => e.City).Distinct().Count(),
                OrganizersFollowed = organizersFollowed,
                LikesGiven = likesGiven,
                CommentsPosted = commentsPosted,
                TopGenre = topGenreGroup?.Key.GetDisplayName(),
                TopGenreCount = topGenreGroup?.Count() ?? 0,
                TopCity = topCityGroup?.Key,
                TopCityCount = topCityGroup?.Count() ?? 0,
                TopOrganizer = topOrgGroup?.Key.OrganizerName,
                TopOrganizerCount = topOrgGroup?.Count() ?? 0,
                FavouriteEventId = favEvent?.Id,
                FavouriteEventTitle = favEvent?.Title,
                BusiestMonth = busiestMonth != null ? new DateTime(y, busiestMonth.Key, 1).ToString("MMMM", new CultureInfo("bg-BG")) : null,
                BusiestMonthCount = busiestMonth?.Count() ?? 0,
                TopEvents = topEvents,
            };

            if (_ai.IsEnabled && vm.TotalEventsAttended > 0)
            {
                try
                {
                    var prompt =
                        "Напиши кратък (2-3 изречения) личен ретроспективен текст на български за потребител на платформа за събития. " +
                        "Тон: топъл, окуражителен, игрив. Обръщай се на 'ти'. Използвай САМО фактите по-долу — не измисляй нищо.\n" +
                        $"Година: {y}. Име: {displayName}.\n" +
                        $"Брой посетени събития: {vm.TotalEventsAttended}.\n" +
                        (vm.TopGenre != null ? $"Любим жанр: {vm.TopGenre} ({vm.TopGenreCount} събития).\n" : "") +
                        (vm.TopCity != null ? $"Любим град: {vm.TopCity} ({vm.TopCityCount} събития).\n" : "") +
                        $"Часове на сцената: {vm.TotalHoursOnScene}. Различни градове: {vm.CitiesVisited}.\n" +
                        (vm.BusiestMonth != null ? $"Най-натоварен месец: {vm.BusiestMonth} ({vm.BusiestMonthCount} събития).\n" : "") +
                        "Изведи САМО текста, без markdown, без кавички, без преамбюл.";
                    vm.AiSummary = await _ai.GenerateTextAsync(prompt, "wrapped");
                }
                catch
                {
                    // Silently degrade — AI summary is optional eye candy.
                }
            }

            return View(vm);
        }
    }
}

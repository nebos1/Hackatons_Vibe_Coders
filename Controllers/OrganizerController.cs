using EventsApp.Common;
using EventsApp.Data;
using EventsApp.Models;
using EventsApp.ViewModels.Events;
using EventsApp.ViewModels.Organizer;
using EventsApp.ViewModels.Posts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EventsApp.Controllers
{
    [Authorize(Roles = GlobalConstants.Roles.Admin + "," + GlobalConstants.Roles.Organizer)]
    public class OrganizerController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public OrganizerController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        public async Task<IActionResult> Dashboard()
        {
            var userId = _userManager.GetUserId(User)!;

            var orgData = await _db.OrganizerData
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.OrganizerId == userId);

            if (orgData == null)
            {
                return RedirectToAction(nameof(Profile));
            }

            var recentEvents = await _db.Events
                .AsNoTracking()
                .Where(e => e.OrganizerId == userId)
                .OrderByDescending(e => e.CreatedAt)
                .Take(5)
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

            var recentPosts = await _db.Posts
                .AsNoTracking()
                .Where(p => p.OrganizerId == userId)
                .OrderByDescending(p => p.CreatedAt)
                .Take(5)
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
                })
                .ToListAsync();

            var ticketTypesCount = await _db.Tickets
                .CountAsync(t => t.Event.OrganizerId == userId);

            var paid = GlobalConstants.TransactionStatuses.Paid;
            var soldQuery = _db.UserTickets
                .AsNoTracking()
                .Where(ut => ut.Ticket.Event.OrganizerId == userId
                             && ut.Transaction.Status == paid);

            var ticketsSoldCount = await soldQuery.CountAsync();
            var ticketsUsedCount = await soldQuery.CountAsync(ut => ut.IsUsed);
            var totalRevenue = await soldQuery.SumAsync(ut => (decimal?)ut.Ticket.Price) ?? 0m;
            var avgTicketPrice = ticketsSoldCount > 0 ? totalRevenue / ticketsSoldCount : 0m;

            var eventsWithTickets = await _db.Events
                .CountAsync(e => e.OrganizerId == userId && e.Tickets.Any(t => t.IsActive));

            var now = DateTime.UtcNow;
            var upcomingCount = await _db.Events
                .CountAsync(e => e.OrganizerId == userId && e.StartTime >= now);
            var pastCount = await _db.Events
                .CountAsync(e => e.OrganizerId == userId && e.EndTime < now);

            var totalLikes = await _db.EventLikes
                .CountAsync(l => l.Event.OrganizerId == userId);
            var totalComments = await _db.EventComments
                .CountAsync(c => c.Event.OrganizerId == userId);

            var since30 = now.AddDays(-29).Date;

            var dailyRaw = await soldQuery
                .Where(ut => ut.CreatedAt >= since30)
                .GroupBy(ut => ut.CreatedAt.Date)
                .Select(g => new
                {
                    Date = g.Key,
                    Sold = g.Count(),
                    Revenue = g.Sum(x => (decimal?)x.Ticket.Price) ?? 0m,
                })
                .ToListAsync();

            var dailyMap = dailyRaw.ToDictionary(d => d.Date, d => (d.Sold, d.Revenue));
            var salesSeries = new List<DailySalesPoint>(30);
            for (int i = 0; i < 30; i++)
            {
                var day = since30.AddDays(i);
                if (dailyMap.TryGetValue(day, out var v))
                    salesSeries.Add(new DailySalesPoint { Date = day, Sold = v.Sold, Revenue = v.Revenue });
                else
                    salesSeries.Add(new DailySalesPoint { Date = day, Sold = 0, Revenue = 0m });
            }
            var last30Sold = salesSeries.Sum(p => p.Sold);
            var last30Revenue = salesSeries.Sum(p => p.Revenue);

            var perEventStats = await _db.UserTickets
                .AsNoTracking()
                .Where(ut => ut.Ticket.Event.OrganizerId == userId
                             && ut.Transaction.Status == paid)
                .GroupBy(ut => new { ut.Ticket.EventId, ut.Ticket.Event.Title })
                .Select(g => new TopEventStat
                {
                    EventId = g.Key.EventId,
                    Title = g.Key.Title,
                    Sold = g.Count(),
                    Revenue = g.Sum(x => x.Ticket.Price),
                })
                .ToListAsync();

            var topByTicketsSold = perEventStats
                .OrderByDescending(s => s.Sold)
                .Take(5)
                .ToList();

            var topByRevenue = perEventStats
                .OrderByDescending(s => s.Revenue)
                .Take(5)
                .ToList();

            var genreBreakdown = await _db.Events
                .AsNoTracking()
                .Where(e => e.OrganizerId == userId)
                .GroupBy(e => e.Genre)
                .Select(g => new GenreCountStat { Genre = g.Key, Count = g.Count() })
                .OrderByDescending(g => g.Count)
                .ToListAsync();

            var cityBreakdown = await _db.Events
                .AsNoTracking()
                .Where(e => e.OrganizerId == userId)
                .GroupBy(e => e.City)
                .Select(g => new CityCountStat { City = g.Key, Count = g.Count() })
                .OrderByDescending(g => g.Count)
                .Take(8)
                .ToListAsync();

            var eventTicketRows = await _db.Events
                .AsNoTracking()
                .Where(e => e.OrganizerId == userId)
                .OrderByDescending(e => e.CreatedAt)
                .Take(8)
                .Select(e => new OrganizerEventTicketRowViewModel
                {
                    EventId = e.Id,
                    EventTitle = e.Title,
                    StartTime = e.StartTime,
                    HasActiveTickets = e.Tickets.Any(t => t.IsActive),
                    Sold = e.Tickets.SelectMany(t => t.UserTickets)
                        .Count(ut => ut.Transaction.Status == paid),
                })
                .ToListAsync();

            var vm = new OrganizerDashboardViewModel
            {
                HasProfile = true,
                OrganizationName = orgData.OrganizationName,
                Description = orgData.Description,
                PhoneNumber = orgData.PhoneNumber,
                Website = orgData.Website,
                CompanyNumber = orgData.CompanyNumber,
                Approved = orgData.Approved,
                EventsCount = await _db.Events.CountAsync(e => e.OrganizerId == userId),
                PostsCount = await _db.Posts.CountAsync(p => p.OrganizerId == userId),
                TicketTypesCount = ticketTypesCount,
                TicketsSoldCount = ticketsSoldCount,
                EventsWithTicketsCount = eventsWithTickets,
                UpcomingEventsCount = upcomingCount,
                PastEventsCount = pastCount,
                TicketsUsedCount = ticketsUsedCount,
                TotalLikes = totalLikes,
                TotalComments = totalComments,
                TotalRevenue = totalRevenue,
                AverageTicketPrice = avgTicketPrice,
                Last30DaysSold = last30Sold,
                Last30DaysRevenue = last30Revenue,
                TopByTicketsSold = topByTicketsSold,
                TopByRevenue = topByRevenue,
                GenreBreakdown = genreBreakdown,
                CityBreakdown = cityBreakdown,
                SalesLast30Days = salesSeries,
                RecentEvents = recentEvents,
                RecentPosts = recentPosts,
                EventTicketRows = eventTicketRows,
            };

            return View(vm);
        }

        [HttpGet]
        public async Task<IActionResult> Profile()
        {
            var userId = _userManager.GetUserId(User)!;
            var orgData = await _db.OrganizerData.AsNoTracking().FirstOrDefaultAsync(o => o.OrganizerId == userId);

            var vm = orgData == null
                ? new OrganizerProfileViewModel { OrganizationName = string.Empty }
                : new OrganizerProfileViewModel
                {
                    OrganizationName = orgData.OrganizationName,
                    Description = orgData.Description,
                    PhoneNumber = orgData.PhoneNumber,
                    Website = orgData.Website,
                    CompanyNumber = orgData.CompanyNumber,
                    Approved = orgData.Approved,
                };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Profile(OrganizerProfileViewModel input)
        {
            if (!ModelState.IsValid) return View(input);

            var userId = _userManager.GetUserId(User)!;
            var orgData = await _db.OrganizerData.FirstOrDefaultAsync(o => o.OrganizerId == userId);

            if (orgData == null)
            {
                _db.OrganizerData.Add(new OrganizerData
                {
                    OrganizerId = userId,
                    OrganizationName = input.OrganizationName,
                    Description = input.Description,
                    PhoneNumber = input.PhoneNumber,
                    Website = input.Website,
                    CompanyNumber = input.CompanyNumber,
                    Approved = false,
                });
            }
            else
            {
                orgData.OrganizationName = input.OrganizationName;
                orgData.Description = input.Description;
                orgData.PhoneNumber = input.PhoneNumber;
                orgData.Website = input.Website;
                orgData.CompanyNumber = input.CompanyNumber;
            }

            await _db.SaveChangesAsync();
            TempData["StatusMessage"] = "Profile saved.";
            return RedirectToAction(nameof(Dashboard));
        }
    }
}

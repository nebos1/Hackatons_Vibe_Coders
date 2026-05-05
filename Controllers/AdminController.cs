using EventsApp.Common;
using EventsApp.Data;
using EventsApp.Models;
using EventsApp.ViewModels.Admin;
using EventsApp.ViewModels.Events;
using EventsApp.ViewModels.Posts;
using EventsApp.ViewModels.Tickets;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EventsApp.Controllers
{
    [Authorize(Roles = GlobalConstants.Roles.Admin)]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public AdminController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var recentPosts = await _db.Posts
                .AsNoTracking()
                .OrderByDescending(p => p.CreatedAt)
                .Take(5)
                .Select(p => new PostCardViewModel
                {
                    Id = p.Id,
                    OrganizerId = p.OrganizerId,
                    OrganizerProfileId = p.OrganizerProfileId,
                    OrganizerName = p.OrganizerProfile != null ? p.OrganizerProfile.DisplayName : p.Organizer.UserName ?? string.Empty,
                    Content = p.Content,
                    CreatedAt = p.CreatedAt,
                    EventId = p.EventId,
                    EventTitle = p.Event != null ? p.Event.Title : null,
                    FirstMediaUrl = p.Images.Select(i => i.ImageUrl).FirstOrDefault(),
                    FirstMediaType = p.Images.Select(i => i.MediaType).FirstOrDefault(),
                    LikesCount = p.Likes.Count,
                    CommentsCount = p.Comments.Count,
                    SavesCount = p.Saves.Count,
                    AuthorImageUrl = p.Organizer.ProfileImageUrl,
                    AuthorIsOrganizer = p.Organizer.OrganizerData != null && p.Organizer.OrganizerData.Approved,
                })
                .ToListAsync();

            var vm = new AdminDashboardViewModel
            {
                UsersCount = await _db.Users.CountAsync(),
                OrganizersCount = await _db.OrganizerData.CountAsync(),
                EventsCount = await _db.Events.CountAsync(),
                PendingOrganizersCount = await _db.OrganizerData.CountAsync(o => !o.Approved),
                PendingEventsCount = await _db.Events.CountAsync(e => !e.IsApproved),
                RecentPosts = recentPosts,
            };

            return View(vm);
        }

        public async Task<IActionResult> Organizers()
        {
            var organizers = await _db.OrganizerData
                .AsNoTracking()
                .Include(o => o.Organizer)
                .OrderBy(o => o.Approved)
                .ThenBy(o => o.CreatedAt)
                .Select(o => new AdminOrganizerRowViewModel
                {
                    OrganizerId = o.OrganizerId,
                    UserName = o.Organizer.UserName ?? string.Empty,
                    Email = o.Organizer.Email ?? string.Empty,
                    OrganizationName = o.OrganizationName,
                    PhoneNumber = o.PhoneNumber,
                    Website = o.Website,
                    CompanyNumber = o.CompanyNumber,
                    Approved = o.Approved,
                    CreatedAt = o.CreatedAt,
                })
                .ToListAsync();

            return View(organizers);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveOrganizer(string id)
        {
            var org = await _db.OrganizerData.FirstOrDefaultAsync(o => o.OrganizerId == id);
            if (org == null) return NotFound();

            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            org.Approved = !org.Approved;
            await _db.SaveChangesAsync();

            if (org.Approved)
            {
                // Премахни User роля, добави Organizer роля
                if (await _userManager.IsInRoleAsync(user, GlobalConstants.Roles.User))
                    await _userManager.RemoveFromRoleAsync(user, GlobalConstants.Roles.User);
                if (!await _userManager.IsInRoleAsync(user, GlobalConstants.Roles.Organizer))
                    await _userManager.AddToRoleAsync(user, GlobalConstants.Roles.Organizer);

                TempData["StatusMessage"] = $"{user.UserName} approved as Organizer.";
            }
            else
            {
                // Премахни Organizer роля, върни User роля
                if (await _userManager.IsInRoleAsync(user, GlobalConstants.Roles.Organizer))
                    await _userManager.RemoveFromRoleAsync(user, GlobalConstants.Roles.Organizer);
                if (!await _userManager.IsInRoleAsync(user, GlobalConstants.Roles.User))
                    await _userManager.AddToRoleAsync(user, GlobalConstants.Roles.User);

                TempData["StatusMessage"] = $"{user.UserName} reverted to User.";
            }

            return RedirectToAction(nameof(Organizers));
        }

        public async Task<IActionResult> Events(bool? pending)
        {
            var query = _db.Events.AsNoTracking().AsQueryable();
            if (pending == true)
                query = query.Where(e => !e.IsApproved);

            var events = await query
                .OrderByDescending(e => e.CreatedAt)
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
                    HasActiveTickets = e.Tickets.Any(t => t.IsActive),
                    HasPaidTickets = e.Tickets.Any(t => t.IsActive && t.Price > 0m),
                    LowestPaidTicketPrice = e.Tickets
                        .Where(t => t.IsActive && t.Price > 0m)
                        .Min(t => (decimal?)t.Price),
                })
                .ToListAsync();

            ViewBag.PendingOnly = pending == true;
            return View(events);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveEvent(int id)
        {
            var ev = await _db.Events.FirstOrDefaultAsync(e => e.Id == id);
            if (ev == null) return NotFound();

            ev.IsApproved = !ev.IsApproved;
            await _db.SaveChangesAsync();
            TempData["StatusMessage"] = ev.IsApproved ? "Event approved." : "Event unapproved.";
            return RedirectToAction(nameof(Events));
        }

        public async Task<IActionResult> Posts()
        {
            var posts = await _db.Posts
                .AsNoTracking()
                .OrderByDescending(p => p.CreatedAt)
                .Select(p => new PostCardViewModel
                {
                    Id = p.Id,
                    OrganizerId = p.OrganizerId,
                    OrganizerProfileId = p.OrganizerProfileId,
                    OrganizerName = p.OrganizerProfile != null ? p.OrganizerProfile.DisplayName : p.Organizer.UserName ?? string.Empty,
                    Content = p.Content,
                    CreatedAt = p.CreatedAt,
                    EventId = p.EventId,
                    EventTitle = p.Event != null ? p.Event.Title : null,
                    FirstMediaUrl = p.Images.Select(i => i.ImageUrl).FirstOrDefault(),
                    FirstMediaType = p.Images.Select(i => i.MediaType).FirstOrDefault(),
                    LikesCount = p.Likes.Count,
                    CommentsCount = p.Comments.Count,
                    SavesCount = p.Saves.Count,
                    AuthorImageUrl = p.Organizer.ProfileImageUrl,
                    AuthorIsOrganizer = p.Organizer.OrganizerData != null && p.Organizer.OrganizerData.Approved,
                })
                .ToListAsync();

            return View(posts);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeletePost(int id)
        {
            var post = await _db.Posts.FirstOrDefaultAsync(p => p.Id == id);
            if (post == null) return NotFound();

            _db.Posts.Remove(post);
            await _db.SaveChangesAsync();
            TempData["StatusMessage"] = "Post deleted.";
            return RedirectToAction(nameof(Posts));
        }

        public async Task<IActionResult> Users()
        {
            var users = await _db.Users.AsNoTracking().OrderBy(u => u.UserName).ToListAsync();
            var model = new List<(ApplicationUser User, IList<string> Roles)>();
            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                model.Add((user, roles));
            }
            return View(model);
        }

        public async Task<IActionResult> Tickets()
        {
            var ticketTypesCount = await _db.Tickets.CountAsync();
            var soldCount = await _db.UserTickets.CountAsync();
            var usedCount = await _db.UserTickets.CountAsync(ut => ut.IsUsed);
            var unusedCount = soldCount - usedCount;

            var totalRevenue = await _db.Transactions
                .Where(t => t.Status == GlobalConstants.TransactionStatuses.Paid)
                .SumAsync(t => (decimal?)t.TotalAmount) ?? 0m;

            var recent = await _db.UserTickets
                .AsNoTracking()
                .OrderByDescending(ut => ut.CreatedAt)
                .Take(50)
                .Select(ut => new AdminTicketRowViewModel
                {
                    Id = ut.Id,
                    TicketName = ut.Ticket.Name,
                    EventTitle = ut.Ticket.Event.Title,
                    EventId = ut.Ticket.EventId,
                    OwnerUserName = ut.Transaction.User.UserName ?? string.Empty,
                    CreatedAt = ut.CreatedAt,
                    IsUsed = ut.IsUsed,
                    Price = ut.Ticket.Price,
                })
                .ToListAsync();

            return View(new AdminTicketsViewModel
            {
                TicketTypesCount = ticketTypesCount,
                TicketsSoldCount = soldCount,
                UsedTicketsCount = usedCount,
                UnusedTicketsCount = unusedCount,
                TotalRevenue = totalRevenue,
                Recent = recent,
            });
        }

        public async Task<IActionResult> Transactions()
        {
            var transactions = await _db.Transactions
                .AsNoTracking()
                .OrderByDescending(t => t.CreatedAt)
                .Select(t => new AdminTransactionRowViewModel
                {
                    Id = t.Id,
                    UserName = t.User.UserName ?? string.Empty,
                    Email = t.User.Email ?? string.Empty,
                    TotalAmount = t.TotalAmount,
                    Status = t.Status,
                    CreatedAt = t.CreatedAt,
                    TicketCount = t.UserTickets.Count,
                })
                .ToListAsync();

            var totalRevenue = transactions
                .Where(t => t.Status == GlobalConstants.TransactionStatuses.Paid)
                .Sum(t => t.TotalAmount);

            return View(new AdminTransactionsViewModel
            {
                Transactions = transactions,
                TotalRevenue = totalRevenue,
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetRole(string userId, string role)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound();

            var validRoles = new[] { GlobalConstants.Roles.User, GlobalConstants.Roles.Organizer, GlobalConstants.Roles.Admin };
            if (!validRoles.Contains(role)) return BadRequest();

            var currentRoles = await _userManager.GetRolesAsync(user);
            await _userManager.RemoveFromRolesAsync(user, currentRoles);
            await _userManager.AddToRoleAsync(user, role);

            TempData["StatusMessage"] = $"{user.UserName} is now {role}.";
            return RedirectToAction(nameof(Users));
        }
    }
}

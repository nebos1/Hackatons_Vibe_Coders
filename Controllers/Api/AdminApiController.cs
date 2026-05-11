using EventsApp.Common;
using EventsApp.Data;
using EventsApp.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EventsApp.Controllers.Api
{
    [ApiController]
    [Route("api/admin")]
    [IgnoreAntiforgeryToken]
    [Authorize(Policy = "ApiAuth")]
    public class AdminApiController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public AdminApiController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        private bool IsAdmin => User.IsInRole(GlobalConstants.Roles.Admin);

        // GET /api/admin/dashboard
        [HttpGet("dashboard")]
        public async Task<IActionResult> Dashboard()
        {
            if (!IsAdmin) return Forbid();

            return Ok(new
            {
                usersCount = await _db.Users.CountAsync(),
                organizersCount = await _db.OrganizerData.CountAsync(),
                eventsCount = await _db.Events.CountAsync(),
                pendingOrganizersCount = await _db.OrganizerData.CountAsync(o => !o.Approved),
                pendingEventsCount = await _db.Events.CountAsync(e => !e.IsApproved || e.ChangeRequests.Any(r => r.Status == EventChangeRequestStatus.Pending)),
                postsCount = await _db.Posts.CountAsync(),
                totalRevenue = await _db.Transactions.Where(t => t.Status == GlobalConstants.TransactionStatuses.Paid).SumAsync(t => (decimal?)t.TotalAmount) ?? 0m,
            });
        }

        // GET /api/admin/organizers
        [HttpGet("organizers")]
        public async Task<IActionResult> Organizers()
        {
            if (!IsAdmin) return Forbid();

            var rows = await _db.OrganizerData
                .AsNoTracking()
                .Include(o => o.Organizer)
                .OrderBy(o => o.Approved)
                .ThenBy(o => o.CreatedAt)
                .Select(o => new
                {
                    organizerId = o.OrganizerId,
                    userName = o.Organizer.UserName ?? string.Empty,
                    email = o.Organizer.Email ?? string.Empty,
                    organizationName = o.OrganizationName,
                    phoneNumber = o.PhoneNumber,
                    city = o.City,
                    country = o.Country,
                    website = o.Website,
                    companyNumber = o.CompanyNumber,
                    approved = o.Approved,
                    createdAt = o.CreatedAt,
                })
                .ToListAsync();

            return Ok(rows);
        }

        // GET /api/admin/posts
        [HttpGet("posts")]
        public async Task<IActionResult> Posts()
        {
            if (!IsAdmin) return Forbid();

            var rows = await _db.Posts
                .AsNoTracking()
                .Include(p => p.Organizer)
                .Include(p => p.OrganizerProfile)
                .Include(p => p.Images)
                .OrderByDescending(p => p.CreatedAt)
                .Take(200)
                .Select(p => new
                {
                    id = p.Id,
                    content = p.Content,
                    authorName = p.OrganizerProfile != null ? p.OrganizerProfile.DisplayName : p.Organizer.UserName ?? string.Empty,
                    mediaUrl = p.Images.Select(i => i.ImageUrl).FirstOrDefault(),
                    mediaType = p.Images.Select(i => i.MediaType.ToString()).FirstOrDefault(),
                    likesCount = p.Likes.Count,
                    commentsCount = p.Comments.Count,
                    createdAt = p.CreatedAt,
                })
                .ToListAsync();

            return Ok(rows);
        }

        // GET /api/admin/transactions
        [HttpGet("transactions")]
        public async Task<IActionResult> Transactions()
        {
            if (!IsAdmin) return Forbid();

            var rows = await _db.Transactions
                .AsNoTracking()
                .Include(t => t.User)
                .Include(t => t.UserTickets)
                    .ThenInclude(ut => ut.Ticket)
                    .ThenInclude(t => t.Event)
                .OrderByDescending(t => t.CreatedAt)
                .Take(200)
                .Select(t => new
                {
                    id = t.Id,
                    userName = t.User.UserName ?? string.Empty,
                    userEmail = t.User.Email ?? string.Empty,
                    totalAmount = t.TotalAmount,
                    status = t.Status,
                    createdAt = t.CreatedAt,
                    ticketsCount = t.UserTickets.Count,
                    eventTitle = t.UserTickets.Select(ut => ut.Ticket.Event.Title).FirstOrDefault(),
                })
                .ToListAsync();

            return Ok(rows);
        }

        // GET /api/admin/tickets
        [HttpGet("tickets")]
        public async Task<IActionResult> Tickets()
        {
            if (!IsAdmin) return Forbid();

            var rows = await _db.UserTickets
                .AsNoTracking()
                .Include(ut => ut.Ticket).ThenInclude(t => t.Event)
                .Include(ut => ut.Transaction).ThenInclude(t => t.User)
                .OrderByDescending(ut => ut.CreatedAt)
                .Take(300)
                .Select(ut => new
                {
                    id = ut.Id,
                    eventId = ut.Ticket.EventId,
                    eventTitle = ut.Ticket.Event.Title,
                    ticketName = ut.Ticket.Name,
                    ownerName = ut.Transaction.User.UserName ?? string.Empty,
                    ownerEmail = ut.Transaction.User.Email ?? string.Empty,
                    pricePaid = ut.PricePaid,
                    isUsed = ut.IsUsed,
                    createdAt = ut.CreatedAt,
                    usedAt = ut.UsedAt,
                })
                .ToListAsync();

            return Ok(rows);
        }

        // POST /api/admin/organizers/{id}/approve
        [HttpPost("organizers/{id}/approve")]
        public async Task<IActionResult> ApproveOrganizer(string id)
        {
            if (!IsAdmin) return Forbid();

            var org = await _db.OrganizerData.FirstOrDefaultAsync(o => o.OrganizerId == id);
            if (org == null) return NotFound();

            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            var wasApproved = org.Approved;
            org.Approved = !org.Approved;

            if (!wasApproved && org.Approved && !org.FirstApprovalBoostGranted)
            {
                org.VipBoostCreditsAvailable = Math.Max(1, org.VipBoostCreditsAvailable);
                org.FirstApprovalBoostGranted = true;
                org.FirstApprovalBoostNoticeSeen = false;
            }
            await _db.SaveChangesAsync();

            if (org.Approved)
            {
                if (await _userManager.IsInRoleAsync(user, GlobalConstants.Roles.User))
                    await _userManager.RemoveFromRoleAsync(user, GlobalConstants.Roles.User);
                if (!await _userManager.IsInRoleAsync(user, GlobalConstants.Roles.Organizer))
                    await _userManager.AddToRoleAsync(user, GlobalConstants.Roles.Organizer);
            }
            else
            {
                if (await _userManager.IsInRoleAsync(user, GlobalConstants.Roles.Organizer))
                    await _userManager.RemoveFromRoleAsync(user, GlobalConstants.Roles.Organizer);
                if (!await _userManager.IsInRoleAsync(user, GlobalConstants.Roles.User))
                    await _userManager.AddToRoleAsync(user, GlobalConstants.Roles.User);
            }

            return Ok(new { approved = org.Approved });
        }

        // GET /api/admin/events?pending=true
        [HttpGet("events")]
        public async Task<IActionResult> Events([FromQuery] bool? pending)
        {
            if (!IsAdmin) return Forbid();

            var q = _db.Events.AsNoTracking().AsQueryable();
            if (pending == true)
                q = q.Where(e => !e.IsApproved || e.ChangeRequests.Any(r => r.Status == EventChangeRequestStatus.Pending));

            var events = await q
                .OrderByDescending(e => e.CreatedAt)
                .Select(e => new
                {
                    id = e.Id,
                    title = e.Title,
                    city = e.City,
                    startTime = e.StartTime,
                    isApproved = e.IsApproved,
                    hasPendingChanges = e.ChangeRequests.Any(r => r.Status == EventChangeRequestStatus.Pending),
                    organizerName = e.OrganizerProfile != null ? e.OrganizerProfile.DisplayName : e.Organizer.UserName ?? string.Empty,
                    genre = e.Genre.ToString(),
                    imageUrl = e.ImageUrl,
                    likesCount = e.Likes.Count,
                    createdAt = e.CreatedAt,
                })
                .ToListAsync();

            return Ok(events);
        }

        // POST /api/admin/events/{id}/approve
        [HttpPost("events/{id:int}/approve")]
        public async Task<IActionResult> ApproveEvent(int id)
        {
            if (!IsAdmin) return Forbid();

            var ev = await _db.Events.FirstOrDefaultAsync(e => e.Id == id);
            if (ev == null) return NotFound();

            ev.IsApproved = !ev.IsApproved;
            await _db.SaveChangesAsync();

            return Ok(new { isApproved = ev.IsApproved });
        }

        // GET /api/admin/users
        [HttpGet("users")]
        public async Task<IActionResult> Users()
        {
            if (!IsAdmin) return Forbid();

            var users = await _db.Users.AsNoTracking().OrderBy(u => u.UserName).ToListAsync();
            var result = new List<object>();
            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                result.Add(new
                {
                    id = user.Id,
                    userName = user.UserName ?? string.Empty,
                    email = user.Email ?? string.Empty,
                    firstName = user.FirstName,
                    lastName = user.LastName,
                    profileImageUrl = user.ProfileImageUrl,
                    roles = roles.ToArray(),
                    createdAt = user.LockoutEnd,
                });
            }
            return Ok(result);
        }

        // POST /api/admin/users/{id}/role
        [HttpPost("users/{id}/role")]
        public async Task<IActionResult> SetRole(string id, [FromBody] SetRoleDto dto)
        {
            if (!IsAdmin) return Forbid();

            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            var valid = new[] { GlobalConstants.Roles.User, GlobalConstants.Roles.Organizer, GlobalConstants.Roles.Admin };
            if (!valid.Contains(dto.Role)) return BadRequest(new { error = "Невалидна роля." });

            var current = await _userManager.GetRolesAsync(user);
            await _userManager.RemoveFromRolesAsync(user, current);
            await _userManager.AddToRoleAsync(user, dto.Role);

            return Ok(new { role = dto.Role });
        }

        // DELETE /api/admin/users/{id}
        [HttpDelete("users/{id}")]
        public async Task<IActionResult> DeleteUser(string id)
        {
            if (!IsAdmin) return Forbid();

            var currentAdminId = _userManager.GetUserId(User);
            if (id == currentAdminId) return BadRequest(new { error = "Не можеш да изтриеш собствения си акаунт." });

            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            if (await _userManager.IsInRoleAsync(user, GlobalConstants.Roles.Admin))
            {
                var admins = await _userManager.GetUsersInRoleAsync(GlobalConstants.Roles.Admin);
                if (admins.Count <= 1) return BadRequest(new { error = "Не можеш да изтриеш последния администратор." });
            }

            var result = await _userManager.DeleteAsync(user);
            if (!result.Succeeded) return BadRequest(new { error = result.Errors.FirstOrDefault()?.Description });

            return Ok(new { message = "Потребителят е изтрит." });
        }

        // POST /api/admin/posts/{id}/delete
        [HttpDelete("posts/{id:int}")]
        public async Task<IActionResult> DeletePost(int id)
        {
            if (!IsAdmin) return Forbid();

            var post = await _db.Posts.FirstOrDefaultAsync(p => p.Id == id);
            if (post == null) return NotFound();

            _db.Posts.Remove(post);
            await _db.SaveChangesAsync();

            return Ok(new { message = "Публикацията е изтрита." });
        }
    }
}

public record SetRoleDto(string Role);

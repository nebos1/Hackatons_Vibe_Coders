using EventsApp.Common;
using EventsApp.Data;
using EventsApp.Models;
using EventsApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EventsApp.Controllers.Api
{
    [ApiController]
    [Route("api/layouts")]
    [Authorize(Roles = GlobalConstants.Roles.Admin + "," + GlobalConstants.Roles.Organizer)]
    public class LayoutsApiController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILayoutService _layouts;

        public LayoutsApiController(
            ApplicationDbContext db,
            UserManager<ApplicationUser> userManager,
            ILayoutService layouts)
        {
            _db = db;
            _userManager = userManager;
            _layouts = layouts;
        }

        [HttpGet]
        public async Task<IActionResult> Mine()
        {
            var userId = _userManager.GetUserId(User)!;
            var isAdmin = User.IsInRole(GlobalConstants.Roles.Admin);

            var query = _db.VenueLayouts.AsNoTracking();
            if (!isAdmin)
            {
                query = query.Where(l => l.OrganizerId == userId);
            }

            var layouts = await query
                .OrderBy(l => l.VenueName)
                .ThenBy(l => l.Name)
                .Select(l => new
                {
                    l.Id,
                    l.VenueName,
                    l.Name,
                    l.Version,
                    Status = l.Status.ToString(),
                    Seats = l.Seats.Count,
                })
                .ToListAsync();

            return Ok(layouts);
        }

        [HttpPost("{layoutId:int}/duplicate")]
        public async Task<IActionResult> Duplicate(int layoutId)
        {
            var copy = await _layouts.DuplicateLayoutAsync(
                layoutId,
                _userManager.GetUserId(User)!,
                User.IsInRole(GlobalConstants.Roles.Admin));

            return copy == null
                ? NotFound(new { ok = false })
                : Ok(new { ok = true, id = copy.Id });
        }

        [HttpPost("{layoutId:int}/assign/{eventId:int}")]
        public async Task<IActionResult> Assign(int layoutId, int eventId)
        {
            var userId = _userManager.GetUserId(User)!;
            var isAdmin = User.IsInRole(GlobalConstants.Roles.Admin);

            var layout = await _db.VenueLayouts.FirstOrDefaultAsync(l => l.Id == layoutId);
            var ev = await _db.Events.FirstOrDefaultAsync(e => e.Id == eventId);
            if (layout == null || ev == null) return NotFound(new { ok = false });
            if (!isAdmin && (layout.OrganizerId != userId || ev.OrganizerId != userId)) return Forbid();

            ev.VenueLayoutId = layout.Id;
            ev.TicketingMode = EventTicketingMode.SeatedLayout;
            await _db.SaveChangesAsync();
            return Ok(new { ok = true });
        }
    }
}

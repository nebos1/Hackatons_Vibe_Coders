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
    [Route("api/event-series")]
    public class EventSeriesApiController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IRecurringEventService _recurringEvents;

        public EventSeriesApiController(
            ApplicationDbContext db,
            UserManager<ApplicationUser> userManager,
            IRecurringEventService recurringEvents)
        {
            _db = db;
            _userManager = userManager;
            _recurringEvents = recurringEvents;
        }

        [HttpGet("{seriesId:int}/occurrences")]
        public async Task<IActionResult> Occurrences(int seriesId)
        {
            var items = await _db.EventOccurrences
                .AsNoTracking()
                .Where(o => o.EventSeriesId == seriesId)
                .OrderBy(o => o.StartDateTime)
                .Select(o => new
                {
                    o.Id,
                    o.StartDateTime,
                    o.EndDateTime,
                    Status = o.Status.ToString(),
                    o.CapacityOverride,
                    o.PriceOverride,
                })
                .ToListAsync();

            return Ok(items);
        }

        [HttpPost("occurrences/{occurrenceId:int}/cancel")]
        [Authorize(Roles = GlobalConstants.Roles.Admin + "," + GlobalConstants.Roles.Organizer)]
        public async Task<IActionResult> Cancel(int occurrenceId)
        {
            var userId = _userManager.GetUserId(User)!;
            var ok = await _recurringEvents.CancelOccurrenceAsync(
                occurrenceId,
                userId,
                User.IsInRole(GlobalConstants.Roles.Admin));

            return ok ? Ok(new { ok = true }) : NotFound(new { ok = false });
        }
    }
}

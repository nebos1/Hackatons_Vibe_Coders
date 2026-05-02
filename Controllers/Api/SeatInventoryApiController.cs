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
    [Route("api/seat-inventory")]
    public class SeatInventoryApiController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ISeatReservationService _seatReservations;

        public SeatInventoryApiController(
            ApplicationDbContext db,
            UserManager<ApplicationUser> userManager,
            ISeatReservationService seatReservations)
        {
            _db = db;
            _userManager = userManager;
            _seatReservations = seatReservations;
        }

        [HttpGet("event/{eventId:int}")]
        public async Task<IActionResult> ForEvent(int eventId, int? occurrenceId)
        {
            await _seatReservations.ReleaseExpiredReservationsAsync();

            var query = _db.EventSeatInventories
                .AsNoTracking()
                .Include(i => i.Seat)
                    .ThenInclude(s => s.Section)
                .Where(i => occurrenceId.HasValue
                    ? i.EventOccurrenceId == occurrenceId.Value
                    : i.EventId == eventId && i.EventOccurrenceId == null);

            var seats = await query
                .Select(i => new
                {
                    i.Id,
                    i.SeatId,
                    Label = i.Seat.Row + i.Seat.Number,
                    Section = i.Seat.Section.Name,
                    i.Seat.Section.PriceModifier,
                    Status = i.Status.ToString(),
                    i.ReservedUntil,
                })
                .ToListAsync();

            return Ok(seats);
        }

        [HttpPost("reserve")]
        [Authorize]
        public async Task<IActionResult> Reserve(SeatReservationRequest request)
        {
            var userId = _userManager.GetUserId(User)!;
            var reserved = await _seatReservations.ReserveSeatAsync(
                request.EventId,
                request.EventOccurrenceId,
                request.SeatId,
                userId,
                TimeSpan.FromMinutes(10));

            return reserved == null
                ? Conflict(new { ok = false, message = "Seat is not available." })
                : Ok(new { ok = true, reserved.Id, reserved.ReservedUntil });
        }

        [HttpPost("release")]
        [Authorize]
        public async Task<IActionResult> Release(SeatReleaseRequest request)
        {
            var ok = await _seatReservations.ReleaseReservationAsync(
                request.InventoryId,
                _userManager.GetUserId(User)!,
                User.IsInRole(GlobalConstants.Roles.Admin));

            return ok ? Ok(new { ok = true }) : NotFound(new { ok = false });
        }
    }

    public class SeatReservationRequest
    {
        public int EventId { get; set; }
        public int? EventOccurrenceId { get; set; }
        public int SeatId { get; set; }
    }

    public class SeatReleaseRequest
    {
        public int InventoryId { get; set; }
    }
}

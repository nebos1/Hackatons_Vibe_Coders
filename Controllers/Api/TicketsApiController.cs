using EventsApp.Data;
using EventsApp.Models;
using EventsApp.Services;
using EventsApp.ViewModels.Tickets;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EventsApp.Controllers.Api
{
    [ApiController]
    [Route("api/tickets")]
    [IgnoreAntiforgeryToken]
    [Authorize(Policy = "ApiAuth")]
    public class TicketsApiController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ITicketDocumentService _docs;

        public TicketsApiController(ApplicationDbContext db, UserManager<ApplicationUser> userManager, ITicketDocumentService docs)
        {
            _db = db;
            _userManager = userManager;
            _docs = docs;
        }

        // GET /api/tickets/mine
        [HttpGet("mine")]
        public async Task<IActionResult> Mine()
        {
            var userId = _userManager.GetUserId(User)!;

            var tickets = await _db.UserTickets
                .AsNoTracking()
                .Where(ut => ut.Transaction.UserId == userId)
                .Include(ut => ut.Ticket)
                    .ThenInclude(t => t.Event)
                .OrderByDescending(ut => ut.CreatedAt)
                .ToListAsync();

            return Ok(tickets.Select(ut => new
            {
                id = ut.Id,
                eventId = ut.Ticket.EventId,
                eventTitle = ut.Ticket.Event.Title,
                eventStartTime = ut.Ticket.Event.StartTime,
                eventAddress = ut.Ticket.Event.Address,
                eventCity = ut.Ticket.Event.City,
                ticketType = ut.Ticket.Name,
                qrCodeUrl = $"/api/tickets/{ut.Id}/qr",
                pdfUrl = $"/api/tickets/{ut.Id}/pdf",
                isUsed = ut.IsUsed,
                purchasedAt = ut.CreatedAt,
            }));
        }

        // GET /api/tickets/{id}
        [HttpGet("{id:guid}")]
        public async Task<IActionResult> Details(Guid id)
        {
            var userId = _userManager.GetUserId(User)!;

            var ut = await _db.UserTickets
                .AsNoTracking()
                .Where(t => t.Id == id)
                .Include(t => t.Ticket)
                    .ThenInclude(t => t.Event)
                .Include(t => t.Transaction)
                .FirstOrDefaultAsync();

            if (ut == null) return NotFound();
            if (ut.Transaction.UserId != userId && !User.IsInRole("Admin")) return Forbid();

            return Ok(new
            {
                id = ut.Id,
                eventId = ut.Ticket.EventId,
                eventTitle = ut.Ticket.Event.Title,
                eventStartTime = ut.Ticket.Event.StartTime,
                eventAddress = ut.Ticket.Event.Address,
                eventCity = ut.Ticket.Event.City,
                ticketType = ut.Ticket.Name,
                qrCodeUrl = $"/api/tickets/{ut.Id}/qr",
                pdfUrl = $"/api/tickets/{ut.Id}/pdf",
                isUsed = ut.IsUsed,
                purchasedAt = ut.CreatedAt,
            });
        }

        // GET /api/tickets/{id}/qr
        [HttpGet("{id:guid}/qr")]
        public async Task<IActionResult> Qr(Guid id)
        {
            var userId = _userManager.GetUserId(User)!;
            var ut = await _db.UserTickets
                .AsNoTracking()
                .Include(x => x.Ticket).ThenInclude(t => t.Event)
                .Include(x => x.Transaction)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (ut == null) return NotFound();
            if (!await CanAccessTicketAsync(userId, ut)) return Forbid();
            if (string.IsNullOrWhiteSpace(ut.QrCode)) return NotFound();

            return File(_docs.GenerateQrPng(ut.QrCode), "image/png");
        }

        // GET /api/tickets/{id}/pdf
        [HttpGet("{id:guid}/pdf")]
        public async Task<IActionResult> Pdf(Guid id)
        {
            var userId = _userManager.GetUserId(User)!;
            var ut = await TicketDetailsQuery().FirstOrDefaultAsync(x => x.Id == id);

            if (ut == null) return NotFound();
            if (!await CanAccessTicketAsync(userId, ut)) return Forbid();

            var vm = ToDetails(ut);
            return File(_docs.GenerateTicketPdf(vm), "application/pdf", $"Evento-Ticket-{ut.Id}.pdf");
        }

        // POST /api/tickets/validate
        [HttpPost("validate")]
        public async Task<IActionResult> Validate([FromBody] ValidateTicketDto dto)
        {
            var userId = _userManager.GetUserId(User)!;
            var isAdmin = User.IsInRole("Admin");
            var code = (dto.QrCode ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(code))
                return BadRequest(new { valid = false, message = "Въведи или сканирай QR кода на билета." });

            var ut = await TicketDetailsQuery()
                .Include(x => x.UsedByOrganizer)
                .FirstOrDefaultAsync(x => x.QrCode == code);

            if (ut == null)
                return NotFound(new { valid = false, notFound = true, message = "Не е намерен билет с този QR код." });

            if (!isAdmin && !await CanValidateEventAsync(userId, ut.Ticket.Event))
                return Forbid();

            var details = ToDetails(ut);
            if (ut.IsUsed)
            {
                return Ok(new
                {
                    valid = false,
                    alreadyUsed = true,
                    message = $"Билетът вече е използван на {ut.UsedAt:dd.MM.yyyy HH:mm}.",
                    ticket = details,
                });
            }

            if (ut.EventOccurrence?.Status == EventOccurrenceStatus.Cancelled)
            {
                return Ok(new { valid = false, message = "Билетът е за отменена дата.", ticket = details });
            }

            if (!dto.Confirm)
            {
                return Ok(new
                {
                    valid = false,
                    requiresConfirmation = true,
                    message = "Билетът е намерен. Провери данните и потвърди валидирането.",
                    ticket = details,
                });
            }

            ut.IsUsed = true;
            ut.UsedAt = DateTime.UtcNow;
            ut.UsedByOrganizerId = userId;
            await _db.SaveChangesAsync();

            return Ok(new
            {
                valid = true,
                message = "Билетът е валидиран успешно.",
                ticket = ToDetails(ut),
            });
        }

        private IQueryable<UserTicket> TicketDetailsQuery()
            => _db.UserTickets
                .Include(x => x.Ticket).ThenInclude(t => t.Event)
                .Include(x => x.Ticket).ThenInclude(t => t.SectionPrices)
                .Include(x => x.EventOccurrence)
                .Include(x => x.Seat).ThenInclude(s => s!.Section)
                .Include(x => x.Transaction).ThenInclude(t => t.User);

        private async Task<bool> CanAccessTicketAsync(string userId, UserTicket ut)
        {
            if (User.IsInRole("Admin")) return true;
            if (ut.Transaction.UserId == userId) return true;
            if (ut.Ticket.Event.OrganizerId == userId) return true;
            return await CanValidateEventAsync(userId, ut.Ticket.Event);
        }

        private async Task<bool> CanValidateEventAsync(string userId, Event ev)
        {
            if (ev.OrganizerId == userId) return true;
            if (!ev.OrganizerProfileId.HasValue) return false;

            return await _db.OrganizerValidatorAssignments
                .AsNoTracking()
                .AnyAsync(a =>
                    a.OrganizerId == ev.OrganizerId &&
                    a.ValidatorUserId == userId &&
                    a.IsActive &&
                    a.OrganizerProfileId == ev.OrganizerProfileId.Value);
        }

        private static UserTicketDetailsViewModel ToDetails(UserTicket ut)
        {
            return new UserTicketDetailsViewModel
            {
                Id = ut.Id,
                TicketId = ut.TicketId,
                TransactionId = ut.TransactionId,
                TicketName = ut.Ticket.Name,
                EventTitle = ut.Ticket.Event.Title,
                EventId = ut.Ticket.EventId,
                EventOccurrenceId = ut.EventOccurrenceId,
                Address = ut.Ticket.Event.Address,
                City = ut.Ticket.Event.City,
                StartTime = ut.EventOccurrence?.StartDateTime ?? ut.Ticket.Event.StartTime,
                EndTime = ut.EventOccurrence?.EndDateTime ?? ut.Ticket.Event.EndTime,
                SeatLabel = ut.Seat != null ? GetSeatLabel(ut.Seat) : null,
                AttendeeName = ut.AttendeeName,
                PurchaseGroupId = ut.PurchaseGroupId,
                IsPrimaryInPurchase = ut.IsPrimaryInPurchase,
                Price = ut.PricePaid > 0 ? ut.PricePaid : ut.Ticket.Price,
                TransactionStatus = ut.Transaction.Status,
                QrCode = ut.QrCode,
                IsUsed = ut.IsUsed,
                CreatedAt = ut.CreatedAt,
                UsedAt = ut.UsedAt,
                UsedByOrganizerName = ut.UsedByOrganizer?.UserName,
                OwnerUserName = ut.Transaction.User.UserName ?? string.Empty,
                OwnerEmail = ut.Transaction.User.Email ?? string.Empty,
            };
        }

        private static string GetSeatLabel(Seat seat)
            => string.IsNullOrWhiteSpace(seat.Label) ? seat.Row + seat.Number : seat.Label;
    }
}

public record ValidateTicketDto(string? QrCode, bool Confirm);

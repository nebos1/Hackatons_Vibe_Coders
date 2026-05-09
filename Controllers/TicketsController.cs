using EventsApp.Common;
using EventsApp.Data;
using EventsApp.Models;
using EventsApp.Services;
using EventsApp.ViewModels.Tickets;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Net;
using System.Text;

namespace EventsApp.Controllers
{
    [Authorize]
    public class TicketsController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ITicketDocumentService _docs;
        private readonly IMediaUploadService _mediaUpload;
        private readonly ISeatReservationService _seatReservations;
        private readonly IEmailSender _emailSender;
        private readonly IAppLinkService _appLinks;
        private readonly ILogger<TicketsController> _logger;

        public TicketsController(
            ApplicationDbContext db,
            UserManager<ApplicationUser> userManager,
            ITicketDocumentService docs,
            IMediaUploadService mediaUpload,
            ISeatReservationService seatReservations,
            IEmailSender emailSender,
            IAppLinkService appLinks,
            ILogger<TicketsController> logger)
        {
            _db = db;
            _userManager = userManager;
            _docs = docs;
            _mediaUpload = mediaUpload;
            _seatReservations = seatReservations;
            _emailSender = emailSender;
            _appLinks = appLinks;
            _logger = logger;
        }

        // ---------- ORGANIZER MANAGEMENT ----------

        [Authorize(Roles = GlobalConstants.Roles.Admin + "," + GlobalConstants.Roles.Organizer)]
        public async Task<IActionResult> Manage(int id)
        {
            var userId = _userManager.GetUserId(User)!;
            var isAdmin = User.IsInRole(GlobalConstants.Roles.Admin);

            var ev = await _db.Events
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == id);

            if (ev == null) return NotFound();
            if (!isAdmin && ev.OrganizerId != userId) return Forbid();

            var tickets = await _db.Tickets
                .AsNoTracking()
                .Where(t => t.EventId == id)
                .OrderByDescending(t => t.CreatedAt)
                .Select(t => new TicketRowViewModel
                {
                    Id = t.Id,
                    Name = t.Name,
                    Price = t.Price,
                    QuantityTotal = t.QuantityTotal,
                    QuantityRemaining = t.QuantityRemaining,
                    IsActive = t.IsActive,
                    RequiresAttendeeNames = t.RequiresAttendeeNames,
                    Sold = t.UserTickets.Count,
                    Used = t.UserTickets.Count(ut => ut.IsUsed),
                })
                .ToListAsync();

            var vm = new TicketManageViewModel
            {
                EventId = ev.Id,
                EventTitle = ev.Title,
                Tickets = tickets,
            };

            return View(vm);
        }

        [Authorize(Roles = GlobalConstants.Roles.Admin + "," + GlobalConstants.Roles.Organizer)]
        public async Task<IActionResult> Create(int id)
        {
            var userId = _userManager.GetUserId(User)!;
            var isAdmin = User.IsInRole(GlobalConstants.Roles.Admin);

            var ev = await _db.Events
                .AsNoTracking()
                .Include(e => e.VenueLayout)
                    .ThenInclude(l => l!.Sections)
                        .ThenInclude(s => s.Seats)
                .Include(e => e.VenueLayout)
                    .ThenInclude(l => l!.Seats)
                .FirstOrDefaultAsync(e => e.Id == id);
            if (ev == null) return NotFound();
            if (!isAdmin && ev.OrganizerId != userId) return Forbid();

            var vm = new TicketCreateEditViewModel
            {
                EventId = ev.Id,
                EventTitle = ev.Title,
                Name = ev.VenueLayoutId.HasValue && ev.TicketingMode != EventTicketingMode.GeneralAdmission
                    ? "Билет по места"
                    : null!,
                Price = 0,
                QuantityTotal = ev.VenueLayout?.Seats.Count(s => s.Status == LayoutSeatStatus.Active) ?? 0,
                QuantityRemaining = ev.VenueLayout?.Seats.Count(s => s.Status == LayoutSeatStatus.Active) ?? 0,
                IsActive = true,
                IsFree = false,
                RequiresAttendeeNames = false,
            };
            PopulateTicketLayoutPricing(vm, ev);

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = GlobalConstants.Roles.Admin + "," + GlobalConstants.Roles.Organizer)]
        public async Task<IActionResult> Create(TicketCreateEditViewModel input)
        {
            var userId = _userManager.GetUserId(User)!;
            var isAdmin = User.IsInRole(GlobalConstants.Roles.Admin);

            var ev = await _db.Events
                .Include(e => e.VenueLayout)
                    .ThenInclude(l => l!.Sections)
                        .ThenInclude(s => s.Seats)
                .Include(e => e.VenueLayout)
                    .ThenInclude(l => l!.Seats)
                .FirstOrDefaultAsync(e => e.Id == input.EventId);
            if (ev == null) return NotFound();
            if (!isAdmin && ev.OrganizerId != userId) return Forbid();

            input.EventTitle = ev.Title;
            PopulateTicketLayoutPricing(input, ev, useSubmittedPrices: true);
            NormalizeFreeTicketInput(input);
            ValidateLayoutSectionPrices(input);

            if (!ModelState.IsValid)
            {
                return View(input);
            }

            var ticket = new Ticket
            {
                EventId = ev.Id,
                Name = input.Name.Trim(),
                Description = string.IsNullOrWhiteSpace(input.Description) ? null : input.Description.Trim(),
                Price = input.Price,
                QuantityTotal = input.QuantityTotal,
                QuantityRemaining = input.QuantityRemaining ?? input.QuantityTotal,
                IsActive = input.IsActive,
                RequiresAttendeeNames = input.RequiresAttendeeNames,
            };

            await ApplyTicketImageAsync(ticket, input);
            ApplyTicketSectionPrices(ticket, input);

            _db.Tickets.Add(ticket);
            await _db.SaveChangesAsync();

            TempData["StatusMessage"] = "Типът билет е създаден.";
            return RedirectToAction(nameof(Manage), new { id = ev.Id });
        }

        [Authorize(Roles = GlobalConstants.Roles.Admin + "," + GlobalConstants.Roles.Organizer)]
        public async Task<IActionResult> Edit(Guid id)
        {
            var userId = _userManager.GetUserId(User)!;
            var isAdmin = User.IsInRole(GlobalConstants.Roles.Admin);

            var ticket = await _db.Tickets
                .Include(t => t.Event)
                    .ThenInclude(e => e.EventSeries)
                .Include(t => t.Event)
                    .ThenInclude(e => e.VenueLayout)
                        .ThenInclude(l => l!.Sections)
                            .ThenInclude(s => s.Seats)
                .Include(t => t.Event)
                    .ThenInclude(e => e.VenueLayout)
                        .ThenInclude(l => l!.Seats)
                .Include(t => t.SectionPrices)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (ticket == null) return NotFound();
            if (!isAdmin && ticket.Event.OrganizerId != userId) return Forbid();

            var vm = new TicketCreateEditViewModel
            {
                Id = ticket.Id,
                EventId = ticket.EventId,
                EventTitle = ticket.Event.Title,
                Name = ticket.Name,
                Description = ticket.Description,
                Price = ticket.Price,
                IsFree = ticket.Price.IsFreeTicket(),
                QuantityTotal = ticket.QuantityTotal,
                QuantityRemaining = ticket.QuantityRemaining,
                ImageUrl = ticket.ImageUrl,
                IsActive = ticket.IsActive,
                RequiresAttendeeNames = ticket.RequiresAttendeeNames,
            };
            PopulateTicketLayoutPricing(vm, ticket.Event, ticket.SectionPrices);

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = GlobalConstants.Roles.Admin + "," + GlobalConstants.Roles.Organizer)]
        public async Task<IActionResult> Edit(Guid id, TicketCreateEditViewModel input)
        {
            var userId = _userManager.GetUserId(User)!;
            var isAdmin = User.IsInRole(GlobalConstants.Roles.Admin);

            var ticket = await _db.Tickets
                .Include(t => t.Event)
                    .ThenInclude(e => e.OrganizerProfile)
                .Include(t => t.Event)
                    .ThenInclude(e => e.VenueLayout)
                        .ThenInclude(l => l!.Sections)
                            .ThenInclude(s => s.Seats)
                .Include(t => t.Event)
                    .ThenInclude(e => e.VenueLayout)
                        .ThenInclude(l => l!.Seats)
                .Include(t => t.Event)
                    .ThenInclude(e => e.EventSeries)
                        .ThenInclude(s => s!.Occurrences)
                .Include(t => t.SectionPrices)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (ticket == null) return NotFound();
            if (!isAdmin && ticket.Event.OrganizerId != userId) return Forbid();

            var remaining = input.QuantityRemaining ?? ticket.QuantityRemaining;
            NormalizeFreeTicketInput(input);
            PopulateTicketLayoutPricing(input, ticket.Event, useSubmittedPrices: true);
            ValidateLayoutSectionPrices(input);
            if (remaining > input.QuantityTotal)
            {
                ModelState.AddModelError(nameof(input.QuantityRemaining),
                    "Оставащите билети не могат да са повече от общото количество.");
            }

            if (!ModelState.IsValid)
            {
                input.EventTitle = ticket.Event.Title;
                input.EventId = ticket.EventId;
                return View(input);
            }

            ticket.Name = input.Name.Trim();
            ticket.Description = string.IsNullOrWhiteSpace(input.Description) ? null : input.Description.Trim();
            ticket.Price = input.Price;
            ticket.QuantityTotal = input.QuantityTotal;
            ticket.QuantityRemaining = remaining;
            await ApplyTicketImageAsync(ticket, input);
            ApplyTicketSectionPrices(ticket, input);
            ticket.IsActive = input.IsActive;
            ticket.RequiresAttendeeNames = input.RequiresAttendeeNames;

            await _db.SaveChangesAsync();
            TempData["StatusMessage"] = "Типът билет е обновен.";
            return RedirectToAction(nameof(Manage), new { id = ticket.EventId });
        }

        [Authorize(Roles = GlobalConstants.Roles.Admin + "," + GlobalConstants.Roles.Organizer)]
        public async Task<IActionResult> Delete(Guid id)
        {
            var userId = _userManager.GetUserId(User)!;
            var isAdmin = User.IsInRole(GlobalConstants.Roles.Admin);

            var ticket = await _db.Tickets
                .AsNoTracking()
                .Include(t => t.Event)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (ticket == null) return NotFound();
            if (!isAdmin && ticket.Event.OrganizerId != userId) return Forbid();

            ViewBag.SoldCount = await _db.UserTickets.CountAsync(ut => ut.TicketId == id);
            return View(ticket);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = GlobalConstants.Roles.Admin + "," + GlobalConstants.Roles.Organizer)]
        public async Task<IActionResult> DeleteConfirmed(Guid id)
        {
            var userId = _userManager.GetUserId(User)!;
            var isAdmin = User.IsInRole(GlobalConstants.Roles.Admin);

            var ticket = await _db.Tickets
                .Include(t => t.Event)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (ticket == null) return NotFound();
            if (!isAdmin && ticket.Event.OrganizerId != userId) return Forbid();

            ticket.IsActive = false;
            await _db.SaveChangesAsync();
            TempData["StatusMessage"] = "Типът билет е деактивиран.";
            return RedirectToAction(nameof(Manage), new { id = ticket.EventId });
        }

        // ---------- USER PURCHASE FLOW ----------

        [HttpPost]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting("interactions")]
        public async Task<IActionResult> Buy(Guid id, int? occurrenceId, int? seatId, int quantity = 1, [FromForm] List<string>? attendeeNames = null)
        {
            var userId = _userManager.GetUserId(User)!;

            var ticket = await _db.Tickets
                .Include(t => t.SectionPrices)
                .Include(t => t.Event)
                    .ThenInclude(e => e.OrganizerProfile)
                .Include(t => t.Event)
                    .ThenInclude(e => e.EventSeries)
                        .ThenInclude(s => s!.Occurrences)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (ticket == null) return NotFound();

            if (!ticket.IsActive)
            {
                TempData["StatusMessage"] = "Този билет вече не е наличен.";
                return RedirectToAction("Details", "Events", new { id = ticket.EventId });
            }
            EventOccurrence? occurrence = null;
            var startsAt = ticket.Event.StartTime;
            var canManageEvent = User.IsInRole(GlobalConstants.Roles.Admin) || ticket.Event.OrganizerId == userId;
            if (!canManageEvent
                && ticket.Event.EventSeries?.OccurrenceDisplayMode == EventOccurrenceDisplayMode.NextAvailableOnly)
            {
                var nextOccurrence = ticket.Event.EventSeries.Occurrences
                    .Where(o => o.Status == EventOccurrenceStatus.Scheduled && o.StartDateTime > DateTime.UtcNow)
                    .OrderBy(o => o.StartDateTime)
                    .FirstOrDefault();

                if (nextOccurrence == null)
                {
                    TempData["StatusMessage"] = "Няма налична следваща дата за покупка.";
                    return RedirectToAction("Details", "Events", new { id = ticket.EventId });
                }

                occurrenceId = nextOccurrence.Id;
            }

            if (occurrenceId.HasValue)
            {
                occurrence = await _db.EventOccurrences
                    .Include(o => o.EventSeries)
                    .FirstOrDefaultAsync(o => o.Id == occurrenceId.Value);

                if (occurrence == null || occurrence.EventSeries.EventId != ticket.EventId)
                {
                    TempData["StatusMessage"] = "Избери валидна дата за събитието.";
                    return RedirectToAction("Details", "Events", new { id = ticket.EventId });
                }

                if (occurrence.Status != EventOccurrenceStatus.Scheduled)
                {
                    TempData["StatusMessage"] = "Тази дата не е налична за покупка.";
                    return RedirectToAction("Details", "Events", new { id = ticket.EventId, occurrenceId });
                }

                startsAt = occurrence.StartDateTime;
            }

            if (occurrence == null && ticket.QuantityRemaining <= 0)
            {
                TempData["StatusMessage"] = "Съжаляваме, този билет е разпродаден.";
                return RedirectToAction("Details", "Events", new { id = ticket.EventId });
            }
            if (!ticket.Event.IsApproved)
            {
                TempData["StatusMessage"] = "Това събитие все още не е одобрено.";
                return RedirectToAction("Details", "Events", new { id = ticket.EventId, occurrenceId });
            }
            if (startsAt <= DateTime.UtcNow)
            {
                TempData["StatusMessage"] = "Това събитие вече е започнало.";
                return RedirectToAction("Details", "Events", new { id = ticket.EventId, occurrenceId });
            }

            var requiresSeat = ticket.Event.VenueLayoutId.HasValue
                               && ticket.Event.TicketingMode != EventTicketingMode.GeneralAdmission;
            var maxRequestQuantity = requiresSeat ? 50 : 10;
            if (quantity < 1 || quantity > maxRequestQuantity)
            {
                TempData["StatusMessage"] = "Избери между 1 и 10 билета за една покупка.";
                return RedirectToAction("Details", "Events", new { id = ticket.EventId, occurrenceId });
            }

            Seat? selectedSeat = null;
            if (requiresSeat)
            {
                if (!seatId.HasValue)
                {
                    TempData["StatusMessage"] = "Избери място преди покупка.";
                    return RedirectToAction("Details", "Events", new { id = ticket.EventId, occurrenceId });
                }

                selectedSeat = await _db.Seats
                    .Include(s => s.Section)
                    .FirstOrDefaultAsync(s => s.Id == seatId.Value
                                              && s.VenueLayoutId == ticket.Event.VenueLayoutId
                                              && s.Status == LayoutSeatStatus.Active);

                if (selectedSeat == null)
                {
                    TempData["StatusMessage"] = "Това място не е налично.";
                    return RedirectToAction("Details", "Events", new { id = ticket.EventId, occurrenceId });
                }

                var isTableSeat = selectedSeat.SeatType == SeatType.Table || selectedSeat.Section.Type == LayoutSectionType.Table;
                var maxSeatQuantity = isTableSeat
                    ? selectedSeat.IsCapacityUnlimited
                        ? 50
                        : Math.Min(Math.Max(selectedSeat.Capacity, 1), 50)
                    : 1;
                if (quantity > maxSeatQuantity)
                {
                    TempData["StatusMessage"] = isTableSeat
                        ? $"Тази маса е до {maxSeatQuantity} души."
                        : "За единично място може да се купи само 1 билет.";
                    return RedirectToAction("Details", "Events", new { id = ticket.EventId, occurrenceId });
                }

            }

            var cleanedAttendeeNames = NormalizeAttendeeNames(
                attendeeNames,
                quantity,
                ticket.RequiresAttendeeNames,
                out var attendeeNamesError);

            if (attendeeNamesError != null)
            {
                TempData["StatusMessage"] = attendeeNamesError;
                return RedirectToAction("Details", "Events", new { id = ticket.EventId, occurrenceId });
            }

            List<UserTicket> userTickets;
            decimal purchasedUnitPrice = 0m;
            await using var purchaseDbTransaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            try
            {
                if (occurrence != null)
                {
                    var paid = GlobalConstants.TransactionStatuses.Paid;
                    var soldForOccurrence = await _db.UserTickets
                        .CountAsync(ut => ut.TicketId == ticket.Id
                                          && ut.EventOccurrenceId == occurrence.Id
                                          && ut.Transaction.Status == paid);
                    var occurrenceCapacity = occurrence.CapacityOverride ?? ticket.QuantityTotal;
                    var occurrenceRemaining = occurrenceCapacity - soldForOccurrence;
                    if (occurrenceRemaining < quantity)
                    {
                        if (occurrenceRemaining <= 0)
                        {
                            occurrence.Status = EventOccurrenceStatus.SoldOut;
                            await _db.SaveChangesAsync();
                        }

                        await purchaseDbTransaction.RollbackAsync();
                        TempData["StatusMessage"] = occurrenceRemaining <= 0
                            ? "Съжаляваме, тази дата е разпродадена."
                            : $"Остават само {occurrenceRemaining} билета за тази дата.";
                        return RedirectToAction("Details", "Events", new { id = ticket.EventId, occurrenceId });
                    }
                }
                else
                {
                    await _db.Entry(ticket).ReloadAsync();
                    if (ticket.QuantityRemaining < quantity)
                    {
                        await purchaseDbTransaction.RollbackAsync();
                        TempData["StatusMessage"] = ticket.QuantityRemaining <= 0
                            ? "Съжаляваме, този билет е разпродаден."
                            : $"Остават само {ticket.QuantityRemaining} билета.";
                        return RedirectToAction("Details", "Events", new { id = ticket.EventId });
                    }

                    ticket.QuantityRemaining -= quantity;
                }

                if (selectedSeat != null)
                {
                    var reservation = await _seatReservations.ReserveSeatAsync(
                        ticket.EventId,
                        occurrence?.Id,
                        selectedSeat.Id,
                        userId,
                        TimeSpan.FromMinutes(10));

                    if (reservation == null)
                    {
                        await purchaseDbTransaction.RollbackAsync();
                        TempData["StatusMessage"] = "Това място току-що беше заето. Избери друго.";
                        return RedirectToAction("Details", "Events", new { id = ticket.EventId, occurrenceId });
                    }
                }

                var unitPrice = CalculateSeatAwarePrice(ticket, selectedSeat);
                purchasedUnitPrice = unitPrice;
                var purchaseGroupId = Guid.NewGuid();
                var purchaseTransaction = new Transaction
                {
                    UserId = userId,
                    BusinessWorkspaceId = ticket.Event.BusinessWorkspaceId ?? ticket.Event.OrganizerProfile?.BusinessWorkspaceId,
                    TotalAmount = unitPrice * quantity,
                    Status = GlobalConstants.TransactionStatuses.Paid,
                };

                userTickets = Enumerable.Range(0, quantity)
                    .Select(index => new UserTicket
                    {
                        TicketId = ticket.Id,
                        EventOccurrenceId = occurrence?.Id,
                        SeatId = selectedSeat?.Id,
                        TransactionId = purchaseTransaction.Id,
                        PurchaseGroupId = purchaseGroupId,
                        IsPrimaryInPurchase = index == 0,
                        AttendeeName = cleanedAttendeeNames[index],
                        QrCode = Guid.NewGuid().ToString("N"),
                        PricePaid = unitPrice,
                        IsUsed = false,
                    })
                    .ToList();

                _db.Transactions.Add(purchaseTransaction);
                _db.UserTickets.AddRange(userTickets);
                await _db.SaveChangesAsync();

                if (selectedSeat != null)
                {
                    var sold = await _seatReservations.MarkSeatSoldAsync(
                        ticket.EventId,
                        occurrence?.Id,
                        selectedSeat.Id,
                        userTickets[0].Id,
                        userId);

                    if (!sold)
                    {
                        await purchaseDbTransaction.RollbackAsync();
                        TempData["StatusMessage"] = "Покупката на мястото не можа да бъде завършена.";
                        return RedirectToAction("Details", "Events", new { id = ticket.EventId, occurrenceId });
                    }
                }

                await purchaseDbTransaction.CommitAsync();
            }
            catch (DbUpdateException)
            {
                await purchaseDbTransaction.RollbackAsync();
                TempData["StatusMessage"] = "Този билет или място току-що беше заето. Опитай отново.";
                return RedirectToAction("Details", "Events", new { id = ticket.EventId, occurrenceId });
            }

            var buyer = await _userManager.GetUserAsync(User);
            await SendPurchaseEmailWithQrAsync(
                buyer,
                ticket,
                occurrence,
                selectedSeat,
                userTickets,
                purchasedUnitPrice);

            TempData["StatusMessage"] = quantity == 1
                ? "Покупката е завършена - билетът е готов."
                : $"Покупката е завършена - {quantity} билета са готови.";
            return RedirectToAction(nameof(Details), new { id = userTickets[0].Id });
        }

        public async Task<IActionResult> MyTickets()
        {
            var userId = _userManager.GetUserId(User)!;

            var tickets = await _db.UserTickets
                .AsNoTracking()
                .Where(ut => ut.Transaction.UserId == userId)
                .OrderByDescending(ut => ut.CreatedAt)
                .Select(ut => new MyTicketRowViewModel
                {
                    Id = ut.Id,
                    EventId = ut.Ticket.EventId,
                    EventOccurrenceId = ut.EventOccurrenceId,
                    EventTitle = ut.Ticket.Event.Title,
                    TicketName = ut.Ticket.Name,
                    Address = ut.Ticket.Event.Address,
                    City = ut.Ticket.Event.City,
                    StartTime = ut.EventOccurrence != null ? ut.EventOccurrence.StartDateTime : ut.Ticket.Event.StartTime,
                    SeatLabel = ut.Seat != null ? GetSeatLabel(ut.Seat) : null,
                    AttendeeName = ut.AttendeeName,
                    PurchaseGroupId = ut.PurchaseGroupId,
                    IsPrimaryInPurchase = ut.IsPrimaryInPurchase,
                    Price = ut.PricePaid > 0 ? ut.PricePaid : ut.Ticket.Price,
                    IsUsed = ut.IsUsed,
                    CreatedAt = ut.CreatedAt,
                })
                .ToListAsync();

            return View(new MyTicketsViewModel { Tickets = tickets });
        }

        public async Task<IActionResult> Details(Guid id)
        {
            var userId = _userManager.GetUserId(User)!;
            var isAdmin = User.IsInRole(GlobalConstants.Roles.Admin);

            var ut = await _db.UserTickets
                .AsNoTracking()
                .Include(x => x.Ticket).ThenInclude(t => t.Event)
                .Include(x => x.Ticket).ThenInclude(t => t.SectionPrices)
                .Include(x => x.EventOccurrence)
                .Include(x => x.Seat).ThenInclude(s => s!.Section)
                .Include(x => x.Transaction).ThenInclude(t => t.User)
                .Include(x => x.UsedByOrganizer)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (ut == null) return NotFound();

            var isOwner = ut.Transaction.UserId == userId;
            var isOrganizerOfEvent = ut.Ticket.Event.OrganizerId == userId;
            if (!isAdmin && !isOwner && !isOrganizerOfEvent) return Forbid();

            var vm = ToDetails(ut);
            return View(vm);
        }

        public async Task<IActionResult> DownloadPdf(Guid id)
        {
            var userId = _userManager.GetUserId(User)!;
            var isAdmin = User.IsInRole(GlobalConstants.Roles.Admin);

            var ut = await _db.UserTickets
                .AsNoTracking()
                .Include(x => x.Ticket).ThenInclude(t => t.Event)
                .Include(x => x.Ticket).ThenInclude(t => t.SectionPrices)
                .Include(x => x.EventOccurrence)
                .Include(x => x.Seat).ThenInclude(s => s!.Section)
                .Include(x => x.Transaction).ThenInclude(t => t.User)
                .Include(x => x.UsedByOrganizer)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (ut == null) return NotFound();

            var isOwner = ut.Transaction.UserId == userId;
            var isOrganizerOfEvent = ut.Ticket.Event.OrganizerId == userId;
            if (!isAdmin && !isOwner && !isOrganizerOfEvent) return Forbid();

            var vm = ToDetails(ut);
            var bytes = _docs.GenerateTicketPdf(vm);
            return File(bytes, "application/pdf", $"Evento-Ticket-{ut.Id}.pdf");
        }

        public async Task<IActionResult> Qr(Guid id)
        {
            var userId = _userManager.GetUserId(User)!;
            var isAdmin = User.IsInRole(GlobalConstants.Roles.Admin);

            var ut = await _db.UserTickets
                .AsNoTracking()
                .Include(x => x.Ticket).ThenInclude(t => t.Event)
                .Include(x => x.Transaction)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (ut == null)
            {
                return NotFound();
            }

            var isOwner = ut.Transaction.UserId == userId;
            var isOrganizerOfEvent = ut.Ticket.Event.OrganizerId == userId;
            var canValidate = isAdmin || isOwner || isOrganizerOfEvent || await CanValidateEventAsync(userId, ut.Ticket.Event);
            if (!canValidate)
            {
                return Forbid();
            }

            if (string.IsNullOrWhiteSpace(ut.QrCode))
            {
                return NotFound();
            }

            var bytes = _docs.GenerateQrPng(ut.QrCode);
            return File(bytes, "image/png");
        }

        // ---------- VALIDATION ----------

        [HttpGet]
        [Authorize(Roles = GlobalConstants.Roles.Admin + "," + GlobalConstants.Roles.Organizer + "," + GlobalConstants.Roles.Validator)]
        public IActionResult Validate()
        {
            return View(new TicketValidationViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = GlobalConstants.Roles.Admin + "," + GlobalConstants.Roles.Organizer + "," + GlobalConstants.Roles.Validator)]
        public async Task<IActionResult> Validate(TicketValidationViewModel input)
        {
            var result = new TicketValidationResultViewModel();

            if (!ModelState.IsValid)
            {
                result.NotFound = true;
                result.Message = "Въведи или сканирай QR кода на билета.";
                ViewBag.Result = result;
                return View(input);
            }

            var userId = _userManager.GetUserId(User)!;
            var isAdmin = User.IsInRole(GlobalConstants.Roles.Admin);

            var code = input.QrCode.Trim();

            var ut = await _db.UserTickets
                .Include(x => x.Ticket).ThenInclude(t => t.Event)
                .Include(x => x.Ticket).ThenInclude(t => t.SectionPrices)
                .Include(x => x.EventOccurrence)
                .Include(x => x.Seat).ThenInclude(s => s!.Section)
                .Include(x => x.Transaction).ThenInclude(t => t.User)
                .Include(x => x.UsedByOrganizer)
                .FirstOrDefaultAsync(x => x.QrCode == code);

            if (ut == null)
            {
                result.NotFound = true;
                result.Message = "Не е намерен билет с този QR код.";
                ViewBag.Result = result;
                return View(input);
            }

            if (!isAdmin && !await CanValidateEventAsync(userId, ut.Ticket.Event))
            {
                result.NotAllowed = true;
                result.Message = "Нямаш достъп да валидираш билет за тази публична страница.";
                result.Ticket = ToDetails(ut);
                ViewBag.Result = result;
                return View(input);
            }

            if (ut.IsUsed)
            {
                result.AlreadyUsed = true;
                result.Message = $"Билетът вече е използван на {ut.UsedAt:dd.MM.yyyy HH:mm}.";
                result.Ticket = ToDetails(ut);
                ViewBag.Result = result;
                return View(input);
            }

            if (ut.EventOccurrence?.Status == EventOccurrenceStatus.Cancelled)
            {
                result.NotAllowed = true;
                result.Message = "Билетът е за отменена дата.";
                result.Ticket = ToDetails(ut);
                ViewBag.Result = result;
                return View(input);
            }

            if (!input.Confirm)
            {
                result.RequiresConfirmation = true;
                result.Message = "Билетът е намерен. Провери данните и потвърди валидирането.";
                result.Ticket = ToDetails(ut);
                ViewBag.Result = result;
                input.QrCode = code;
                return View(input);
            }

            ut.IsUsed = true;
            ut.UsedAt = DateTime.UtcNow;
            ut.UsedByOrganizerId = userId;
            await _db.SaveChangesAsync();

            result.Valid = true;
            result.Message = "Билетът е валидиран успешно.";
            result.Ticket = ToDetails(ut);
            ViewBag.Result = result;
            return View(new TicketValidationViewModel { QrCode = code });
        }

        // ---------- HELPERS ----------

        private async Task SendPurchaseEmailWithQrAsync(
            ApplicationUser? buyer,
            Ticket ticket,
            EventOccurrence? occurrence,
            Seat? seat,
            IReadOnlyCollection<UserTicket> userTickets,
            decimal unitPrice)
        {
            if (buyer == null || string.IsNullOrWhiteSpace(buyer.Email) || userTickets.Count == 0)
            {
                return;
            }

            static string E(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);

            try
            {
                var startsAt = occurrence?.StartDateTime ?? ticket.Event.StartTime;
                var endsAt = occurrence?.EndDateTime ?? ticket.Event.EndTime;
                var place = $"{ticket.Event.City}, {ticket.Event.Address}";
                var buyerName = string.IsNullOrWhiteSpace(buyer.UserName) ? buyer.Email : buyer.UserName;
                var seatLabel = seat == null ? null : GetSeatLabel(seat);
                var seatLine = string.IsNullOrWhiteSpace(seatLabel)
                    ? string.Empty
                    : $"""<p style="margin:0 0 6px;color:#374151"><strong>Седалка:</strong> {E(seatLabel)}</p>""";
                var total = unitPrice * userTickets.Count;
                var eventUrl = _appLinks.ToAbsoluteUrl(
                    Request,
                    Url.Action("Details", "Events", new { id = ticket.EventId, occurrenceId = occurrence?.Id })
                    ?? $"/Events/Details/{ticket.EventId}");
                var eventButton = BuildEmailButton(eventUrl, "Виж събитието", "#111827");
                var ticketCards = new StringBuilder();

                foreach (var userTicket in userTickets)
                {
                    var detailsUrl = _appLinks.ToAbsoluteUrl(
                        Request,
                        Url.Action(nameof(Details), "Tickets", new { id = userTicket.Id })
                        ?? $"/Tickets/Details/{userTicket.Id}");
                    var pdfUrl = _appLinks.ToAbsoluteUrl(
                        Request,
                        Url.Action(nameof(DownloadPdf), "Tickets", new { id = userTicket.Id })
                        ?? $"/Tickets/DownloadPdf/{userTicket.Id}");
                    var attendee = string.IsNullOrWhiteSpace(userTicket.AttendeeName)
                        ? buyerName
                        : userTicket.AttendeeName;
                    var qrImageUrl = "https://api.qrserver.com/v1/create-qr-code/?size=220x220&margin=12&data="
                        + Uri.EscapeDataString(userTicket.QrCode);
                    var paid = userTicket.PricePaid > 0 ? userTicket.PricePaid : unitPrice;
                    var ticketButton = BuildEmailButton(detailsUrl, "Отвори билета", "#5b4bff");
                    var pdfButton = BuildEmailButton(pdfUrl, "Изтегли PDF", "#111827");

                    ticketCards.Append($"""
                        <table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0" style="margin:18px 0;border:1px solid #dfe3f5;border-radius:20px;background:#ffffff">
                            <tr>
                                <td style="padding:20px 20px 8px 20px;vertical-align:top">
                                    <div style="font-size:12px;letter-spacing:.08em;text-transform:uppercase;color:#5b4bff;font-weight:800">Билет</div>
                                    <h3 style="margin:6px 0 12px;color:#111827;font-size:20px">{E(ticket.Name)}</h3>
                                    <p style="margin:0 0 6px;color:#374151"><strong>Притежател:</strong> {E(attendee)}</p>
                                    <p style="margin:0 0 6px;color:#374151"><strong>Имейл:</strong> {E(buyer.Email)}</p>
                                    <p style="margin:0 0 6px;color:#374151"><strong>Дата:</strong> {startsAt:dd.MM.yyyy HH:mm} - {endsAt:HH:mm}</p>
                                    <p style="margin:0 0 6px;color:#374151"><strong>Място:</strong> {E(place)}</p>
                                    {seatLine}
                                    <p style="margin:0 0 12px;color:#374151"><strong>Цена:</strong> {FormatMoney(paid)}</p>
                                </td>
                            </tr>
                            <tr>
                                <td align="center" style="padding:16px 20px;text-align:center;background:#f6f7ff">
                                    <img src="{E(qrImageUrl)}" width="180" height="180" alt="QR код за билет" style="display:block;width:180px;height:180px;margin:0 auto 10px;border-radius:14px;background:#fff" />
                                    <div style="font-size:11px;line-height:1.4;color:#6b7280;word-break:break-all">{E(userTicket.QrCode)}</div>
                                </td>
                            </tr>
                            <tr>
                                <td style="padding:18px 20px 20px 20px">
                                    {ticketButton}
                                    {pdfButton}
                                    <p style="margin:8px 0 0;color:#6b7280;font-size:12px;line-height:1.5;word-break:break-all">
                                        Ако бутоните не се отварят:<br />
                                        Билет: <a href="{E(detailsUrl)}" target="_blank" rel="noopener" style="color:#5b4bff">{E(detailsUrl)}</a><br />
                                        PDF: <a href="{E(pdfUrl)}" target="_blank" rel="noopener" style="color:#5b4bff">{E(pdfUrl)}</a>
                                    </p>
                                </td>
                            </tr>
                        </table>
                        """);
                }

                await _emailSender.SendEmailAsync(
                    buyer.Email,
                    $"Билет за {ticket.Event.Title} - Evento",
                    $"""
                    <div style="margin:0;padding:0;background:#f1f3fb;font-family:Arial,Helvetica,sans-serif;color:#111827">
                        <table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0" style="background:#f1f3fb;padding:16px 8px">
                            <tr>
                                <td align="center">
                                    <table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0" style="max-width:560px;background:#ffffff;border-radius:24px;box-shadow:0 24px 70px rgba(31,41,55,.12)">
                                        <tr>
                                            <td bgcolor="#2b276f" style="padding:26px 24px;background:#2b276f;color:#ffffff;border-radius:24px 24px 0 0">
                                                <div style="font-size:13px;font-weight:800;letter-spacing:.08em;text-transform:uppercase;opacity:.8">Evento</div>
                                                <h1 style="margin:10px 0 8px;font-size:26px;line-height:1.2;color:#ffffff">Билетите ти са готови.</h1>
                                                <p style="margin:0;color:#e7e9ff">Пази този имейл. QR кодът е валиден на входа, а PDF билетът е резервен вариант.</p>
                                            </td>
                                        </tr>
                                        <tr>
                                            <td style="padding:24px 20px 8px">
                                                <h2 style="margin:0 0 10px;font-size:22px;color:#111827;line-height:1.25">{E(ticket.Event.Title)}</h2>
                                                <p style="margin:0 0 6px;color:#374151"><strong>Дата:</strong> {startsAt:dd.MM.yyyy HH:mm} - {endsAt:HH:mm}</p>
                                                <p style="margin:0 0 6px;color:#374151"><strong>Място:</strong> {E(place)}</p>
                                                {seatLine}
                                                <p style="margin:0 0 18px;color:#374151"><strong>Общо:</strong> {FormatMoney(total)}</p>
                                                {eventButton}
                                                <p style="margin:4px 0 18px;color:#6b7280;font-size:12px;line-height:1.5;word-break:break-all">
                                                    Линк към събитието:
                                                    <a href="{E(eventUrl)}" target="_blank" rel="noopener" style="color:#5b4bff">{E(eventUrl)}</a>
                                                </p>
                                                {ticketCards}
                                                <p style="margin:20px 0 0;color:#6b7280;font-size:13px;line-height:1.5">Ако не си купувал билет, пиши на поддръжката. Не споделяй QR кода публично.</p>
                                            </td>
                                        </tr>
                                    </table>
                                </td>
                            </tr>
                        </table>
                    </div>
                    """);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send purchase email for ticket {TicketId} to user {UserId}.", ticket.Id, buyer.Id);
            }
        }

        private async Task SendPurchaseEmailAsync(
            ApplicationUser? buyer,
            Ticket ticket,
            EventOccurrence? occurrence,
            Seat? seat,
            IReadOnlyCollection<UserTicket> userTickets,
            decimal unitPrice)
        {
            if (buyer == null || string.IsNullOrWhiteSpace(buyer.Email) || userTickets.Count == 0)
            {
                return;
            }

            try
            {
                var startsAt = occurrence?.StartDateTime ?? ticket.Event.StartTime;
                var endsAt = occurrence?.EndDateTime ?? ticket.Event.EndTime;
                var eventUrl = _appLinks.ToAbsoluteUrl(
                    Request,
                    Url.Action("Details", "Events", new { id = ticket.EventId, occurrenceId = occurrence?.Id })
                    ?? $"/Events/Details/{ticket.EventId}");
                var ticketRows = new StringBuilder();

                foreach (var userTicket in userTickets)
                {
                    var detailsUrl = _appLinks.ToAbsoluteUrl(
                        Request,
                        Url.Action(nameof(Details), "Tickets", new { id = userTicket.Id })
                        ?? $"/Tickets/Details/{userTicket.Id}");
                    var pdfUrl = _appLinks.ToAbsoluteUrl(
                        Request,
                        Url.Action(nameof(DownloadPdf), "Tickets", new { id = userTicket.Id })
                        ?? $"/Tickets/DownloadPdf/{userTicket.Id}");
                    var attendee = string.IsNullOrWhiteSpace(userTicket.AttendeeName)
                        ? string.Empty
                        : $" - {WebUtility.HtmlEncode(userTicket.AttendeeName)}";

                    ticketRows.Append($"""
                        <li style="margin:10px 0">
                            <strong>{WebUtility.HtmlEncode(ticket.Name)}{attendee}</strong><br />
                            <a href="{WebUtility.HtmlEncode(detailsUrl)}">Отвори билета</a>
                            &nbsp;|&nbsp;
                            <a href="{WebUtility.HtmlEncode(pdfUrl)}">Изтегли PDF</a>
                        </li>
                        """);
                }

                var seatLine = seat == null
                    ? string.Empty
                    : $"<p><strong>Място:</strong> {WebUtility.HtmlEncode(GetSeatLabel(seat))}</p>";
                var total = unitPrice * userTickets.Count;

                await _emailSender.SendEmailAsync(
                    buyer.Email,
                    $"Билет за {ticket.Event.Title} - Evento",
                    $"""
                    <div style="font-family:Arial,sans-serif;line-height:1.5;color:#111827">
                        <h2>Билетите ти са готови</h2>
                        <p><strong>Събитие:</strong> {WebUtility.HtmlEncode(ticket.Event.Title)}</p>
                        <p><strong>Дата:</strong> {startsAt:dd.MM.yyyy HH:mm} - {endsAt:HH:mm}</p>
                        <p><strong>Място:</strong> {WebUtility.HtmlEncode(ticket.Event.City)}, {WebUtility.HtmlEncode(ticket.Event.Address)}</p>
                        {seatLine}
                        <p><strong>Сума:</strong> {FormatMoney(total)}</p>
                        <p><a href="{WebUtility.HtmlEncode(eventUrl)}" style="display:inline-block;background:#5b4bff;color:#ffffff;padding:12px 18px;border-radius:10px;text-decoration:none;font-weight:700">Виж събитието</a></p>
                        <h3>Твоите билети</h3>
                        <ul style="padding-left:18px">{ticketRows}</ul>
                        <p style="color:#6b7280">Пази този имейл. QR кодът и PDF билетът са достъпни през линковете по-горе.</p>
                    </div>
                    """);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send purchase email for ticket {TicketId} to user {UserId}.", ticket.Id, buyer.Id);
            }
        }

        private async Task<bool> CanValidateEventAsync(string userId, Event ev)
        {
            if (ev.OrganizerId == userId)
            {
                return true;
            }

            if (!ev.OrganizerProfileId.HasValue)
            {
                return false;
            }

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
                Price = ut.PricePaid > 0
                    ? ut.PricePaid
                    : CalculateSeatAwarePrice(ut.Ticket, ut.Seat),
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
        {
            return string.IsNullOrWhiteSpace(seat.Label) ? seat.Row + seat.Number : seat.Label;
        }

        private static string FormatMoney(decimal amount)
        {
            return amount <= 0m ? "Безплатно" : $"{amount:0.00} лв.";
        }

        private static string BuildEmailButton(string url, string label, string backgroundColor)
        {
            var safeUrl = WebUtility.HtmlEncode(url);
            var safeLabel = WebUtility.HtmlEncode(label);

            return $"""
                <table role="presentation" cellspacing="0" cellpadding="0" border="0" style="margin:0 0 10px 0">
                    <tr>
                        <td bgcolor="{backgroundColor}" style="background:{backgroundColor};border-radius:14px;text-align:center">
                            <a href="{safeUrl}" style="display:inline-block;padding:13px 18px;font-family:Arial,Helvetica,sans-serif;font-size:15px;line-height:20px;color:#ffffff;text-decoration:none;font-weight:800;border-radius:14px">
                                {safeLabel}
                            </a>
                        </td>
                    </tr>
                </table>
                """;
        }

        private static List<string?> NormalizeAttendeeNames(
            List<string>? names,
            int quantity,
            bool required,
            out string? error)
        {
            error = null;
            var normalized = new List<string?>(quantity);
            for (var i = 0; i < quantity; i++)
            {
                var value = names != null && i < names.Count ? names[i]?.Trim() : null;
                if (string.IsNullOrWhiteSpace(value))
                {
                    if (required)
                    {
                        error = $"Въведи име за билет #{i + 1}.";
                    }
                    normalized.Add(null);
                    continue;
                }

                normalized.Add(value.Length > 120 ? value[..120] : value);
            }

            return normalized;
        }

        private void NormalizeFreeTicketInput(TicketCreateEditViewModel input)
        {
            if (!input.IsFree)
            {
                return;
            }

            input.Price = 0m;
            foreach (var section in input.LayoutSections)
            {
                section.SectionPrice = 0m;
            }
            ModelState.Remove(nameof(input.Price));
        }

        private static void PopulateTicketLayoutPricing(
            TicketCreateEditViewModel input,
            Event ev,
            IEnumerable<TicketSectionPrice>? existingPrices = null,
            bool useSubmittedPrices = false)
        {
            input.HasSeatLayout = ev.VenueLayoutId.HasValue
                && ev.TicketingMode != EventTicketingMode.GeneralAdmission
                && ev.VenueLayout != null;

            if (!input.HasSeatLayout || ev.VenueLayout == null)
            {
                input.LayoutSections = new List<TicketLayoutSectionPriceViewModel>();
                input.SuggestedSeatCapacity = 0;
                return;
            }

            input.SuggestedSeatCapacity = ev.VenueLayout.Seats.Count(s => s.Status == LayoutSeatStatus.Active);
            var posted = useSubmittedPrices
                ? input.LayoutSections.ToDictionary(s => s.SectionId)
                : new Dictionary<int, TicketLayoutSectionPriceViewModel>();
            var existing = existingPrices?
                .GroupBy(p => p.SectionId)
                .ToDictionary(g => g.Key, g => g.First().Price)
                ?? new Dictionary<int, decimal>();

            input.LayoutSections = ev.VenueLayout.Sections
                .Where(section => section.Seats.Any(seat => seat.Status == LayoutSeatStatus.Active))
                .OrderBy(s => s.Name)
                .Select(section =>
                {
                    posted.TryGetValue(section.Id, out var postedSection);
                    return new TicketLayoutSectionPriceViewModel
                    {
                        SectionId = section.Id,
                        Name = section.Name,
                        ColorHex = string.IsNullOrWhiteSpace(section.ColorHex) ? "#2456ff" : section.ColorHex,
                        SeatsCount = section.Seats.Count(seat => seat.Status == LayoutSeatStatus.Active),
                        SectionPrice = postedSection?.SectionPrice
                            ?? (existing.TryGetValue(section.Id, out var price)
                                ? price
                                : Math.Max(0m, input.Price + section.PriceModifier)),
                    };
                })
                .ToList();
        }

        private void ValidateLayoutSectionPrices(TicketCreateEditViewModel input)
        {
            if (!input.HasSeatLayout)
            {
                return;
            }

            for (var i = 0; i < input.LayoutSections.Count; i++)
            {
                if (input.LayoutSections[i].SectionPrice < 0)
                {
                    ModelState.AddModelError(
                        $"LayoutSections[{i}].SectionPrice",
                        "Цената на сектор не може да е отрицателна.");
                }
            }
        }

        private static void ApplyTicketSectionPrices(Ticket ticket, TicketCreateEditViewModel input)
        {
            if (!input.HasSeatLayout || input.LayoutSections.Count == 0)
            {
                ticket.SectionPrices.Clear();
                return;
            }

            var posted = input.LayoutSections.ToDictionary(s => s.SectionId);
            var existing = ticket.SectionPrices.ToDictionary(p => p.SectionId);
            var postedSectionIds = posted.Keys.ToHashSet();

            foreach (var stalePrice in ticket.SectionPrices.Where(p => !postedSectionIds.Contains(p.SectionId)).ToList())
            {
                ticket.SectionPrices.Remove(stalePrice);
            }

            foreach (var sectionPrice in posted.Values)
            {
                if (existing.TryGetValue(sectionPrice.SectionId, out var entity))
                {
                    entity.Price = sectionPrice.SectionPrice;
                }
                else
                {
                    ticket.SectionPrices.Add(new TicketSectionPrice
                    {
                        SectionId = sectionPrice.SectionId,
                        Price = sectionPrice.SectionPrice,
                    });
                }
            }
        }

        private static decimal CalculateSeatAwarePrice(Ticket ticket, Seat? seat)
        {
            if (seat == null)
            {
                return ticket.Price;
            }

            var sectionPrice = ticket.SectionPrices.FirstOrDefault(p => p.SectionId == seat.SectionId);
            return sectionPrice?.Price ?? Math.Max(0m, ticket.Price + (seat.Section?.PriceModifier ?? 0m));
        }

        private async Task ApplyTicketImageAsync(Ticket ticket, TicketCreateEditViewModel input)
        {
            if (input.ImageFile == null || input.ImageFile.Length == 0) return;

            var media = await _mediaUpload.SaveAsync(input.ImageFile, "tickets");
            if (media?.MediaType != PostMediaType.Image)
            {
                throw new InvalidOperationException("Only image files are allowed for tickets.");
            }

            ticket.ImageUrl = media.Url;
        }
    }
}

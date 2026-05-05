using EventsApp.Common;
using EventsApp.Data;
using EventsApp.Models;
using EventsApp.Services;
using EventsApp.ViewModels.Tickets;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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

        public TicketsController(
            ApplicationDbContext db,
            UserManager<ApplicationUser> userManager,
            ITicketDocumentService docs,
            IMediaUploadService mediaUpload,
            ISeatReservationService seatReservations)
        {
            _db = db;
            _userManager = userManager;
            _docs = docs;
            _mediaUpload = mediaUpload;
            _seatReservations = seatReservations;
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

            var ev = await _db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id);
            if (ev == null) return NotFound();
            if (!isAdmin && ev.OrganizerId != userId) return Forbid();

            var vm = new TicketCreateEditViewModel
            {
                EventId = ev.Id,
                EventTitle = ev.Title,
                IsActive = true,
                IsFree = false,
                RequiresAttendeeNames = false,
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = GlobalConstants.Roles.Admin + "," + GlobalConstants.Roles.Organizer)]
        public async Task<IActionResult> Create(TicketCreateEditViewModel input)
        {
            var userId = _userManager.GetUserId(User)!;
            var isAdmin = User.IsInRole(GlobalConstants.Roles.Admin);

            var ev = await _db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.Id == input.EventId);
            if (ev == null) return NotFound();
            if (!isAdmin && ev.OrganizerId != userId) return Forbid();

            input.EventTitle = ev.Title;
            NormalizeFreeTicketInput(input);

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
                .FirstOrDefaultAsync(t => t.Id == id);

            if (ticket == null) return NotFound();
            if (!isAdmin && ticket.Event.OrganizerId != userId) return Forbid();

            var remaining = input.QuantityRemaining ?? ticket.QuantityRemaining;
            NormalizeFreeTicketInput(input);
            if (remaining > input.QuantityTotal)
            {
                ModelState.AddModelError(nameof(input.QuantityRemaining),
                    "Remaining cannot exceed total.");
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
        public async Task<IActionResult> Buy(Guid id, int? occurrenceId, int? seatId, int quantity = 1, [FromForm] List<string>? attendeeNames = null)
        {
            var userId = _userManager.GetUserId(User)!;

            var ticket = await _db.Tickets
                .Include(t => t.Event)
                    .ThenInclude(e => e.OrganizerProfile)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (ticket == null) return NotFound();

            if (!ticket.IsActive)
            {
                TempData["StatusMessage"] = "Този билет вече не е наличен.";
                return RedirectToAction("Details", "Events", new { id = ticket.EventId });
            }
            EventOccurrence? occurrence = null;
            var startsAt = ticket.Event.StartTime;
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

            if (occurrence != null)
            {
                var paid = GlobalConstants.TransactionStatuses.Paid;
                var soldForOccurrence = await _db.UserTickets
                    .CountAsync(ut => ut.TicketId == ticket.Id
                                      && ut.EventOccurrenceId == occurrence.Id
                                      && ut.Transaction.Status == paid);
                var occurrenceCapacity = occurrence.CapacityOverride ?? ticket.QuantityTotal;
                if (soldForOccurrence >= occurrenceCapacity)
                {
                    occurrence.Status = EventOccurrenceStatus.SoldOut;
                    await _db.SaveChangesAsync();
                    TempData["StatusMessage"] = "Съжаляваме, тази дата е разпродадена.";
                    return RedirectToAction("Details", "Events", new { id = ticket.EventId, occurrenceId });
                }
            }

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
                    TempData["StatusMessage"] = occurrenceRemaining <= 0
                        ? "Съжаляваме, тази дата е разпродадена."
                        : $"Остават само {occurrenceRemaining} билета за тази дата.";
                    return RedirectToAction("Details", "Events", new { id = ticket.EventId, occurrenceId });
                }
            }
            else if (ticket.QuantityRemaining < quantity)
            {
                TempData["StatusMessage"] = ticket.QuantityRemaining <= 0
                    ? "Съжаляваме, този билет е разпродаден."
                    : $"Остават само {ticket.QuantityRemaining} билета.";
                return RedirectToAction("Details", "Events", new { id = ticket.EventId });
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
                        ? $"Ð¢Ð°Ð·Ð¸ Ð¼Ð°ÑÐ° Ðµ Ð´Ð¾ {maxSeatQuantity} Ð´ÑƒÑˆÐ¸."
                        : "Ð—Ð° ÐµÐ´Ð¸Ð½Ð¸Ñ‡Ð½Ð¾ Ð¼ÑÑÑ‚Ð¾ Ð¼Ð¾Ð¶Ðµ Ð´Ð° ÑÐµ ÐºÑƒÐ¿Ð¸ ÑÐ°Ð¼Ð¾ 1 Ð±Ð¸Ð»ÐµÑ‚.";
                    return RedirectToAction("Details", "Events", new { id = ticket.EventId, occurrenceId });
                }

                var reservation = await _seatReservations.ReserveSeatAsync(
                    ticket.EventId,
                    occurrence?.Id,
                    selectedSeat.Id,
                    userId,
                    TimeSpan.FromMinutes(10));

                if (reservation == null)
                {
                    TempData["StatusMessage"] = "Това място току-що беше заето. Избери друго.";
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

            var unitPrice = ticket.Price + (selectedSeat?.Section.PriceModifier ?? 0m);
            var purchaseGroupId = Guid.NewGuid();
            var transaction = new Transaction
            {
                UserId = userId,
                BusinessWorkspaceId = ticket.Event.BusinessWorkspaceId ?? ticket.Event.OrganizerProfile?.BusinessWorkspaceId,
                TotalAmount = unitPrice * quantity,
                Status = GlobalConstants.TransactionStatuses.Paid,
            };

            var userTickets = Enumerable.Range(0, quantity)
                .Select(index => new UserTicket
                {
                    TicketId = ticket.Id,
                    EventOccurrenceId = occurrence?.Id,
                    SeatId = selectedSeat?.Id,
                    TransactionId = transaction.Id,
                    PurchaseGroupId = purchaseGroupId,
                    IsPrimaryInPurchase = index == 0,
                    AttendeeName = cleanedAttendeeNames[index],
                    QrCode = Guid.NewGuid().ToString("N"),
                    PricePaid = unitPrice,
                    IsUsed = false,
                })
                .ToList();

            if (occurrence == null)
            {
                ticket.QuantityRemaining -= quantity;
            }

            _db.Transactions.Add(transaction);
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
                    TempData["StatusMessage"] = "Покупката на мястото не можа да бъде завършена.";
                    return RedirectToAction("Details", "Events", new { id = ticket.EventId, occurrenceId });
                }
            }

            TempData["StatusMessage"] = "Демо покупката е завършена - билетът е готов.";
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

        // ---------- VALIDATION ----------

        [HttpGet]
        [Authorize(Roles = GlobalConstants.Roles.Admin + "," + GlobalConstants.Roles.Organizer)]
        public IActionResult Validate()
        {
            return View(new TicketValidationViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = GlobalConstants.Roles.Admin + "," + GlobalConstants.Roles.Organizer)]
        public async Task<IActionResult> Validate(TicketValidationViewModel input)
        {
            var result = new TicketValidationResultViewModel();

            if (!ModelState.IsValid)
            {
                result.NotFound = true;
                result.Message = "Please paste a QR code value.";
                ViewBag.Result = result;
                return View(input);
            }

            var userId = _userManager.GetUserId(User)!;
            var isAdmin = User.IsInRole(GlobalConstants.Roles.Admin);

            var code = input.QrCode.Trim();

            var ut = await _db.UserTickets
                .Include(x => x.Ticket).ThenInclude(t => t.Event)
                .Include(x => x.EventOccurrence)
                .Include(x => x.Seat).ThenInclude(s => s!.Section)
                .Include(x => x.Transaction).ThenInclude(t => t.User)
                .Include(x => x.UsedByOrganizer)
                .FirstOrDefaultAsync(x => x.QrCode == code);

            if (ut == null)
            {
                result.NotFound = true;
                result.Message = "Invalid ticket — no such QR code.";
                ViewBag.Result = result;
                return View(input);
            }

            if (!isAdmin && ut.Ticket.Event.OrganizerId != userId)
            {
                result.NotAllowed = true;
                result.Message = "You are not the organizer of this event.";
                result.Ticket = ToDetails(ut);
                ViewBag.Result = result;
                return View(input);
            }

            if (ut.IsUsed)
            {
                result.AlreadyUsed = true;
                result.Message = $"Ticket already used at {ut.UsedAt:yyyy-MM-dd HH:mm}.";
                result.Ticket = ToDetails(ut);
                ViewBag.Result = result;
                return View(input);
            }

            if (ut.EventOccurrence?.Status == EventOccurrenceStatus.Cancelled)
            {
                result.NotAllowed = true;
                result.Message = "This ticket belongs to a cancelled occurrence.";
                result.Ticket = ToDetails(ut);
                ViewBag.Result = result;
                return View(input);
            }

            ut.IsUsed = true;
            ut.UsedAt = DateTime.UtcNow;
            ut.UsedByOrganizerId = userId;
            await _db.SaveChangesAsync();

            result.Valid = true;
            result.Message = "Ticket valid — checked in successfully.";
            result.Ticket = ToDetails(ut);
            ViewBag.Result = result;
            return View(new TicketValidationViewModel());
        }

        // ---------- HELPERS ----------

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
                    : ut.Ticket.Price + (ut.Seat?.Section.PriceModifier ?? 0m),
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
                        error = $"Ð’ÑŠÐ²ÐµÐ´Ð¸ Ð¸Ð¼Ðµ Ð·Ð° Ð±Ð¸Ð»ÐµÑ‚ #{i + 1}.";
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
            ModelState.Remove(nameof(input.Price));
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

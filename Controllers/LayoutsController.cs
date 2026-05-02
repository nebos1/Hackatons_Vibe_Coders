using System.Text.Json;
using System.Text.Json.Serialization;
using EventsApp.Common;
using EventsApp.Data;
using EventsApp.Models;
using EventsApp.Services;
using EventsApp.ViewModels.Layouts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EventsApp.Controllers
{
    [Authorize(Roles = GlobalConstants.Roles.Admin + "," + GlobalConstants.Roles.Organizer)]
    public class LayoutsController : Controller
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
        {
            Converters = { new JsonStringEnumConverter() },
        };

        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILayoutService _layouts;

        public LayoutsController(
            ApplicationDbContext db,
            UserManager<ApplicationUser> userManager,
            ILayoutService layouts)
        {
            _db = db;
            _userManager = userManager;
            _layouts = layouts;
        }

        public async Task<IActionResult> Index()
        {
            var userId = _userManager.GetUserId(User)!;
            var isAdmin = User.IsInRole(GlobalConstants.Roles.Admin);

            var query = _db.VenueLayouts.AsNoTracking();
            if (!isAdmin)
            {
                query = query.Where(l => l.OrganizerId == userId);
            }

            var layouts = await query
                .OrderByDescending(l => l.CreatedAt)
                .Select(l => new VenueLayoutRowViewModel
                {
                    Id = l.Id,
                    VenueName = l.VenueName,
                    Name = l.Name,
                    Version = l.Version,
                    Status = l.Status,
                    SectionsCount = l.Sections.Count,
                    SeatsCount = l.Seats.Count,
                    HasSoldSeats = l.Seats.SelectMany(s => s.Inventories).Any(i => i.Status == EventSeatInventoryStatus.Sold),
                    CreatedAt = l.CreatedAt,
                })
                .ToListAsync();

            return View(new VenueLayoutListViewModel { Layouts = layouts });
        }

        public IActionResult Create()
        {
            return View("Editor", new VenueLayoutEditorViewModel
            {
                VenueName = "Основна зала",
                Name = "Стандартна схема",
                LayoutJson = "{\"sections\":[{\"clientId\":\"section-1\",\"name\":\"Основна секция\",\"type\":\"Seated\",\"capacity\":40,\"priceModifier\":0,\"x\":60,\"y\":60,\"width\":520,\"height\":280,\"seats\":[]}]}",
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(VenueLayoutEditorViewModel input)
        {
            if (!ModelState.IsValid)
            {
                return View("Editor", input);
            }

            var userId = _userManager.GetUserId(User)!;
            var layout = new VenueLayout
            {
                OrganizerId = userId,
                VenueName = input.VenueName.Trim(),
                Name = input.Name.Trim(),
                Status = input.Status,
            };

            ApplyLayoutJson(layout, input.LayoutJson);
            _db.VenueLayouts.Add(layout);
            await _db.SaveChangesAsync();

            TempData["StatusMessage"] = "Layout-ът е създаден.";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(int id)
        {
            var layout = await FindOwnedLayoutAsync(id, includeDetails: true);
            if (layout == null) return NotFound();

            return View("Editor", new VenueLayoutEditorViewModel
            {
                Id = layout.Id,
                VenueName = layout.VenueName,
                Name = layout.Name,
                Version = layout.Version,
                Status = layout.Status,
                IsInUseWithSoldSeats = await _layouts.LayoutHasSoldSeatsAsync(layout.Id),
                LayoutJson = BuildLayoutJson(layout),
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, VenueLayoutEditorViewModel input)
        {
            var layout = await FindOwnedLayoutAsync(id, includeDetails: true);
            if (layout == null) return NotFound();

            if (!ModelState.IsValid)
            {
                input.Id = id;
                input.IsInUseWithSoldSeats = await _layouts.LayoutHasSoldSeatsAsync(id);
                return View("Editor", input);
            }

            if (await _layouts.LayoutHasSoldSeatsAsync(id))
            {
                var versioned = new VenueLayout
                {
                    OrganizerId = layout.OrganizerId,
                    VenueName = input.VenueName.Trim(),
                    Name = input.Name.Trim(),
                    Version = layout.Version + 1,
                    Status = input.Status,
                };

                ApplyLayoutJson(versioned, input.LayoutJson);
                _db.VenueLayouts.Add(versioned);
                await _db.SaveChangesAsync();
                TempData["StatusMessage"] = "Този layout вече има продадени места, затова беше създадена нова версия.";
                return RedirectToAction(nameof(Edit), new { id = versioned.Id });
            }

            layout.VenueName = input.VenueName.Trim();
            layout.Name = input.Name.Trim();
            layout.Status = input.Status;
            layout.UpdatedAt = DateTime.UtcNow;

            _db.Seats.RemoveRange(layout.Seats);
            _db.LayoutSections.RemoveRange(layout.Sections);
            ApplyLayoutJson(layout, input.LayoutJson);
            await _db.SaveChangesAsync();

            TempData["StatusMessage"] = "Layout-ът е обновен.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Duplicate(int id)
        {
            var userId = _userManager.GetUserId(User)!;
            var copy = await _layouts.DuplicateLayoutAsync(id, userId, User.IsInRole(GlobalConstants.Roles.Admin));
            if (copy == null) return NotFound();

            TempData["StatusMessage"] = "Layout-ът е дублиран.";
            return RedirectToAction(nameof(Edit), new { id = copy.Id });
        }

        public async Task<IActionResult> Preview(int id)
        {
            var layout = await FindOwnedLayoutAsync(id, includeDetails: true);
            if (layout == null) return NotFound();

            ViewBag.ReadOnly = true;
            return View("Editor", new VenueLayoutEditorViewModel
            {
                Id = layout.Id,
                VenueName = layout.VenueName,
                Name = layout.Name,
                Version = layout.Version,
                Status = layout.Status,
                LayoutJson = BuildLayoutJson(layout),
            });
        }

        private async Task<VenueLayout?> FindOwnedLayoutAsync(int id, bool includeDetails)
        {
            var userId = _userManager.GetUserId(User)!;
            var isAdmin = User.IsInRole(GlobalConstants.Roles.Admin);

            IQueryable<VenueLayout> query = _db.VenueLayouts;
            if (includeDetails)
            {
                query = query
                    .Include(l => l.Sections)
                    .Include(l => l.Seats)
                        .ThenInclude(s => s.Section);
            }

            var layout = await query.FirstOrDefaultAsync(l => l.Id == id);
            if (layout == null || (!isAdmin && layout.OrganizerId != userId))
            {
                return null;
            }

            return layout;
        }

        private static void ApplyLayoutJson(VenueLayout layout, string? json)
        {
            var parsed = string.IsNullOrWhiteSpace(json)
                ? new VenueLayoutJsonModel()
                : JsonSerializer.Deserialize<VenueLayoutJsonModel>(json, JsonOptions) ?? new VenueLayoutJsonModel();

            foreach (var inputSection in parsed.Sections)
            {
                var section = new LayoutSection
                {
                    VenueLayout = layout,
                    Name = string.IsNullOrWhiteSpace(inputSection.Name) ? "Секция" : inputSection.Name.Trim(),
                    Type = inputSection.Type,
                    Capacity = inputSection.Seats.Count > 0 ? inputSection.Seats.Count : Math.Max(0, inputSection.Capacity),
                    PriceModifier = inputSection.PriceModifier,
                    X = inputSection.X,
                    Y = inputSection.Y,
                    Width = inputSection.Width,
                    Height = inputSection.Height,
                };

                layout.Sections.Add(section);

                foreach (var inputSeat in inputSection.Seats)
                {
                    layout.Seats.Add(new Seat
                    {
                        VenueLayout = layout,
                        Section = section,
                        Row = string.IsNullOrWhiteSpace(inputSeat.Row) ? "A" : inputSeat.Row.Trim(),
                        Number = string.IsNullOrWhiteSpace(inputSeat.Number) ? "1" : inputSeat.Number.Trim(),
                        X = inputSeat.X,
                        Y = inputSeat.Y,
                        SeatType = inputSeat.SeatType,
                        Status = inputSeat.Status,
                    });
                }
            }
        }

        private static string BuildLayoutJson(VenueLayout layout)
        {
            var payload = new VenueLayoutJsonModel
            {
                Sections = layout.Sections
                    .OrderBy(s => s.Id)
                    .Select(section => new LayoutSectionJsonModel
                    {
                        Id = section.Id,
                        ClientId = "section-" + section.Id,
                        Name = section.Name,
                        Type = section.Type,
                        Capacity = section.Capacity,
                        PriceModifier = section.PriceModifier,
                        X = section.X,
                        Y = section.Y,
                        Width = section.Width,
                        Height = section.Height,
                        Seats = layout.Seats
                            .Where(seat => seat.SectionId == section.Id)
                            .OrderBy(seat => seat.Row)
                            .ThenBy(seat => seat.Number)
                            .Select(seat => new SeatJsonModel
                            {
                                Id = seat.Id,
                                Row = seat.Row,
                                Number = seat.Number,
                                X = seat.X,
                                Y = seat.Y,
                                SeatType = seat.SeatType,
                                Status = seat.Status,
                            })
                            .ToList(),
                    })
                    .ToList(),
            };

            return JsonSerializer.Serialize(payload, JsonOptions);
        }
    }
}

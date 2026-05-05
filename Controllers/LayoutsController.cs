using System.Text.Json;
using System.Text.Json.Serialization;
using EventsApp.Common;
using EventsApp.Data;
using EventsApp.Models;
using EventsApp.Services;
using EventsApp.Services.AI;
using EventsApp.ViewModels.Layouts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
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
        private const int MaxAiDescriptionLength = 1200;
        private const long MaxAiImageBytes = 5L * 1024 * 1024;

        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILayoutService _layouts;
        private readonly ILayoutAiService _layoutAi;

        public LayoutsController(
            ApplicationDbContext db,
            UserManager<ApplicationUser> userManager,
            ILayoutService layouts,
            ILayoutAiService layoutAi)
        {
            _db = db;
            _userManager = userManager;
            _layouts = layouts;
            _layoutAi = layoutAi;
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

        [HttpPost]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting("ai-heavy")]
        [RequestSizeLimit(MaxAiImageBytes + 64 * 1024)]
        public async Task<IActionResult> AiGenerate([FromForm] string? description, [FromForm] IFormFile? image, CancellationToken cancellationToken)
        {
            if (!string.IsNullOrWhiteSpace(description) && description.Length > MaxAiDescriptionLength)
            {
                return BadRequest(new
                {
                    ok = false,
                    fallback = true,
                    message = $"Описанието е твърде дълго. Максимумът е {MaxAiDescriptionLength} символа.",
                });
            }

            if (image?.Length > MaxAiImageBytes)
            {
                return BadRequest(new
                {
                    ok = false,
                    fallback = true,
                    message = "Снимката е твърде голяма. Максимумът е 5 MB.",
                });
            }

            if (image != null &&
                image.Length > 0 &&
                (string.IsNullOrWhiteSpace(image.ContentType) ||
                 !image.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)))
            {
                return BadRequest(new
                {
                    ok = false,
                    fallback = true,
                    message = "Може да се качва само снимка.",
                });
            }

            if ((string.IsNullOrWhiteSpace(description) && (image == null || image.Length == 0)) || !_layoutAi.IsEnabled)
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new
                {
                    ok = false,
                    fallback = true,
                    message = string.IsNullOrWhiteSpace(_layoutAi.LastError)
                        ? "AI не е наличен. Използвай локалния генератор."
                        : _layoutAi.LastError,
                });
            }

            var layout = await _layoutAi.GenerateLayoutAsync(description, image, cancellationToken);
            if (layout == null)
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new
                {
                    ok = false,
                    fallback = true,
                    message = string.IsNullOrWhiteSpace(_layoutAi.LastError)
                        ? "AI не успя да върне валиден layout."
                        : _layoutAi.LastError,
                });
            }

            return new JsonResult(new { ok = true, layout }, JsonOptions);
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
                    FloorName = NormalizeFloorName(inputSection.FloorName),
                    Type = inputSection.Type,
                    Shape = NormalizeShape(inputSection.Shape),
                    Capacity = inputSection.Seats.Count > 0
                        ? inputSection.Seats.Sum(s => s.IsCapacityUnlimited ? 0 : Math.Max(1, s.Capacity))
                        : Math.Max(0, inputSection.Capacity),
                    PriceModifier = inputSection.PriceModifier,
                    X = inputSection.X,
                    Y = inputSection.Y,
                    Width = inputSection.Width,
                    Height = inputSection.Height,
                    Rotation = inputSection.Rotation,
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
                        Label = string.IsNullOrWhiteSpace(inputSeat.Label) ? null : inputSeat.Label.Trim(),
                        X = inputSeat.X,
                        Y = inputSeat.Y,
                        Radius = inputSeat.Radius <= 0 ? 16 : inputSeat.Radius,
                        Rotation = inputSeat.Rotation,
                        Capacity = Math.Max(1, inputSeat.Capacity),
                        IsCapacityUnlimited = inputSeat.IsCapacityUnlimited,
                        SeatType = inputSeat.SeatType,
                        Status = inputSeat.Status,
                    });
                }
            }
        }

        private static string NormalizeFloorName(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? "Floor 1" : value.Trim();
        }

        private static string NormalizeShape(string? value)
        {
            var shape = string.IsNullOrWhiteSpace(value) ? "Rectangle" : value.Trim();
            return shape.Length > 32 ? shape[..32] : shape;
        }

        private static string BuildLayoutJson(VenueLayout layout)
        {
            var payload = new VenueLayoutJsonModel
            {
                Floors = layout.Sections
                    .Select(s => NormalizeFloorName(s.FloorName))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Select((floorName, index) => new LayoutFloorJsonModel
                    {
                        ClientId = "floor-" + (index + 1),
                        Name = floorName,
                    })
                    .ToList(),
                Sections = layout.Sections
                    .OrderBy(s => s.Id)
                    .Select(section => new LayoutSectionJsonModel
                    {
                        Id = section.Id,
                        ClientId = "section-" + section.Id,
                        Name = section.Name,
                        FloorId = "floor-" + (layout.Sections
                            .Select(s => NormalizeFloorName(s.FloorName))
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .ToList()
                            .FindIndex(f => string.Equals(f, NormalizeFloorName(section.FloorName), StringComparison.OrdinalIgnoreCase)) + 1),
                        FloorName = NormalizeFloorName(section.FloorName),
                        Type = section.Type,
                        Shape = NormalizeShape(section.Shape),
                        Capacity = section.Capacity,
                        PriceModifier = section.PriceModifier,
                        X = section.X,
                        Y = section.Y,
                        Width = section.Width,
                        Height = section.Height,
                        Rotation = section.Rotation,
                        Seats = layout.Seats
                            .Where(seat => seat.SectionId == section.Id)
                            .OrderBy(seat => seat.Row)
                            .ThenBy(seat => seat.Number)
                            .Select(seat => new SeatJsonModel
                            {
                                Id = seat.Id,
                                Row = seat.Row,
                                Number = seat.Number,
                                Label = seat.Label,
                                X = seat.X,
                                Y = seat.Y,
                                Radius = seat.Radius <= 0 ? 16 : seat.Radius,
                                Rotation = seat.Rotation,
                                Capacity = Math.Max(1, seat.Capacity),
                                IsCapacityUnlimited = seat.IsCapacityUnlimited,
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

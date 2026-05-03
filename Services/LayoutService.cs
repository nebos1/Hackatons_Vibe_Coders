using EventsApp.Data;
using EventsApp.Models;
using Microsoft.EntityFrameworkCore;

namespace EventsApp.Services
{
    public interface ILayoutService
    {
        Task EnsureInventoryAsync(int eventId, int? occurrenceId, int layoutId);
        Task<VenueLayout?> DuplicateLayoutAsync(int layoutId, string organizerId, bool isAdmin);
        Task<bool> LayoutHasSoldSeatsAsync(int layoutId);
    }

    public class LayoutService : ILayoutService
    {
        private readonly ApplicationDbContext _db;

        public LayoutService(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task EnsureInventoryAsync(int eventId, int? occurrenceId, int layoutId)
        {
            var seats = await _db.Seats
                .AsNoTracking()
                .Where(s => s.VenueLayoutId == layoutId && s.Status == LayoutSeatStatus.Active)
                .Select(s => s.Id)
                .ToListAsync();

            if (seats.Count == 0)
            {
                return;
            }

            var existingSeatIds = occurrenceId.HasValue
                ? await _db.EventSeatInventories
                    .Where(i => i.EventOccurrenceId == occurrenceId.Value)
                    .Select(i => i.SeatId)
                    .ToListAsync()
                : await _db.EventSeatInventories
                    .Where(i => i.EventId == eventId && i.EventOccurrenceId == null)
                    .Select(i => i.SeatId)
                    .ToListAsync();

            var existing = existingSeatIds.ToHashSet();
            foreach (var seatId in seats.Where(s => !existing.Contains(s)))
            {
                _db.EventSeatInventories.Add(new EventSeatInventory
                {
                    EventId = occurrenceId.HasValue ? null : eventId,
                    EventOccurrenceId = occurrenceId,
                    SeatId = seatId,
                    Status = EventSeatInventoryStatus.Available,
                });
            }

            await _db.SaveChangesAsync();
        }

        public async Task<VenueLayout?> DuplicateLayoutAsync(int layoutId, string organizerId, bool isAdmin)
        {
            var source = await _db.VenueLayouts
                .AsNoTracking()
                .Include(l => l.Sections)
                .Include(l => l.Seats)
                .FirstOrDefaultAsync(l => l.Id == layoutId);

            if (source == null || (!isAdmin && source.OrganizerId != organizerId))
            {
                return null;
            }

            var copy = new VenueLayout
            {
                OrganizerId = source.OrganizerId,
                VenueName = source.VenueName,
                Name = source.Name + " copy",
                Version = source.Version + 1,
                Status = VenueLayoutStatus.Draft,
            };

            var sectionMap = new Dictionary<int, LayoutSection>();
            foreach (var section in source.Sections.OrderBy(s => s.Id))
            {
                var sectionCopy = new LayoutSection
                {
                    Name = section.Name,
                    Type = section.Type,
                    Capacity = section.Capacity,
                    PriceModifier = section.PriceModifier,
                    X = section.X,
                    Y = section.Y,
                    Width = section.Width,
                    Height = section.Height,
                };

                copy.Sections.Add(sectionCopy);
                sectionMap[section.Id] = sectionCopy;
            }

            foreach (var seat in source.Seats.OrderBy(s => s.Id))
            {
                if (!sectionMap.TryGetValue(seat.SectionId, out var sectionCopy))
                {
                    continue;
                }

                copy.Seats.Add(new Seat
                {
                    VenueLayout = copy,
                    Section = sectionCopy,
                    Row = seat.Row,
                    Number = seat.Number,
                    X = seat.X,
                    Y = seat.Y,
                    SeatType = seat.SeatType,
                    Status = seat.Status,
                });
            }

            _db.VenueLayouts.Add(copy);
            await _db.SaveChangesAsync();
            return copy;
        }

        public Task<bool> LayoutHasSoldSeatsAsync(int layoutId)
        {
            return _db.EventSeatInventories
                .AnyAsync(i => i.Seat.VenueLayoutId == layoutId && i.Status == EventSeatInventoryStatus.Sold);
        }
    }
}

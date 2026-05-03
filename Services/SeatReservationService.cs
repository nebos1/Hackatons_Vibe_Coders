using EventsApp.Data;
using EventsApp.Models;
using Microsoft.EntityFrameworkCore;

namespace EventsApp.Services
{
    public interface ISeatReservationService
    {
        Task ReleaseExpiredReservationsAsync();
        Task<EventSeatInventory?> ReserveSeatAsync(int eventId, int? occurrenceId, int seatId, string userId, TimeSpan holdFor);
        Task<bool> ReleaseReservationAsync(int inventoryId, string userId, bool isAdmin);
        Task<bool> MarkSeatSoldAsync(int eventId, int? occurrenceId, int seatId, Guid userTicketId, string userId);
    }

    public class SeatReservationService : ISeatReservationService
    {
        private readonly ApplicationDbContext _db;

        public SeatReservationService(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task ReleaseExpiredReservationsAsync()
        {
            var now = DateTime.UtcNow;
            var expired = await _db.EventSeatInventories
                .Where(i => i.Status == EventSeatInventoryStatus.Reserved
                            && i.ReservedUntil.HasValue
                            && i.ReservedUntil <= now
                            && i.TicketId == null)
                .ToListAsync();

            foreach (var item in expired)
            {
                item.Status = EventSeatInventoryStatus.Available;
                item.ReservedUntil = null;
                item.ReservedByUserId = null;
            }

            if (expired.Count > 0)
            {
                await _db.SaveChangesAsync();
            }
        }

        public async Task<EventSeatInventory?> ReserveSeatAsync(int eventId, int? occurrenceId, int seatId, string userId, TimeSpan holdFor)
        {
            await ReleaseExpiredReservationsAsync();

            var inventory = await FindInventoryAsync(eventId, occurrenceId, seatId);
            if (inventory == null)
            {
                return null;
            }

            var now = DateTime.UtcNow;
            var reservedBySameUser = inventory.Status == EventSeatInventoryStatus.Reserved
                                     && inventory.ReservedByUserId == userId
                                     && inventory.ReservedUntil > now;

            if (inventory.Status != EventSeatInventoryStatus.Available && !reservedBySameUser)
            {
                return null;
            }

            inventory.Status = EventSeatInventoryStatus.Reserved;
            inventory.ReservedByUserId = userId;
            inventory.ReservedUntil = now.Add(holdFor);
            await _db.SaveChangesAsync();
            return inventory;
        }

        public async Task<bool> ReleaseReservationAsync(int inventoryId, string userId, bool isAdmin)
        {
            var inventory = await _db.EventSeatInventories.FirstOrDefaultAsync(i => i.Id == inventoryId);
            if (inventory == null || inventory.Status != EventSeatInventoryStatus.Reserved)
            {
                return false;
            }

            if (!isAdmin && inventory.ReservedByUserId != userId)
            {
                return false;
            }

            inventory.Status = EventSeatInventoryStatus.Available;
            inventory.ReservedUntil = null;
            inventory.ReservedByUserId = null;
            await _db.SaveChangesAsync();
            return true;
        }

        public async Task<bool> MarkSeatSoldAsync(int eventId, int? occurrenceId, int seatId, Guid userTicketId, string userId)
        {
            var inventory = await FindInventoryAsync(eventId, occurrenceId, seatId);
            if (inventory == null)
            {
                return false;
            }

            var canUseReservation = inventory.Status == EventSeatInventoryStatus.Reserved
                                    && inventory.ReservedByUserId == userId
                                    && inventory.ReservedUntil > DateTime.UtcNow;

            if (inventory.Status == EventSeatInventoryStatus.Sold
                || inventory.Status == EventSeatInventoryStatus.Blocked
                || (inventory.Status == EventSeatInventoryStatus.Reserved && !canUseReservation))
            {
                return false;
            }

            inventory.Status = EventSeatInventoryStatus.Sold;
            inventory.TicketId = userTicketId;
            inventory.ReservedUntil = null;
            inventory.ReservedByUserId = null;
            await _db.SaveChangesAsync();
            return true;
        }

        private Task<EventSeatInventory?> FindInventoryAsync(int eventId, int? occurrenceId, int seatId)
        {
            return occurrenceId.HasValue
                ? _db.EventSeatInventories.FirstOrDefaultAsync(i => i.EventOccurrenceId == occurrenceId.Value && i.SeatId == seatId)
                : _db.EventSeatInventories.FirstOrDefaultAsync(i => i.EventId == eventId && i.EventOccurrenceId == null && i.SeatId == seatId);
        }
    }
}

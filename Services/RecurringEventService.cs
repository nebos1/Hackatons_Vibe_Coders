using EventsApp.Data;
using EventsApp.Models;
using Microsoft.EntityFrameworkCore;

namespace EventsApp.Services
{
    public interface IRecurringEventService
    {
        IReadOnlyList<EventOccurrence> GenerateOccurrences(EventSeries series, int maxOccurrences = 370);
        Task RegenerateOccurrencesAsync(EventSeries series, RecurringEditScope scope = RecurringEditScope.FutureOccurrences);
        Task<bool> CancelOccurrenceAsync(int occurrenceId, string organizerId, bool isAdmin);
    }

    public class RecurringEventService : IRecurringEventService
    {
        private readonly ApplicationDbContext _db;

        public RecurringEventService(ApplicationDbContext db)
        {
            _db = db;
        }

        public IReadOnlyList<EventOccurrence> GenerateOccurrences(EventSeries series, int maxOccurrences = 370)
        {
            if (series.RecurrenceType == EventRecurrenceType.None)
            {
                return Array.Empty<EventOccurrence>();
            }

            if (series.EndDate.Date < series.StartDate.Date || series.Interval < 1)
            {
                return Array.Empty<EventOccurrence>();
            }

            var occurrences = new List<EventOccurrence>();
            var startDate = series.StartDate.Date;
            var endDate = series.EndDate.Date;
            var selectedDays = ParseDays(series.DaysOfWeek);

            for (var date = startDate; date <= endDate && occurrences.Count < maxOccurrences; date = date.AddDays(1))
            {
                if (!ShouldIncludeDate(series, date, startDate, selectedDays))
                {
                    continue;
                }

                var startsAt = date.Add(series.StartTime);
                var endsAt = date.Add(series.EndTime);
                if (endsAt <= startsAt)
                {
                    endsAt = endsAt.AddDays(1);
                }

                occurrences.Add(new EventOccurrence
                {
                    EventSeriesId = series.Id,
                    StartDateTime = startsAt,
                    EndDateTime = endsAt,
                    Status = EventOccurrenceStatus.Scheduled,
                });
            }

            return occurrences;
        }

        public async Task RegenerateOccurrencesAsync(EventSeries series, RecurringEditScope scope = RecurringEditScope.FutureOccurrences)
        {
            var generated = GenerateOccurrences(series);
            var generatedMap = generated.ToDictionary(o => o.StartDateTime);

            var existing = await _db.EventOccurrences
                .Include(o => o.UserTickets)
                .Where(o => o.EventSeriesId == series.Id)
                .ToListAsync();

            var now = DateTime.UtcNow;
            var editableExisting = scope == RecurringEditScope.EntireSeries
                ? existing
                : existing.Where(o => o.StartDateTime >= now).ToList();

            foreach (var occurrence in editableExisting)
            {
                var hasTickets = occurrence.UserTickets.Any();
                if (generatedMap.TryGetValue(occurrence.StartDateTime, out var generatedOccurrence))
                {
                    if (!hasTickets)
                    {
                        occurrence.EndDateTime = generatedOccurrence.EndDateTime;
                    }

                    generatedMap.Remove(occurrence.StartDateTime);
                    continue;
                }

                if (!hasTickets)
                {
                    _db.EventOccurrences.Remove(occurrence);
                }
            }

            foreach (var occurrence in generatedMap.Values)
            {
                occurrence.EventSeriesId = series.Id;
                _db.EventOccurrences.Add(occurrence);
            }

            series.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }

        public async Task<bool> CancelOccurrenceAsync(int occurrenceId, string organizerId, bool isAdmin)
        {
            var occurrence = await _db.EventOccurrences
                .Include(o => o.EventSeries)
                .FirstOrDefaultAsync(o => o.Id == occurrenceId);

            if (occurrence == null)
            {
                return false;
            }

            if (!isAdmin && occurrence.EventSeries.OrganizerId != organizerId)
            {
                return false;
            }

            occurrence.Status = EventOccurrenceStatus.Cancelled;
            await _db.SaveChangesAsync();
            return true;
        }

        private static bool ShouldIncludeDate(EventSeries series, DateTime date, DateTime startDate, HashSet<DayOfWeek> selectedDays)
        {
            if (series.RecurrenceType == EventRecurrenceType.Daily)
            {
                var days = (date.Date - startDate.Date).Days;
                return days % series.Interval == 0;
            }

            if (series.RecurrenceType == EventRecurrenceType.Weekly)
            {
                if (selectedDays.Count > 0 && !selectedDays.Contains(date.DayOfWeek))
                {
                    return false;
                }

                var weekIndex = (int)Math.Floor((date.Date - startDate.Date).TotalDays / 7);
                return weekIndex >= 0 && weekIndex % series.Interval == 0;
            }

            return false;
        }

        private static HashSet<DayOfWeek> ParseDays(string? daysOfWeek)
        {
            var result = new HashSet<DayOfWeek>();
            if (string.IsNullOrWhiteSpace(daysOfWeek))
            {
                return result;
            }

            foreach (var part in daysOfWeek.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (Enum.TryParse<DayOfWeek>(part, ignoreCase: true, out var day))
                {
                    result.Add(day);
                }
                else if (int.TryParse(part, out var numeric) && Enum.IsDefined(typeof(DayOfWeek), numeric))
                {
                    result.Add((DayOfWeek)numeric);
                }
            }

            return result;
        }
    }
}

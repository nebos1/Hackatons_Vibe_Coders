using EventsApp.Models;

namespace EventsApp.ViewModels.Home
{
    public class CalendarViewModel
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public IReadOnlyList<CalendarEventViewModel> Events { get; set; } = Array.Empty<CalendarEventViewModel>();
    }

    public class CalendarEventViewModel
    {
        public int Id { get; set; }
        public string Title { get; set; } = null!;
        public string City { get; set; } = null!;
        public DateTime StartTime { get; set; }
        public EventGenre Genre { get; set; }
    }
}

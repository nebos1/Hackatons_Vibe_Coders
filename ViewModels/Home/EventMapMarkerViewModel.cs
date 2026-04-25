using EventsApp.Models;

namespace EventsApp.ViewModels.Home
{
    public class EventMapMarkerViewModel
    {
        public int EventId { get; set; }
        public string Title { get; set; } = null!;
        public string City { get; set; } = null!;
        public string Address { get; set; } = null!;
        public DateTime StartTime { get; set; }
        public EventGenre Genre { get; set; }
        public string? ImageUrl { get; set; }
        public string OrganizerName { get; set; } = null!;
        public double Lat { get; set; }
        public double Lng { get; set; }
        public bool IsApproximate { get; set; }
    }
}

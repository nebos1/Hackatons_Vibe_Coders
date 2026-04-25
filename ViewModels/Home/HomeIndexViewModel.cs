using EventsApp.Models;
using EventsApp.ViewModels.Events;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace EventsApp.ViewModels.Home
{
    public class HomeIndexViewModel
    {
        public string? Search { get; set; }
        public string? City { get; set; }
        public EventGenre? Genre { get; set; }
        public DateTime? DateFrom { get; set; }

        public IReadOnlyList<EventCardViewModel> LatestEvents { get; set; } = Array.Empty<EventCardViewModel>();
        public IReadOnlyList<EventMapMarkerViewModel> MapMarkers { get; set; } = Array.Empty<EventMapMarkerViewModel>();
        public IReadOnlyList<SelectListItem> Cities { get; set; } = Array.Empty<SelectListItem>();
    }
}

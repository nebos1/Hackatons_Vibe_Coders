using EventsApp.Models;
using EventsApp.ViewModels.Events;
using EventsApp.ViewModels.Home;
using EventsApp.ViewModels.Posts;

namespace EventsApp.ViewModels.Search
{
    public class SearchResultsViewModel
    {
        public string? Query { get; set; }
        public IReadOnlyList<EventCardViewModel> Events { get; set; } = Array.Empty<EventCardViewModel>();
        public IReadOnlyList<PostCardViewModel> Posts { get; set; } = Array.Empty<PostCardViewModel>();
        public IReadOnlyList<EventMapMarkerViewModel> MapMarkers { get; set; } = Array.Empty<EventMapMarkerViewModel>();
        public string? AiHint { get; set; }
        public string? AiHintLevel { get; set; }
        public bool AiUsed { get; set; }
        public string? InterpretedCity { get; set; }
        public string? InterpretedKeyword { get; set; }
        public EventGenre? InterpretedGenre { get; set; }
        public DateTime? InterpretedDateFrom { get; set; }
        public DateTime? InterpretedDateTo { get; set; }
        public bool InterpretedNearMe { get; set; }
    }
}

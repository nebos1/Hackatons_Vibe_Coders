using EventsApp.Models;

namespace EventsApp.ViewModels.Wrapped
{
    public class DayPlanViewModel
    {
        public string? City { get; set; }
        public DateTime When { get; set; } = DateTime.UtcNow.Date;
        public string? Vibe { get; set; }
        public IReadOnlyList<string> Cities { get; set; } = Array.Empty<string>();
        public IReadOnlyList<DayPlanCandidate> Candidates { get; set; } = Array.Empty<DayPlanCandidate>();
        public string? AiPlan { get; set; }
        public string? AiError { get; set; }
        public bool HasResult => !string.IsNullOrWhiteSpace(AiPlan) || Candidates.Count > 0;
    }

    public class DayPlanCandidate
    {
        public int Id { get; set; }
        public string Title { get; set; } = null!;
        public string City { get; set; } = null!;
        public string? Address { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public EventGenre Genre { get; set; }
        public string? ImageUrl { get; set; }
    }
}

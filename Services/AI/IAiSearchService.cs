using EventsApp.Models;

namespace EventsApp.Services.AI
{
    public class AiSearchIntent
    {
        public string? City { get; set; }
        public string? Keyword { get; set; }
        public EventGenre? Genre { get; set; }
        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }
        public bool NearMe { get; set; }
        public string? Explanation { get; set; }
    }

    public enum AiStatus
    {
        Ok,
        Disabled,
        MissingProjectId,
        ProvisionFailed,
        CallFailed,
        ParseFailed,
    }

    public interface IAiSearchService
    {
        bool IsEnabled { get; }
        AiStatus LastStatus { get; }
        string? LastStatusDetail { get; }
        Task<AiSearchIntent?> InterpretAsync(string query, CancellationToken cancellationToken = default);
        Task<string?> GenerateEventDescriptionAsync(string title, string? city, string? genre, string? hints, CancellationToken cancellationToken = default);
    }
}

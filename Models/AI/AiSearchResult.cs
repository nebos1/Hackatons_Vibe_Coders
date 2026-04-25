namespace EventsApp.Models.AI
{
    public class AiSearchResult
    {
        public string? City { get; set; }
        public string? Genre { get; set; }
        public string? Keyword { get; set; }
        public string? DateIntent { get; set; }
        public bool NearMe { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public string[] Keywords { get; set; } = System.Array.Empty<string>();
        public string RawQuery { get; set; } = string.Empty;
        public bool AiUsed { get; set; }
        public string? AiStatus { get; set; }
        public string? AiStatusDetail { get; set; }
    }
}

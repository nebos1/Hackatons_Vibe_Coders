namespace EventsApp.Configuration
{
    public class GoogleMapsOptions
    {
        public const string SectionName = "GoogleMaps";

        public string ApiKey { get; set; } = string.Empty;
        public string DefaultRegion { get; set; } = "BG";
        public string DefaultLanguage { get; set; } = "bg";

        public bool IsEnabled => !string.IsNullOrWhiteSpace(ApiKey);
    }
}

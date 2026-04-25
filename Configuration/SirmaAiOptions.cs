namespace EventsApp.Configuration
{
    public class SirmaAiOptions
    {
        public const string SectionName = "SirmaAi";

        public string Domain { get; set; } = string.Empty;
        public string ApiKey { get; set; } = string.Empty;
        public string AgentId { get; set; } = string.Empty;
    }
}
